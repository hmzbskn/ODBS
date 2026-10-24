using Advisor.Application.Analytics;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;

namespace Advisor.Tests;

public class RiskCalculatorTests
{
    private static readonly RiskOptions O = new();

    private static ObisTranscriptEntry E(int term, string code, string grade, int credit = 3) =>
        new("1", term, code, $"Ders {code}", credit, 5, grade, grade is not ("FF" or "FD"));

    private static ObisAttendance A(int term, string code, int attended, int total = 14) => new("1", term, code, total, attended);

    private static RiskAssessment Assess(
        IEnumerable<ObisTranscriptEntry> transcript, IEnumerable<ObisAttendance>? attendance = null, int term = 5, RiskOptions? o = null) =>
        RiskCalculator.Assess("1", transcript.ToList(), (attendance ?? Array.Empty<ObisAttendance>()).ToList(), term, o ?? O);

    private static RiskIndicator? Find(RiskAssessment a, string code) => a.Indicators.FirstOrDefault(i => i.Code == code);

    [Fact]
    public void HealthyStudent_HasNoIndicators_AndLowRisk()
    {
        var a = Assess(new[] { E(1, "A", "AA"), E(2, "B", "AA") });

        Assert.Empty(a.Indicators);
        Assert.Equal(0, a.Score);
        Assert.Equal(RiskLevel.Dusuk, a.Level);
    }

    [Fact]
    public void NewStudentWithoutHistory_IsNotFlaggedForGpa()
    {
        var a = Assess(Array.Empty<ObisTranscriptEntry>(), term: 1);

        Assert.Empty(a.Indicators);
    }

    [Fact]
    public void LowGpa_BelowThreshold_Scores35()
    {
        var a = Assess(new[] { E(1, "A", "DD"), E(1, "B", "CC") }); // 1.5

        var i = Find(a, "LOW_GPA")!;
        Assert.Equal(35, i.Points);
        Assert.Contains("1,50", i.Explanation);
        Assert.Null(Find(a, "WATCH_GPA"));
    }

    [Fact]
    public void Gpa_ExactlyAtLowThreshold_IsWatchNotLow()
    {
        var a = Assess(new[] { E(1, "A", "CC") }); // 2.00

        Assert.Null(Find(a, "LOW_GPA"));
        Assert.Equal(15, Find(a, "WATCH_GPA")!.Points);
    }

    [Fact]
    public void Gpa_AtWatchThreshold_IsClean()
    {
        var a = Assess(new[] { E(1, "A", "CC"), E(1, "B", "BB") }); // 2.5

        Assert.Null(Find(a, "WATCH_GPA"));
    }

    [Fact]
    public void GpaDrop_SevereAtExactlyOnePoint()
    {
        var a = Assess(new[] { E(1, "A", "AA"), E(2, "B", "BB") }); // 4.0 -> 3.0

        Assert.Equal(30, Find(a, "GPA_DROP")!.Points);
    }

    [Fact]
    public void GpaDrop_HalfPoint_IsWarning()
    {
        var a = Assess(new[] { E(1, "A", "AA"), E(2, "B", "BA") }); // 4.0 -> 3.5

        var i = Find(a, "GPA_DROP")!;
        Assert.Equal(20, i.Points);
        Assert.Contains("1. dönem", i.Explanation);
        Assert.Contains("2. dönem", i.Explanation);
    }

    [Fact]
    public void GpaDrop_SmallDropOrRise_IsIgnored()
    {
        Assert.Null(Find(Assess(new[] { E(1, "A", "AA"), E(2, "B", "AA") }), "GPA_DROP"));
        Assert.Null(Find(Assess(new[] { E(1, "A", "CC"), E(2, "B", "AA") }), "GPA_DROP"));
    }

    [Fact]
    public void GpaDrop_ComparesOnlyLastTwoTerms()
    {
        // 1. dönem çok düşük, sonraki ikisi sabit: düşüş yok.
        var a = Assess(new[] { E(1, "A", "DD"), E(2, "B", "AA"), E(3, "C", "AA") });

        Assert.Null(Find(a, "GPA_DROP"));
    }

    [Fact]
    public void GpaDrop_NeedsTwoTerms()
    {
        Assert.Null(Find(Assess(new[] { E(1, "A", "AA") }), "GPA_DROP"));
    }

    [Fact]
    public void RepeatedFailure_SameCourseFailedTwice_Scores15()
    {
        var a = Assess(new[] { E(1, "A", "FF"), E(2, "A", "FD"), E(3, "A", "AA"), E(3, "B", "AA") });

        Assert.Equal(15, Find(a, "REPEATED_FAIL")!.Points);
    }

