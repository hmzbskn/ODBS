using System.Text.Json;
using System.Text.Json.Serialization;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Application.Registration.Rules;
using Xunit.Abstractions;

namespace Advisor.Tests;

/// <summary>Mock OBİS'in gerçek seed JSON'unu (ağ olmadan) okuyan istemci; kuralların
/// seed'deki bilinçli uç durumları yakalayıp yakalamadığını doğrular.</summary>
internal class SeedObisClient : IObisClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dir;

    public SeedObisClient()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Obis.MockApi", "SeedData", "json")))
            dir = dir.Parent;
        _dir = dir is null
            ? throw new DirectoryNotFoundException("Obis.MockApi/SeedData/json bulunamadı.")
            : Path.Combine(dir.FullName, "src", "Obis.MockApi", "SeedData", "json");
    }

    private List<T> Load<T>(string file) =>
        JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(_dir, file)), Json)!;

    /// <summary>Testlerin çoğu "danisman1 bütün demo öğrencilerin (ogrenci1-4) danışmanı" varsayımıyla yazıldı.
    /// Gerçek seed artık sınıf başına tek danışman verdiği için bu fixture eski dağılımı yeniden kurar:
    /// öğrenci numarasının son üç hanesi sıra numarasıdır, her danışman 21 öğrenci (ADV1 = 1-21, ADV2 = 22-42, ADV3 = 43-63).
    /// Sınıf başına tek danışman kuralı ayrıca `AdvisorPerClassYearTests` ile gerçek seed üzerinde sınanır.</summary>
    private static ObisStudent LegacyAdvisor(ObisStudent s) =>
        s with { AdvisorId = $"ADV{(int.Parse(s.StudentNo[^3..]) - 1) / 21 + 1}" };

    /// <summary>Seed dosyasındaki gerçek (sınıfa göre dağıtılmış) danışman atamaları.</summary>
    public List<ObisStudent> GetSeedStudents() => Load<ObisStudent>("students.json");

    public Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default) =>
        Task.FromResult(Load<ObisStudent>("students.json").Select(LegacyAdvisor).ToList());
    public Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisStudent>("students.json").Where(s => s.StudentNo == studentNo).Select(LegacyAdvisor).FirstOrDefault());
    public Task<List<ObisTranscriptEntry>> GetTranscriptAsync(string studentNo, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisTranscriptEntry>("transcripts.json").Where(t => t.StudentNo == studentNo).ToList());
    public Task<List<ObisRegistration>> GetRegistrationsAsync(string studentNo, int? term = null, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisRegistration>("registrations.json")
            .Where(r => r.StudentNo == studentNo && (term is null || r.Term == term)).ToList());
    public Task<List<ObisAttendance>> GetAttendanceAsync(string studentNo, int? term = null, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisAttendance>("attendance.json").Where(a => a.StudentNo == studentNo).ToList());
    public Task<List<ObisCourse>> GetCoursesAsync(CancellationToken ct = default) => Task.FromResult(Load<ObisCourse>("courses.json"));
    public Task<List<ObisScheduleSlot>> GetScheduleAsync(int? term = null, CancellationToken ct = default) => Task.FromResult(Load<ObisScheduleSlot>("schedule.json"));
    public Task<List<ObisExamSlot>> GetExamsAsync(int? term = null, CancellationToken ct = default) => Task.FromResult(Load<ObisExamSlot>("exams.json"));
    public Task<ObisCurriculum> GetCurriculumAsync(CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Deserialize<ObisCurriculum>(File.ReadAllText(Path.Combine(_dir, "curriculum.json")), Json)!);
    public Task<ObisInternship> GetInternshipAsync(string studentNo, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisInternship>("internships.json").FirstOrDefault(i => i.StudentNo == studentNo) ?? new ObisInternship(studentNo, 0));
    public Task<List<ObisAdvisor>> GetAdvisorsAsync(CancellationToken ct = default) => Task.FromResult(Load<ObisAdvisor>("advisors.json"));
}

public class SeedEdgeCaseTests
{
    private readonly ITestOutputHelper _output;
    private readonly RegistrationValidationService _service;

