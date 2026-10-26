using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Management;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Tests;

/// <summary>Bölüm başkanı / dekan rolleri, danışman atama ve danışman bazlı analizler.
/// Her test kendi öğrencilerini kullanır (fixture sınıf içinde paylaşılır, atamalar kalıcıdır).</summary>
public class ManagementApiTests : IClassFixture<SeedApiFactory>
{
    private readonly SeedApiFactory _factory;

    public ManagementApiTests(SeedApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static async Task<List<string>> StudentsOf(string advisorId) =>
        (await new SeedObisClient().GetStudentsAsync()).Where(s => s.AdvisorId == advisorId).Select(s => s.StudentNo).OrderBy(n => n).ToList();

    [Theory]
    [InlineData("danisman1")]
    [InlineData("ogrenci1")]
    public async Task Management_IsForbiddenForAdvisorAndStudent(string username)
    {
        var client = await LoginAs(username);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/management/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/management/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new() { "20241011" }))).StatusCode);
    }

    [Fact]
    public async Task Management_RequiresLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/management/overview")).StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("bolumbaskani1")]
    [InlineData("dekan1")]
    public async Task Overview_ListsEveryAdvisorWithConsistentTotals(string username)
    {
        var client = await LoginAs(username);

        var overview = (await client.GetFromJsonAsync<ManagementOverview>("/api/management/overview", TestJson.Options))!;

        Assert.Equal(4, overview.Advisors.Count);
        Assert.Equal(overview.Total.StudentCount, overview.Advisors.Sum(a => a.StudentCount));
        Assert.Equal(overview.Total.Risk.High, overview.Advisors.Sum(a => a.Risk.High));
        foreach (var row in overview.Advisors)
            Assert.Equal(row.StudentCount, row.Risk.Low + row.Risk.Medium + row.Risk.High);
    }

    [Fact]
    public async Task AdvisorDashboard_MatchesWhatTheAdvisorSeesAboutOwnStudents()
    {
        var adv = await LoginAs("danisman1");
        var dean = await LoginAs("dekan1");

        var own = (await adv.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!;
        var viaDean = (await dean.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard?advisorId=ADV1", TestJson.Options))!;
        var viaManagement = (await dean.GetFromJsonAsync<DashboardResponse>("/api/management/advisors/ADV1/dashboard", TestJson.Options))!;

        Assert.Equal(own.StudentCount, viaDean.StudentCount);
        Assert.Equal(own.Risk, viaDean.Risk);
        Assert.Equal(own.AverageGpa, viaDean.AverageGpa);
        Assert.Equal(own.StudentCount, viaManagement.StudentCount);
        Assert.Equal(own.GraduationCandidates, viaManagement.GraduationCandidates);
    }

    [Fact]
    public async Task Advisor_CannotWidenScopeWithAdvisorIdQuery()
    {
        var adv = await LoginAs("danisman1");
        var own = (await StudentsOf("ADV1")).Count;

        var d = (await adv.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard?advisorId=ADV2", TestJson.Options))!;

        Assert.Equal(own, d.StudentCount);
    }

    [Fact]
    public async Task Dean_CanSeeRiskListFilteredByAdvisor_ButNotRegistrationsOrSurveys()
    {
        var dean = await LoginAs("dekan1");

        var rows = (await dean.GetFromJsonAsync<List<RiskRow>>("/api/analytics/risk?advisorId=ADV3", TestJson.Options))!;
        // Diğer testler atama yapabildiği için beklenen sayı, aynı andaki atama listesinden alınır.
        var assigned = (await dean.GetFromJsonAsync<List<ManagedStudent>>("/api/management/students?advisorId=ADV3", TestJson.Options))!;
        Assert.Equal(assigned.Select(s => s.StudentNo).Order(), rows.Select(r => r.StudentNo).Order());

        Assert.Equal(HttpStatusCode.Forbidden, (await dean.GetAsync("/api/registrations/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dean.GetAsync("/api/surveys")).StatusCode);
    }

    [Fact]
    public async Task Assign_MovesStudentToNewAdvisor_AndAccessFollows()
    {
        var student = (await StudentsOf("ADV1")).First();
        var head = await LoginAs("bolumbaskani1");
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");

        Assert.Equal(HttpStatusCode.OK, (await adv1.GetAsync($"/api/analytics/students/{student}/risk")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/analytics/students/{student}/risk")).StatusCode);
        var before1 = (await adv1.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!.StudentCount;
        var before2 = (await adv2.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!.StudentCount;

        var res = await head.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new() { student }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = (await res.Content.ReadFromJsonAsync<AssignResult>(TestJson.Options))!;
        Assert.Equal(new AssignResult(1, 0, 0), result);

        Assert.Equal(HttpStatusCode.Forbidden, (await adv1.GetAsync($"/api/analytics/students/{student}/risk")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adv2.GetAsync($"/api/analytics/students/{student}/risk")).StatusCode);
        Assert.Equal(before1 - 1, (await adv1.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!.StudentCount);
        Assert.Equal(before2 + 1, (await adv2.GetFromJsonAsync<DashboardResponse>("/api/analytics/dashboard", TestJson.Options))!.StudentCount);

        var listed = (await head.GetFromJsonAsync<List<ManagedStudent>>("/api/management/students?advisorId=ADV2", TestJson.Options))!
            .Single(s => s.StudentNo == student);
        Assert.True(listed.IsReassigned);
        Assert.Equal("ADV1", listed.ObisAdvisorId);
        Assert.Equal("ADV2", listed.AdvisorId);

        var overview = (await head.GetFromJsonAsync<ManagementOverview>("/api/management/overview", TestJson.Options))!;
        Assert.Equal(before2 + 1, overview.Advisors.Single(a => a.AdvisorId == "ADV2").StudentCount);
    }

    [Fact]
    public async Task Assign_BackToObisAdvisor_RemovesOverride()
    {
        var student = (await StudentsOf("ADV2")).First();
        var dean = await LoginAs("dekan1");

        await dean.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV3", new() { student }));
        var back = await dean.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new() { student }));

        Assert.Equal(new AssignResult(0, 0, 1), await back.Content.ReadFromJsonAsync<AssignResult>(TestJson.Options));
        var row = (await dean.GetFromJsonAsync<List<ManagedStudent>>("/api/management/students?advisorId=ADV2", TestJson.Options))!
            .Single(s => s.StudentNo == student);
        Assert.False(row.IsReassigned);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        Assert.DoesNotContain(db.AdvisorAssignments, a => a.StudentNo == student);
    }

    [Fact]
    public async Task Assign_SameAdvisorAgain_IsUnchanged_AndRecordsWhoAssigned()
    {
        var student = (await StudentsOf("ADV3")).First();
        var admin = await LoginAs("admin");

        await admin.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV1", new() { student }));
        var again = await admin.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV1", new() { student }));

        Assert.Equal(new AssignResult(0, 1, 0), await again.Content.ReadFromJsonAsync<AssignResult>(TestJson.Options));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var row = db.AdvisorAssignments.Single(a => a.StudentNo == student);
        Assert.Equal("ADV3", row.PreviousAdvisorId);
        Assert.Equal("Sistem Yöneticisi", row.AssignedByName);
    }

    [Fact]
    public async Task Assign_RejectsUnknownAdvisorEmptyListAndUnknownStudent_WithoutPartialApply()
    {
        var student = (await StudentsOf("ADV1"))[1];
        var admin = await LoginAs("admin");

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV99", new() { student }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new() { student, "99999999" }))).StatusCode);

        var row = (await admin.GetFromJsonAsync<List<ManagedStudent>>("/api/management/students?advisorId=ADV1", TestJson.Options))!
            .Single(s => s.StudentNo == student);
        Assert.False(row.IsReassigned);
    }

    [Fact]
    public async Task DepartmentHead_OnlySeesAndAssignsOwnDepartment()
    {
        const string username = "bolum-makine";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
            db.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(), Username = username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoDataSeeder.DemoPassword),
                DisplayName = "Makine Bölüm Başkanı", Role = UserRole.BolumBaskani, Department = "Makine Mühendisliği",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var head = await LoginAs(username);
        var student = (await StudentsOf("ADV1")).First();

        var overview = (await head.GetFromJsonAsync<ManagementOverview>("/api/management/overview", TestJson.Options))!;
        Assert.Equal(0, overview.Total.StudentCount);
        Assert.Empty(await head.GetFromJsonAsync<List<ManagedStudent>>("/api/management/students", TestJson.Options) ?? new());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await head.PostAsJsonAsync("/api/management/assignments", new AssignRequest("ADV2", new() { student }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/analytics/students/{student}/risk")).StatusCode);
    }
}