    [Fact]
    public void RepeatedFailure_IsCappedAtMaximum()
    {
        var entries = new List<ObisTranscriptEntry>();
        foreach (var code in new[] { "A", "B", "C", "D" })
        {
            entries.Add(E(1, code, "FF"));
            entries.Add(E(2, code, "FF"));
        }

        Assert.Equal(30, Find(Assess(entries), "REPEATED_FAIL")!.Points);
    }

    [Fact]
    public void SingleFailure_IsNotRepeatedFailure()
    {
        Assert.Null(Find(Assess(new[] { E(1, "A", "FF"), E(2, "A", "AA") }), "REPEATED_FAIL"));
    }

    [Fact]
    public void ConsecutiveTermsWithFailures_Scores10()
    {
        var a = Assess(new[] { E(1, "A", "AA"), E(2, "B", "FF"), E(3, "C", "FD") });

        Assert.Equal(10, Find(a, "CONSECUTIVE_FAIL_TERMS")!.Points);
    }

    [Fact]
    public void FailuresInNonAdjacentTerms_AreNotConsecutive()
    {
        var a = Assess(new[] { E(1, "A", "FF"), E(2, "B", "AA"), E(3, "C", "AA") });

        Assert.Null(Find(a, "CONSECUTIVE_FAIL_TERMS"));
    }

    [Fact]
    public void Absence_BelowWarning_NoIndicator()
    {
        // 14 dersin 2'si = %14
        Assert.Null(Find(Assess(new[] { E(1, "A", "AA") }, new[] { A(5, "A", 12) }), "ABSENCE_NEAR"));
    }

    [Fact]
    public void Absence_NearLimit_ScoresWarningPoints()
    {
        // 14 dersin 4'ü kaçırıldı = %29
        var a = Assess(new[] { E(1, "A", "AA") }, new[] { A(5, "BLM201", 10) });

        var i = Find(a, "ABSENCE_NEAR")!;
        Assert.Equal(20, i.Points);
        Assert.Contains("BLM201", i.Explanation);
        Assert.Null(Find(a, "ABSENCE_LIMIT"));
    }

    [Fact]
    public void Absence_AtLimit_ScoresLimitPoints_AndReplacesWarning()
    {
        // 10 üzerinden 3 devamsızlık = tam %30
        var a = Assess(new[] { E(1, "A", "AA") }, new[] { A(5, "A", 7, 10), A(5, "B", 8, 10) });

        Assert.Equal(40, Find(a, "ABSENCE_LIMIT")!.Points);
        Assert.Null(Find(a, "ABSENCE_NEAR"));
    }

    [Fact]
    public void Absence_OnlyCurrentTermCounts()
    {
        var a = Assess(new[] { E(1, "A", "AA") }, new[] { A(4, "A", 5) }, term: 5);

        Assert.Empty(a.Indicators);
    }

    [Theory]
    [InlineData(8, 0, null)]
    [InlineData(9, 10, "OVER_NORMAL_DURATION")]
    [InlineData(10, 15, "OVER_NORMAL_DURATION")]
    [InlineData(12, 25, "OVER_NORMAL_DURATION")]
    [InlineData(13, 40, "MAX_DURATION")]
    [InlineData(14, 40, "MAX_DURATION")]
    public void Duration_Points(int term, int expectedPoints, string? code)
    {
        var a = Assess(new[] { E(1, "A", "AA") }, term: term);

        var duration = a.Indicators.FirstOrDefault(i => i.Code is "OVER_NORMAL_DURATION" or "MAX_DURATION");
        Assert.Equal(code, duration?.Code);
        Assert.Equal(expectedPoints, duration?.Points ?? 0);
    }

    [Fact]
    public void ScoreIsCappedAt100_AndLevelsFollowThresholds()
    {
        var entries = new List<ObisTranscriptEntry>();
        foreach (var code in new[] { "A", "B", "C", "D" })
        {
            entries.Add(E(1, code, "FF"));
            entries.Add(E(2, code, "FF"));
            entries.Add(E(3, code, "FF"));
        }

        var a = Assess(entries, new[] { A(14, "A", 5) }, term: 14);

        Assert.Equal(100, a.Score);
        Assert.Equal(RiskLevel.Yuksek, a.Level);
    }

    [Theory]
    [InlineData(29, RiskLevel.Dusuk)]
    [InlineData(30, RiskLevel.Orta)]
    [InlineData(59, RiskLevel.Orta)]
    [InlineData(60, RiskLevel.Yuksek)]
    public void RiskLevel_Boundaries(int lowGpaPoints, RiskLevel expected)
    {
        var o = new RiskOptions { LowGpaPoints = lowGpaPoints };

        var a = Assess(new[] { E(1, "A", "DD") }, o: o);

        Assert.Equal(expected, a.Level);
    }

