using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Application.Registration.Rules;

namespace Advisor.Tests;

/// <summary>Kural birim testleri: OBİS'e gitmeden, elle kurulmuş küçük bağlamlarla.</summary>
public class RegistrationRuleTests
{
    // ---- test verisi kurucuları ----

    private static ObisCourse Course(string code, int term, int ects = 5, params string[] prereq) =>
        new(code, $"Ders {code}", 3, ects, term, true, prereq.ToList());

    private static ObisTranscriptEntry Passed(string code, int term, string grade = "CC", int credit = 3) =>
        new("1", term, code, $"Ders {code}", credit, 5, grade, grade != "FF");

    private static ObisTranscriptEntry Failed(string code, int term, int credit = 3) =>
        new("1", term, code, $"Ders {code}", credit, 5, "FF", false);

    private static ObisRegistration Reg(string code, string section = "A", int term = 3) =>
        new("1", term, code, section, "Beklemede");

    private static ObisScheduleSlot Slot(string code, DayOfWeek day, string start, string end, string section = "A") =>
        new(1, code, $"Ders {code}", section, day, TimeOnly.Parse(start), TimeOnly.Parse(end), "D-1");

    private static ObisExamSlot Exam(string code, string date, string start, string end,
        ObisExamType type = ObisExamType.Final, string section = "A") =>
        new(1, code, $"Ders {code}", section, type, DateOnly.Parse(date), TimeOnly.Parse(start), TimeOnly.Parse(end), "D-1");

    private static RegistrationContext Ctx(
        IEnumerable<ObisRegistration> selected,
        IEnumerable<ObisCourse>? courses = null,
        IEnumerable<ObisTranscriptEntry>? history = null,
        IEnumerable<ObisScheduleSlot>? schedule = null,
        IEnumerable<ObisExamSlot>? exams = null,
        IEnumerable<string>? mandatory = null,
        RegistrationRuleOptions? options = null)
    {
        var courseList = (courses ?? Enumerable.Empty<ObisCourse>()).ToList();
        var historyList = (history ?? Enumerable.Empty<ObisTranscriptEntry>()).ToList();
        return RegistrationValidationService.BuildContext(
            new ObisStudent("1", "Test", "Öğrenci", 2, "BLM", "ADV1", 0, "Aktif"),
            3,
            selected,
            historyList,
            courseList,
            schedule ?? Enumerable.Empty<ObisScheduleSlot>(),
            exams ?? Enumerable.Empty<ObisExamSlot>(),
            new ObisCurriculum("BLM", (mandatory ?? courseList.Select(c => c.CourseCode)).ToList(), 0, 240, 30),
            options ?? new RegistrationRuleOptions());
    }

    // ---- SectionProgramRule ----

    private static RegistrationContext CtxFor(string program, params ObisRegistration[] selected) =>
        RegistrationValidationService.BuildContext(
            new ObisStudent("1", "Test", "Öğrenci", 4, "BLM", "ADV1", 0, "Aktif", Program: program),
            3, selected, Array.Empty<ObisTranscriptEntry>(), new[] { Course("A1", 3), Course("B1", 3) },
            Array.Empty<ObisScheduleSlot>(), Array.Empty<ObisExamSlot>(),
            new ObisCurriculum("BLM", new List<string>(), 0, 240, 30), new RegistrationRuleOptions());

    [Theory]
    [InlineData("Birinci Öğretim", "İÖ")]
    [InlineData("İkinci Öğretim", "NÖ")]
    public void SectionProgram_OtherProgramsSection_IsError(string program, string section)
    {
        var issue = Assert.Single(new SectionProgramRule().Evaluate(CtxFor(program, Reg("A1", section))));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Theory]
    [InlineData("Birinci Öğretim", "NÖ")]
    [InlineData("İkinci Öğretim", "İÖ")]
    [InlineData("Birinci Öğretim", "NÖ/İÖ")]
    [InlineData("İkinci Öğretim", "NÖ/İÖ")]
    public void SectionProgram_OwnOrSharedSection_NoIssue(string program, string section) =>
        Assert.Empty(new SectionProgramRule().Evaluate(CtxFor(program, Reg("A1", section))));

