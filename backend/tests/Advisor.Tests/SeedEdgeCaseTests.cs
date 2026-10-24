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

    public Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default) => Task.FromResult(Load<ObisStudent>("students.json"));
    public Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default) =>
        Task.FromResult(Load<ObisStudent>("students.json").FirstOrDefault(s => s.StudentNo == studentNo));
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
    public async Task Edge01_TimeConflict_IsDetected()
    {
        var r = await Validate("20241011");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == TimeConflictRule.RuleCode);
        Assert.Contains("MAT101", issue.CourseCodes);
        Assert.Contains("BLM301", issue.CourseCodes);
    }

    [Fact]
    public async Task Edge02_PrerequisiteViolation_IsDetected()
    {
        var r = await Validate("20251006");
        // BLM201 ve BLM202 ikisi de BLM103'e bağlı; her biri ayrı bulgu.
        var issues = r.Issues.Where(i => i.RuleCode == PrerequisiteRule.RuleCode).ToList();
        var blm201 = Assert.Single(issues, i => i.CourseCodes[0] == "BLM201");
        Assert.Contains("BLM103", blm201.CourseCodes);
    }

    [Fact]
    public async Task Edge03_LowerSemesterPriority_IsDetected()
    {
        var r = await Validate("20241012");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == LowerSemesterPriorityRule.RuleCode && i.Message.Contains("kalınmış"));
        Assert.Contains("FIZ102", issue.CourseCodes);
    }

    [Fact]
    public async Task Edge04_CreditLimit_IsDetected()
    {
        var r = await Validate("20251007");
        Assert.True(Has(r, CreditLimitRule.RuleCode));
        Assert.True(r.TotalEcts > r.MaxEcts);
    }

    [Fact]
    public async Task Edge05_ExamConflict_IsDetected()
    {
        var r = await Validate("20231016");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == ExamConflictRule.RuleCode);
        Assert.Contains("BLM203", issue.CourseCodes);
        Assert.Contains("BLM401", issue.CourseCodes);
    }

    [Fact]
    public async Task Edge06_Retake_IsDetected()
    {
        var r = await Validate("20231017");
        var issue = Assert.Single(r.Issues, i => i.RuleCode == RetakeRule.RuleCode);
        Assert.Contains("BLM201", issue.CourseCodes);
        Assert.Equal(IssueSeverity.Info, issue.Severity);
    }

    [Fact]
    public async Task Edge01_And_Edge05_DoNotLeakIntoOtherStudents()
    {
        // Uç durum için ortak şubeler değiştirilmemeli: birinci sınıf öğrencileri çakışma görmemeli.
        var students = await new SeedObisClient().GetStudentsAsync();
        foreach (var s in students.Where(s => s.ClassYear == 1))
        {
            var r = await Validate(s.StudentNo);
            Assert.DoesNotContain(r.Issues, i => i.RuleCode is TimeConflictRule.RuleCode or ExamConflictRule.RuleCode);
        }
    }

    [Fact]
    public async Task Edge05_HasExamConflict_ButNoTimeConflict()
    {
        var r = await Validate("20231016");
        Assert.True(Has(r, ExamConflictRule.RuleCode));
        Assert.False(Has(r, TimeConflictRule.RuleCode));
    }

    [Fact]
    public async Task EdgeCases_AreDistinguishable_EachHasItsOwnPrimaryFinding()
    {
        // Her uç durum öğrencisi beklenen kuralı tetiklemeli.
        var expected = new Dictionary<string, string>
        {
            ["20241011"] = TimeConflictRule.RuleCode,
            ["20251006"] = PrerequisiteRule.RuleCode,
            ["20241012"] = LowerSemesterPriorityRule.RuleCode,
            ["20251007"] = CreditLimitRule.RuleCode,
            ["20231016"] = ExamConflictRule.RuleCode,
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