    [Fact]
    public void Thresholds_AreConfigurable()
    {
        var o = new RiskOptions { LowGpaThreshold = 3.0, WatchGpaThreshold = 3.0 };

        var a = Assess(new[] { E(1, "A", "BB") }, o: o); // 3.0 -> artık sınırda değil

        Assert.Null(Find(a, "LOW_GPA"));
        var b = Assess(new[] { E(1, "A", "CB") }, o: o); // 2.5 < 3.0
        Assert.NotNull(Find(b, "LOW_GPA"));
    }
}

public class GraduationAnalyzerTests
{
    private static readonly RiskOptions O = new();

    private static readonly ObisStudent Student = new("1", "Test", "Öğrenci", 4, "BLM", "ADV1", 0, "Aktif");

    private static ObisCourse C(string code, int term, bool mandatory, int ects = 5, int credit = 3) =>
        new(code, $"Ders {code}", credit, ects, term, mandatory, new List<string>());

    private static ObisTranscriptEntry P(int term, string code, string grade = "BB", int ects = 5, int credit = 3) =>
        new("1", term, code, $"Ders {code}", credit, ects, grade, grade is not ("FF" or "FD"));

    private static ObisRegistration R(string code) => new("1", 8, code, "A", "Beklemede");

    // 3 zorunlu (15 AKTS) + 2 seçmeli (3'er kredi): 15 + 10 = 25 AKTS, 6 seçmeli kredi, 20 gün staj.
    private static readonly ObisCurriculum Curriculum = new("BLM", new List<string> { "M1", "M2", "M3" }, 6, 25, 20);
    private static readonly List<ObisCourse> Courses = new()
    {
        C("M1", 1, true), C("M2", 2, true), C("M3", 3, true), C("E1", 5, false), C("E2", 6, false), C("E3", 7, false),
    };

    private static GraduationAnalysis Analyze(
        IEnumerable<ObisTranscriptEntry> transcript, IEnumerable<ObisRegistration>? current = null, int internship = 20, RiskOptions? o = null) =>
        GraduationAnalyzer.Analyze(Student, transcript.ToList(), (current ?? Array.Empty<ObisRegistration>()).ToList(), Courses, Curriculum, internship, 8, o ?? O);

    private static IEnumerable<ObisTranscriptEntry> Complete() => new[]
    {
        P(1, "M1"), P(2, "M2"), P(3, "M3"), P(5, "E1"), P(6, "E2"),
    };

    [Fact]
    public void EverythingSatisfied_CanGraduate()
    {
        var g = Analyze(Complete());

        Assert.True(g.CanGraduate);
        Assert.Empty(g.Blockers);
        Assert.Equal(0, g.EstimatedRemainingTerms);
        Assert.Equal(25, g.EarnedEcts);
        Assert.Equal(100, g.EctsProgressPercent);
        Assert.Equal(6, g.ElectiveEarnedCredits);
        Assert.Equal(3, g.MandatoryPassed);
        Assert.Empty(g.MissingMandatory);
    }

    [Fact]
    public void MissingMandatory_StatusDistinguishesFailedNeverTakenAndInProgress()
    {
        var transcript = new[] { P(1, "M1"), P(2, "M2", "FF"), P(5, "E1"), P(6, "E2") };

        var g = Analyze(transcript, new[] { R("M3") });

        Assert.False(g.CanGraduate);
        Assert.Equal("Failed", g.MissingMandatory.Single(m => m.CourseCode == "M2").Status);
        Assert.Equal("InProgress", g.MissingMandatory.Single(m => m.CourseCode == "M3").Status);
        Assert.Contains(g.Blockers, b => b.Contains("2 zorunlu ders") && b.Contains("1 tanesi bu dönem"));
    }

    [Fact]
    public void MissingMandatory_NeverTaken()
    {
        var g = Analyze(new[] { P(1, "M1"), P(5, "E1"), P(6, "E2") });

        Assert.Equal(new[] { "NotTaken", "NotTaken" }, g.MissingMandatory.Select(m => m.Status));
        Assert.Equal(new[] { "M2", "M3" }, g.MissingMandatory.Select(m => m.CourseCode));
    }

    [Fact]
    public void FailedThenPassed_CountsAsPassedOnce()
    {
        var transcript = new[] { P(1, "M1", "FF"), P(2, "M1", "CC"), P(2, "M2"), P(3, "M3"), P(5, "E1"), P(6, "E2") };

        var g = Analyze(transcript);

        Assert.True(g.CanGraduate);
        Assert.Equal(25, g.EarnedEcts); // M1 bir kez sayılır
    }