    public SeedEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new RegistrationRuleOptions();
        var rules = typeof(IRegistrationRule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRegistrationRule).IsAssignableFrom(t))
            .Select(t => (IRegistrationRule)Activator.CreateInstance(t)!)
            .ToList();
        _service = new RegistrationValidationService(new SeedObisClient(), new RegistrationValidator(rules), options);
    }

    private async Task<RegistrationValidationResult> Validate(string studentNo)
    {
        var result = await _service.ValidateAsync(studentNo);
        Assert.NotNull(result);
        return result!;
    }

    private static bool Has(RegistrationValidationResult r, string ruleCode) => r.Issues.Any(i => i.RuleCode == ruleCode);

    [Fact]
    public async Task Edge01_TimeConflict_WithoutAlternative_IsDetected()
    {
        // Gerçek güz programı: Mantık Devreleri-1 (alttan) ve Programlama Dili-3 ikisi de Pazartesi 13:25-16:55.
        var r = await Validate("20241011");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == TimeConflictRule.RuleCode);
        Assert.Equal(new[] { "3301353", "3301552" }, issue.CourseCodes);
        Assert.Contains("alternatif şube bulunamadı", issue.Message);
    }

    [Fact]
    public async Task Edge02_NoAttendanceRequirement_IsNotAConflict()
    {
        var r = await Validate("20251006");
        Assert.DoesNotContain(r.Issues, i => i.RuleCode == TimeConflictRule.RuleCode);
        Assert.Contains(r.Selected, x => x.CourseCode == "3301154" && !x.AttendanceRequired);
    }

    [Fact]
    public async Task Edge03_LowerSemesterPriority_IsDetected()
    {
        var r = await Validate("20241012");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == LowerSemesterPriorityRule.RuleCode && i.Message.Contains("kalınmış"));
        Assert.Contains("3301152", issue.CourseCodes);
    }

    [Fact]
    public async Task Edge04_CreditLimit_IsDetected()
    {
        var r = await Validate("20251007");
        Assert.True(Has(r, CreditLimitRule.RuleCode));
        Assert.True(r.TotalEcts > r.MaxEcts);
    }

    [Fact]
    public async Task Edge05_ConflictWithAlternativeSection_IsDetected()
    {
        // Alttan Veri Haberleşmesi temel; Veri Bilimi NÖ ile çakışır, İÖ şubesi önerilir.
        var r = await Validate("20231016");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == TimeConflictRule.RuleCode);
        Assert.Equal(new[] { "3301553", "3301753" }, issue.CourseCodes);
        Assert.Contains("İÖ şubesi (Çarşamba 17:00-19:25)", issue.Message);
        // İkinci öğretim öğrencisi Veri Bilimi'ni NÖ şubesinden seçmiş.
        var section = Assert.Single(r.Issues, i => i.RuleCode == SectionProgramRule.RuleCode);
        Assert.Equal(new[] { "3301753" }, section.CourseCodes);
    }

    [Fact]
    public async Task OnlyEdge06_RetakesACoursePassedWithBbOrHigher()
    {
        var students = await new SeedObisClient().GetStudentsAsync();
        foreach (var s in students.Where(s => s.StudentNo != "20231017"))
        {
            var r = await Validate(s.StudentNo);
            Assert.DoesNotContain(r.Issues, i => i.RuleCode == RetakeRule.RuleCode);
        }
    }

    [Fact]
    public async Task EveningProgramStudents_AreOnlyInFourthYear()
    {
        // Güz programında 1-3. sınıfın İÖ saati yok: ikinci öğretim yalnızca 4. sınıfta.
        var students = await new SeedObisClient().GetStudentsAsync();
        Assert.Contains(students, s => s.Program == "İkinci Öğretim");
        Assert.All(students.Where(s => s.Program == "İkinci Öğretim"), s => Assert.Equal(4, s.ClassYear));
    }

    [Fact]
    public async Task OnlyEdge05_TakesASectionOfTheOtherProgram()
    {
        var students = await new SeedObisClient().GetStudentsAsync();
        foreach (var s in students.Where(s => s.StudentNo != "20231016"))
        {
            var r = await Validate(s.StudentNo);
            Assert.DoesNotContain(r.Issues, i => i.RuleCode == SectionProgramRule.RuleCode);
        }
    }

    [Fact]
    public async Task Edge06_Retake_IsDetected()
    {
        var r = await Validate("20231017");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == RetakeRule.RuleCode);
        Assert.Contains("3301551", issue.CourseCodes);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public async Task Edge01_And_Edge05_DoNotLeakIntoOtherStudents()
    {
        // Uç durum için ortak şubeler değiştirilmemeli: birinci sınıf öğrencileri çakışma görmemeli.
        var students = await new SeedObisClient().GetStudentsAsync();
        foreach (var s in students.Where(s => s.ClassYear == 1))
        {
            var r = await Validate(s.StudentNo);
            Assert.DoesNotContain(r.Issues, i => i.RuleCode == TimeConflictRule.RuleCode);
        }
    }

    [Fact]
    public async Task EdgeCases_AreDistinguishable_EachHasItsOwnPrimaryFinding()
    {
        // Her uç durum öğrencisi beklenen kuralı tetiklemeli.
        var expected = new Dictionary<string, string>
        {
            ["20241011"] = TimeConflictRule.RuleCode,
            ["20241012"] = LowerSemesterPriorityRule.RuleCode,
            ["20251007"] = CreditLimitRule.RuleCode,
            ["20231016"] = TimeConflictRule.RuleCode,
            ["20231017"] = RetakeRule.RuleCode,
        };
        foreach (var (studentNo, rule) in expected)
            Assert.True(Has(await Validate(studentNo), rule), $"{studentNo} için {rule} bulunamadı.");
    }

    [Fact]
    public async Task Report_AllStudents_FindingsPerRule()
    {
        // Bilgi amaçlı döküm + temel sağlamlık: hiçbir öğrenci için motor patlamamalı.
        var students = await new SeedObisClient().GetStudentsAsync();
        var perRule = new SortedDictionary<string, int>();
        foreach (var s in students)
        {
            var r = await Validate(s.StudentNo);
            foreach (var i in r.Issues)
            {
                perRule[i.RuleCode] = perRule.GetValueOrDefault(i.RuleCode) + 1;
                _output.WriteLine($"{s.StudentNo} t{r.Term} [{i.Severity}] {i.RuleCode}: {i.Message}");
            }
        }
        foreach (var (rule, count) in perRule) _output.WriteLine($"TOPLAM {rule}: {count}");
        Assert.Equal(63, students.Count);
    }
}