    [Fact]
    public void TimeConflict_AlternativeFromOtherProgram_IsNotSuggested()
    {
        // İkinci öğretim öğrencisine NÖ şubesi önerilmez.
        var ctx = RegistrationValidationService.BuildContext(
            new ObisStudent("1", "Test", "Öğrenci", 4, "BLM", "ADV1", 0, "Aktif", Program: "İkinci Öğretim"),
            3, new[] { Reg("A1", "NÖ/İÖ"), Reg("B1", "İÖ") }, Array.Empty<ObisTranscriptEntry>(),
            new[] { Course("A1", 1), Course("B1", 3) },
            new[]
            {
                Slot("A1", DayOfWeek.Monday, "17:00", "19:00", "NÖ/İÖ"),
                Slot("B1", DayOfWeek.Monday, "17:00", "19:00", "İÖ"),
                Slot("B1", DayOfWeek.Tuesday, "09:00", "11:00", "NÖ"),
            },
            Array.Empty<ObisExamSlot>(), new ObisCurriculum("BLM", new List<string>(), 0, 240, 30), new RegistrationRuleOptions());

        var issue = Assert.Single(new TimeConflictRule().Evaluate(ctx));
        Assert.Contains("alternatif şube bulunamadı", issue.Message);
    }

    // ---- TimeConflictRule ----

    [Fact]
    public void TimeConflict_OverlappingSameDay_IsError()
    {
        var ctx = Ctx(
            new[] { Reg("A1"), Reg("B1") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Monday, "10:00", "12:00"),
            });