    [Fact]
    public void ElectiveCredits_CountOnlyPassedNonMandatory()
    {
        var transcript = new[] { P(1, "M1"), P(2, "M2"), P(3, "M3"), P(5, "E1"), P(6, "E2", "FF") };

        var g = Analyze(transcript);

        Assert.Equal(3, g.ElectiveEarnedCredits);
        Assert.Equal("E1", Assert.Single(g.PassedElectives).CourseCode);
        Assert.Contains(g.Blockers, b => b.Contains("Seçmeli ders kredisi eksik: 3 / 6"));
    }

    [Fact]
    public void TotalEcts_ShortfallIsReported_AndProgressIsProportional()
    {
        var g = Analyze(new[] { P(1, "M1"), P(2, "M2") });

        Assert.Equal(10, g.EarnedEcts);
        Assert.Equal(40, g.EctsProgressPercent);
        Assert.Contains(g.Blockers, b => b.Contains("Toplam AKTS eksik: 10 / 25"));
    }

    [Fact]
    public void EctsProgress_IsCappedAt100()
    {
        var transcript = Complete().Append(P(7, "E3")).ToList(); // 30 AKTS > 25

        var g = Analyze(transcript);

        Assert.Equal(30, g.EarnedEcts);
        Assert.Equal(100, g.EctsProgressPercent);
    }

    [Fact]
    public void InternshipShortfall_IsABlocker()
    {
        var g = Analyze(Complete(), internship: 10);

        Assert.False(g.CanGraduate);
        Assert.Contains(g.Blockers, b => b.Contains("Staj eksik: 10 / 20 gün"));
        Assert.Equal(1, g.EstimatedRemainingTerms); // yalnızca staj: en az 1 dönem
    }

    [Fact]
    public void LowGpa_IsABlocker()
    {
        var transcript = new[] { P(1, "M1", "DD"), P(2, "M2", "DD"), P(3, "M3", "DD"), P(5, "E1", "DD"), P(6, "E2", "DD") };

        var g = Analyze(transcript);

        Assert.False(g.GpaOk);
        Assert.False(g.CanGraduate);
        Assert.Contains(g.Blockers, b => b.Contains("GANO 1,00") && b.Contains("2,00"));
    }

    [Fact]
    public void GpaExactlyAtMinimum_IsOk()
    {
        var transcript = new[] { P(1, "M1", "CC"), P(2, "M2", "CC"), P(3, "M3", "CC"), P(5, "E1", "CC"), P(6, "E2", "CC") };

        Assert.True(Analyze(transcript).GpaOk);
    }

    [Fact]
    public void EmptyTranscript_CannotGraduate()
    {
        var g = Analyze(Array.Empty<ObisTranscriptEntry>());

        Assert.False(g.CanGraduate);
        Assert.False(g.GpaOk);
        Assert.Equal(3, g.MissingMandatory.Count);
    }

    [Fact]
    public void Projection_AssumesCurrentTermCoursesArePassed()
    {
        // M3 ve E2 bu dönem alınıyor: geçilirse tüm koşullar sağlanır.
        var transcript = new[] { P(1, "M1"), P(2, "M2"), P(5, "E1") };

        var g = Analyze(transcript, new[] { R("M3"), R("E2") });

        Assert.False(g.CanGraduate);
        Assert.True(g.ProjectedCanGraduate);
        Assert.Empty(g.ProjectedBlockers);
    }

    [Fact]
    public void Projection_StillBlockedByNotRegisteredCourses()
    {
        var transcript = new[] { P(1, "M1"), P(5, "E1"), P(6, "E2") };

        var g = Analyze(transcript, new[] { R("M3") }); // M2 hiç alınmıyor

        Assert.False(g.ProjectedCanGraduate);
        Assert.Contains(g.ProjectedBlockers, b => b.Contains("1 zorunlu ders tamamlanmadı"));
    }

    [Fact]
    public void Projection_DoesNotFixInternshipOrGpa()
    {
        var g = Analyze(Complete(), new[] { R("E3") }, internship: 0);

        Assert.False(g.ProjectedCanGraduate);
        Assert.Contains(g.ProjectedBlockers, b => b.Contains("Staj eksik"));
    }

    [Fact]
    public void RemainingTerms_AreEstimatedFromMissingEcts()
    {
        var o = new RiskOptions { TypicalTermEcts = 10 };

        var g = Analyze(new[] { P(1, "M1") }, o: o); // 25 - 5 = 20 AKTS kaldı, dönem başı 10

        Assert.Equal(2, g.EstimatedRemainingTerms);
    }
}
