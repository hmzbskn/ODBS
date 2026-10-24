using Advisor.Application.Analytics;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;

namespace Advisor.Tests;

/// <summary>Gerçek seed verisiyle risk ve mezuniyet analizi: bilinçli uç durumlar yakalanmalı.</summary>
public class AnalyticsSeedTests
{
    private readonly AnalyticsService _service = new(new SeedObisClient(), new RiskOptions());

    private async Task<RiskDetail> Risk(string no) => await _service.GetRiskAsync(no);

    private static bool Has(RiskDetail r, string code) => r.Indicators.Any(i => i.Code == code);

    [Fact]
    public async Task Edge07_GpaDrop_IsDetected_AsSevere()
    {
        var r = await Risk("20241013"); // dönem 3 AA ortalama -> dönem 4 DD ortalama

        var i = r.Indicators.Single(x => x.Code == "GPA_DROP");
        Assert.Equal(30, i.Points);
        Assert.Equal(RiskLevel.Orta, r.Level);
    }

    [Fact]
    public async Task Edge08_BeyondNormalDuration_IsDetected()
    {
        var r = await Risk("20231018"); // 9. dönem

        Assert.Equal(9, r.Term);
        Assert.True(Has(r, "OVER_NORMAL_DURATION"));
    }

    [Fact]
    public async Task Edge10_AbsenceNearLimit_IsDetected()
    {
        var r = await Risk("20251008");

        var i = r.Indicators.Single(x => x.Code == "ABSENCE_NEAR");
        Assert.Contains("%29", i.Explanation);
    }

    [Fact]
    public async Task Edge04_IsTheHighRiskDemoStudent_WithSeveralSignals()
    {
        var r = await Risk("20251007");

        Assert.Equal(RiskLevel.Yuksek, r.Level);
        Assert.True(Has(r, "LOW_GPA"));
        Assert.True(Has(r, "CONSECUTIVE_FAIL_TERMS"));
        Assert.True(Has(r, "ABSENCE_LIMIT"));
        Assert.True(r.Score >= 60);
    }

    [Fact]
    public async Task Edge09_AlmostGraduated_WillGraduateIfCurrentTermIsPassed()
    {
        var g = await _service.GetGraduationAsync("20231019");

        Assert.False(g.CanGraduate);
        Assert.True(g.ProjectedCanGraduate);
        Assert.All(g.MissingMandatory, m => Assert.Equal("InProgress", m.Status));
        Assert.Single(g.MissingMandatory); // kalan 2 dersten biri zorunlu, biri seçmeli
        Assert.Equal("GIR101", g.MissingMandatory[0].CourseCode);
        Assert.Equal(40, g.InternshipDays);
        Assert.Equal(1, g.EstimatedRemainingTerms);
    }

    [Fact]
    public async Task Edge09_IsCountedAsGraduationCandidate()
    {
        var students = await new SeedObisClient().GetStudentsAsync();

        var d = await _service.GetDashboardAsync(students);

        Assert.True(d.GraduationCandidates >= 1);
    }

    [Fact]
    public async Task FirstYearStudents_AreNotFlagged()
    {
        var students = (await new SeedObisClient().GetStudentsAsync()).Where(s => s.ClassYear == 1).ToList();

        var rows = await _service.GetRiskListAsync(students);

        Assert.All(rows, r => Assert.Equal(RiskLevel.Dusuk, r.Level));
        Assert.All(rows, r => Assert.Equal(0, r.Score));
    }

    [Fact]
    public async Task RiskList_IsSortedByScoreDescending()
    {
        var students = await new SeedObisClient().GetStudentsAsync();

        var rows = await _service.GetRiskListAsync(students);

        Assert.Equal(rows.OrderByDescending(r => r.Score).Select(r => r.Score), rows.Select(r => r.Score));
        Assert.Equal("20251007", rows[0].StudentNo);
    }

    [Fact]
    public async Task Dashboard_AggregatesAreConsistent()
    {
        var students = await new SeedObisClient().GetStudentsAsync();

        var d = await _service.GetDashboardAsync(students);

        Assert.Equal(63, d.StudentCount);
        Assert.Equal(63, d.Risk.Low + d.Risk.Medium + d.Risk.High);
        Assert.True(d.Risk.High >= 1);
        Assert.InRange(d.AverageGpa, 2.0, 4.0);
        Assert.Equal(d.GpaTrend.Select(t => t.Term).OrderBy(t => t), d.GpaTrend.Select(t => t.Term));
        Assert.All(d.GpaTrend, t => Assert.InRange(t.AverageGpa, 0, 4));
        Assert.All(d.CourseSuccess, c => Assert.InRange(c.SuccessRate, 0, 100));
        Assert.Equal(d.CourseSuccess.OrderBy(c => c.SuccessRate).Select(c => c.CourseCode), d.CourseSuccess.Select(c => c.CourseCode));
        Assert.All(d.CourseSuccess, c => Assert.True(c.Attempts >= 5));
    }

    [Fact]
    public async Task Dashboard_ForEmptyScope_IsAllZero()
    {
        var d = await _service.GetDashboardAsync(new List<ObisStudent>());

        Assert.Equal(0, d.StudentCount);
        Assert.Equal(0, d.AverageGpa);
        Assert.Empty(d.GpaTrend);
    }

    [Fact]
    public async Task UnknownStudent_Throws404()
    {
        var ex = await Assert.ThrowsAsync<Advisor.Application.Common.AppException>(() => _service.GetRiskAsync("99999999"));
        Assert.Equal(404, ex.StatusCode);
    }
}
