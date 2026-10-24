using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Advisor.Tests;

public class AnalyticsApiTests : IClassFixture<SeedApiFactory>
{
    private readonly SeedApiFactory _factory;

    public AnalyticsApiTests(SeedApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Anonymous_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/analytics/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/analytics/students/20241011/graduation")).StatusCode);
    }

    [Fact]
    public async Task Student_CannotSeeDashboardRiskListOrOwnRisk()
    {
        var ogr = await LoginAs("ogrenci2"); // 20241011

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/analytics/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/analytics/risk")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/analytics/students/20241011/risk")).StatusCode);
    }

    [Fact]
    public async Task Student_CanSeeOwnGraduationAnalysis_ButNotOthers()
    {
        var ogr = await LoginAs("ogrenci2");

        var res = await ogr.GetAsync("/api/analytics/students/20241011/graduation");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var g = (await res.Content.ReadFromJsonAsync<GraduationAnalysis>(TestJson.Options))!;
        Assert.Equal("20241011", g.StudentNo);
        Assert.Equal(40, g.InternshipRequiredDays);

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/analytics/students/20251006/graduation")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_AdvisorSeesOnlyOwnStudents_AdminSeesAll()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");

        var d1 = (await adv1.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!;
        var d2 = (await adv2.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!;
        var da = (await admin.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!;

        Assert.Equal(all.Count(s => s.AdvisorId == "ADV1"), d1.StudentCount);
        Assert.Equal(all.Count(s => s.AdvisorId == "ADV2"), d2.StudentCount);
        Assert.Equal(all.Count, da.StudentCount);
        Assert.Equal(da.StudentCount, da.Risk.Low + da.Risk.Medium + da.Risk.High);
    }

    [Fact]
    public async Task RiskList_AdvisorSeesOnlyOwnStudents_SortedByScore()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv1Numbers = all.Where(s => s.AdvisorId == "ADV1").Select(s => s.StudentNo).ToHashSet();
        var adv1 = await LoginAs("danisman1");

        var rows = (await adv1.GetFromJsonAsync<List<RiskRow>>("/api/analytics/risk", TestJson.Options))!;

        Assert.Equal(adv1Numbers.Count, rows.Count);
        Assert.All(rows, r => Assert.Contains(r.StudentNo, adv1Numbers));
        Assert.Equal(rows.OrderByDescending(r => r.Score).Select(r => r.Score), rows.Select(r => r.Score));
        Assert.Equal(RiskLevel.Yuksek, rows[0].Level);
    }

    [Fact]
    public async Task StudentRisk_OnlyForOwnAdvisorOrAdmin()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");

        var res = await adv1.GetAsync("/api/analytics/students/20251007/risk");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = (await res.Content.ReadFromJsonAsync<RiskDetail>(TestJson.Options))!;
        Assert.Equal(RiskLevel.Yuksek, detail.Level);
        Assert.All(detail.Indicators, i => Assert.False(string.IsNullOrWhiteSpace(i.Explanation)));

        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync("/api/analytics/students/20251007/risk")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync("/api/analytics/students/20251007/graduation")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/analytics/students/20251007/risk")).StatusCode);
    }

    [Fact]
    public async Task UnknownStudent_AdminGets404WithTurkishMessage()
    {
        var admin = await LoginAs("admin");

        var res = await admin.GetAsync("/api/analytics/students/99999999/graduation");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("Öğrenci bulunamadı.", (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"]);
    }
}