        var issue = Assert.Single(new TimeConflictRule().Evaluate(ctx));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("A1", issue.CourseCodes);
        Assert.Contains("B1", issue.CourseCodes);
    }

    [Fact]
    public void TimeConflict_NoAttendanceRequirement_IsNotConflict()
    {
        // İki dersten birinde devam şartı yoksa çakışma sayılmaz.
        var ctx = Ctx(
            new[] { Reg("A1") with { AttendanceRequired = false }, Reg("B1") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Monday, "10:00", "12:00"),
            });

        Assert.Empty(new TimeConflictRule().Evaluate(ctx));
    }

    [Fact]
    public void TimeConflict_RetakeIsBase_AndAlternativeSectionIsSuggested()
    {
        // L1 daha önce kalınmış (alttan); H1 üstten. H1'in B şubesi başka saatte: öneri olarak verilir.
        var courses = new[] { Course("L1", 1), Course("H1", 5) };
        var ctx = Ctx(
            new[] { Reg("H1"), Reg("L1") }, courses,
            history: new[] { Failed("L1", 2) },
            schedule: new[]
            {
                Slot("L1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("H1", DayOfWeek.Monday, "10:00", "12:00", "A"),
                Slot("H1", DayOfWeek.Tuesday, "14:00", "16:00", "B"),
            });

        var issue = Assert.Single(new TimeConflictRule().Evaluate(ctx));
        Assert.Equal(new[] { "L1", "H1" }, issue.CourseCodes);
        Assert.Contains("alttan", issue.Message);
        Assert.Contains("B şubesi (Salı 14:00-16:00)", issue.Message);
    }

    [Fact]
    public void TimeConflict_AlternativeThatClashesWithOtherCourse_IsNotSuggested()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5), Course("X1", 5) };
        var ctx = Ctx(
            new[] { Reg("H1"), Reg("L1"), Reg("X1") }, courses,
            history: new[] { Failed("L1", 2) },
            schedule: new[]
            {
                Slot("L1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("H1", DayOfWeek.Monday, "10:00", "12:00", "A"),
                Slot("H1", DayOfWeek.Tuesday, "14:00", "16:00", "B"),
                Slot("X1", DayOfWeek.Tuesday, "15:00", "17:00"),
            });

        var issue = Assert.Single(new TimeConflictRule().Evaluate(ctx));
        Assert.Contains("alternatif şube bulunamadı", issue.Message);
    }

    [Fact]
    public void TimeConflict_NoRetake_LowerTermCourseIsBase()
    {
        var courses = new[] { Course("L1", 3), Course("H1", 6) };
        var ctx = Ctx(
            new[] { Reg("H1"), Reg("L1") }, courses,
            schedule: new[]
            {
                Slot("L1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("H1", DayOfWeek.Monday, "10:00", "12:00"),
            });

        var issue = Assert.Single(new TimeConflictRule().Evaluate(ctx));
        Assert.Equal(new[] { "L1", "H1" }, issue.CourseCodes);
    }

    [Fact]
    public void TimeConflict_BackToBack_IsNotConflict()
    {
        var ctx = Ctx(
            new[] { Reg("A1"), Reg("B1") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Monday, "11:00", "13:00"),
            });

        Assert.Empty(new TimeConflictRule().Evaluate(ctx));
    }

    [Fact]
    public void TimeConflict_DifferentDays_IsNotConflict()
    {
        var ctx = Ctx(
            new[] { Reg("A1"), Reg("B1") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Tuesday, "09:00", "11:00"),
            });

        Assert.Empty(new TimeConflictRule().Evaluate(ctx));
    }

    [Fact]
    public void TimeConflict_UsesSelectedSection_NotOtherSection()
    {
        // B1'in B şubesi A1 ile çakışır, ama öğrenci A şubesini seçti.
        var ctx = Ctx(
            new[] { Reg("A1"), Reg("B1", "A") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Wednesday, "09:00", "11:00", "A"),
                Slot("B1", DayOfWeek.Monday, "09:00", "11:00", "B"),
            });

        Assert.Empty(new TimeConflictRule().Evaluate(ctx));
    }

    [Fact]
    public void TimeConflict_IgnoresUnselectedCourses()
    {
        var ctx = Ctx(
            new[] { Reg("A1") },
            schedule: new[]
            {
                Slot("A1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("B1", DayOfWeek.Monday, "09:00", "11:00"),
            });

        Assert.Empty(new TimeConflictRule().Evaluate(ctx));
    }

    // ---- LowerSemesterPriorityRule ----

    [Fact]
    public void LowerSemester_FailedLowerCourse_WithHigherSelected_IsWarning()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("H1") }, courses, history: new[] { Failed("L1", 2) });

        var issue = Assert.Single(new LowerSemesterPriorityRule().Evaluate(ctx));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Contains("kalınmış", issue.Message);
        Assert.Contains("L1", issue.CourseCodes);
        Assert.Contains("H1", issue.CourseCodes);
    }

    [Fact]
    public void LowerSemester_NeverTakenLowerCourse_IsWarningWithDifferentWording()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("H1") }, courses);

        var issue = Assert.Single(new LowerSemesterPriorityRule().Evaluate(ctx));
        Assert.Contains("hiç alınmamış", issue.Message);
    }

    [Fact]
    public void LowerSemester_LowerCourseAlsoSelected_NoIssue()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("L1"), Reg("H1") }, courses, history: new[] { Failed("L1", 2) });

        Assert.Empty(new LowerSemesterPriorityRule().Evaluate(ctx));
    }

    [Fact]
    public void LowerSemester_OnlyLowerOrSameTermSelected_NoIssue()
    {
        var courses = new[] { Course("L1", 1), Course("L2", 1) };
        var ctx = Ctx(new[] { Reg("L2") }, courses, history: new[] { Failed("L1", 2) });

        Assert.Empty(new LowerSemesterPriorityRule().Evaluate(ctx));
    }

    [Fact]
    public void LowerSemester_ElectiveNotInMandatoryList_IsIgnored()
    {
        var courses = new[] { Course("E1", 1), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("H1") }, courses, history: new[] { Failed("E1", 2) }, mandatory: new[] { "H1" });

        Assert.Empty(new LowerSemesterPriorityRule().Evaluate(ctx));
    }

    [Fact]
    public void LowerSemester_CourseOfOtherSemester_IsIgnored()
    {
        // Ctx dönemi 3 (güz); 2. dönem dersi (bahar) bu yarıyılda açılmadığı için uyarı üretmez.
        var courses = new[] { Course("L1", 2), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("H1") }, courses, history: new[] { Failed("L1", 2) });

        Assert.Empty(new LowerSemesterPriorityRule().Evaluate(ctx));
    }

    [Fact]
    public void LowerSemester_PassedLowerCourse_NoIssue()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5) };
        var ctx = Ctx(new[] { Reg("H1") }, courses, history: new[] { Passed("L1", 2) });

        Assert.Empty(new LowerSemesterPriorityRule().Evaluate(ctx));
    }

    // ---- CreditLimitRule ----

    private static (ObisCourse[] Courses, ObisRegistration[] Regs) SixCourses(int ectsEach)
    {
        var courses = Enumerable.Range(1, 6).Select(i => Course($"C{i}", 3, ectsEach)).ToArray();
        return (courses, courses.Select(c => Reg(c.CourseCode)).ToArray());
    }

    [Fact]
    public void CreditLimit_NormalGpa_AtLimit_NoIssue()
    {
        // GANO 2.0 ('CC') → normal sınır 31; 6 x 5 = 30 AKTS.
        var (courses, regs) = SixCourses(5);
        var ctx = Ctx(regs, courses, history: new[] { Passed("OLD", 1, "CC") });

        Assert.Empty(new CreditLimitRule().Evaluate(ctx));
    }

    [Fact]
    public void CreditLimit_NormalGpa_OverLimit_IsError()
    {
        var (courses, regs) = SixCourses(6); // 36 AKTS
        var ctx = Ctx(regs, courses, history: new[] { Passed("OLD", 1, "CC") });

        var issue = Assert.Single(new CreditLimitRule().Evaluate(ctx));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("36", issue.Message);
        Assert.Contains("31", issue.Message);
    }

    [Fact]
    public void CreditLimit_LowGpa_HasLowerLimit()
    {
        // GANO 1.0 ('DD') → sınır 24; 5 x 5 = 25 AKTS aşar, normal GANO'da aşmazdı.
        var courses = Enumerable.Range(1, 5).Select(i => Course($"C{i}", 3, 5)).ToArray();
        var regs = courses.Select(c => Reg(c.CourseCode)).ToArray();
        var ctx = Ctx(regs, courses, history: new[] { Passed("OLD", 1, "DD") });

        Assert.Single(new CreditLimitRule().Evaluate(ctx));
    }

    [Fact]
    public void CreditLimit_HighGpa_GetsExtraCredits()
    {
        // GANO 4.0 → sınır 37; 7 x 5 = 35 AKTS serbest, normal GANO'da aşardı.
        var courses = Enumerable.Range(1, 7).Select(i => Course($"C{i}", 3, 5)).ToArray();
        var regs = courses.Select(c => Reg(c.CourseCode)).ToArray();
        var ctx = Ctx(regs, courses, history: new[] { Passed("OLD", 1, "AA") });

        Assert.Empty(new CreditLimitRule().Evaluate(ctx));
    }

    [Fact]
    public void CreditLimit_ThresholdsAreConfigurable()
    {
        var (courses, regs) = SixCourses(5); // 30 AKTS
        var options = new RegistrationRuleOptions { NormalMaxEcts = 20 };
        var ctx = Ctx(regs, courses, history: new[] { Passed("OLD", 1, "CC") }, options: options);

        Assert.Single(new CreditLimitRule().Evaluate(ctx));
    }

    [Fact]
    public void CreditLimit_NoHistory_UsesNormalLimit()
    {
        var (courses, regs) = SixCourses(6); // 36 AKTS, geçmiş yok
        var ctx = Ctx(regs, courses);

        Assert.Single(new CreditLimitRule().Evaluate(ctx));
        Assert.Equal(31, CreditLimitRule.MaxEctsFor(ctx));
    }

    // ---- RetakeRule ----

    [Fact]
    public void Retake_HighGradeCourseSelectedAgain_IsError()
    {
        var ctx = Ctx(new[] { Reg("A1") }, history: new[] { Passed("A1", 1, "AA") });

        var issue = Assert.Single(new RetakeRule().Evaluate(ctx));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("AA", issue.Message);
    }

    [Fact]
    public void Retake_GradeAtThreshold_IsError()
    {
        var ctx = Ctx(new[] { Reg("A1") }, history: new[] { Passed("A1", 1, "BB") });

        Assert.Single(new RetakeRule().Evaluate(ctx));
    }

    [Theory]
    [InlineData("CB")]
    [InlineData("CC")]
    [InlineData("DD")]
    public void Retake_CbOrLowerGrade_IsAllowed(string grade) =>
        Assert.Empty(new RetakeRule().Evaluate(Ctx(new[] { Reg("A1") }, history: new[] { Passed("A1", 1, grade) })));

    [Fact]
    public void Retake_LowPassingGrade_NoIssue()
    {
        var ctx = Ctx(new[] { Reg("A1") }, history: new[] { Passed("A1", 1, "DD") });

        Assert.Empty(new RetakeRule().Evaluate(ctx));
    }

    [Fact]
    public void Retake_FailedBefore_NoIssue()
    {
        var ctx = Ctx(new[] { Reg("A1") }, history: new[] { Failed("A1", 1) });

        Assert.Empty(new RetakeRule().Evaluate(ctx));
    }

    [Fact]
    public void Retake_NeverTaken_NoIssue()
    {
        Assert.Empty(new RetakeRule().Evaluate(Ctx(new[] { Reg("A1") })));
    }

    // ---- GANO hesabı ve motor ----

    [Fact]
    public void Gpa_IsCreditWeighted_AndLatestAttemptWins()
    {
        // A1 önce FF, sonra AA (son deneme geçerli): (4*3 + 2*3) / 6 = 3.00
        var gpa = RegistrationValidationService.ComputeGpa(new[]
        {
            Failed("A1", 1), Passed("A1", 2, "AA"), Passed("B1", 1, "CC"),
        });

        Assert.Equal(3.0, gpa);
    }

    [Fact]
    public void Gpa_EmptyHistory_IsZero() =>
        Assert.Equal(0, RegistrationValidationService.ComputeGpa(Array.Empty<ObisTranscriptEntry>()));

    [Fact]
    public void Validator_OrdersIssuesBySeverity_AndCountsThem()
    {
        var courses = new[] { Course("L1", 1), Course("H1", 5), Course("X1", 5) };
        var ctx = Ctx(
            new[] { Reg("H1"), Reg("X1") }, courses,
            history: new[] { Failed("L1", 2), Passed("H1", 1, "AA") },
            schedule: new[]
            {
                Slot("H1", DayOfWeek.Monday, "09:00", "11:00"),
                Slot("X1", DayOfWeek.Monday, "10:00", "12:00"),
            });

        var validator = new RegistrationValidator(new IRegistrationRule[]
        {
            new RetakeRule(), new LowerSemesterPriorityRule(), new TimeConflictRule(),
        });
        var result = validator.Validate(ctx);

        Assert.Equal(2, result.ErrorCount); // saat çakışması + AA ile geçilmiş dersin tekrarı
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(0, result.InfoCount);
        Assert.Equal(IssueSeverity.Error, result.Issues[0].Severity);
        Assert.Equal(IssueSeverity.Warning, result.Issues[^1].Severity);
    }

    [Fact]
    public void AllRules_AreDiscoveredByAssemblyScan()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddRegistrationRules(new RegistrationRuleOptions());
        var codes = services
            .Where(d => d.ServiceType == typeof(IRegistrationRule))
            .Select(d => d.ImplementationType!)
            .ToHashSet();

        Assert.Equal(5, codes.Count);
    }
}
