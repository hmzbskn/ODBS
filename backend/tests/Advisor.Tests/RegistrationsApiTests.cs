using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Api.Controllers;
using Advisor.Application.Auth;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

/// <summary>Gerçek seed verisiyle (ağsız) çalışan API fabrikası.</summary>
public class SeedApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AdvisorDbContext>>();
            services.RemoveAll<AdvisorDbContext>();
            services.AddDbContext<AdvisorDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IObisClient>();
            services.AddSingleton<IObisClient, SeedObisClient>();
        });
    }
}

public class RegistrationsApiTests : IClassFixture<SeedApiFactory>
{
    private readonly SeedApiFactory _factory;

    public RegistrationsApiTests(SeedApiFactory factory) => _factory = factory;

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
    public async Task StudentList_RequiresLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/registrations/students")).StatusCode);
    }

    [Fact]
    public async Task StudentList_IsForbiddenForStudents()
    {
        var ogr = await LoginAs("ogrenci1");
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/registrations/students")).StatusCode);
    }

    [Fact]
    public async Task StudentList_AdvisorSeesOnlyOwnStudents_WithWarningCounts()
    {
        var adv = await LoginAs("danisman1");
        var list = await adv.GetFromJsonAsync<AdvisorStudentList>("/api/registrations/students", TestJson.Options);

        var all = await new SeedObisClient().GetStudentsAsync();
        Assert.Equal(all.Count(s => s.AdvisorId == "ADV1"), list!.Students.Count);

        var edge01 = list.Students.Single(s => s.StudentNo == "20241011");
        Assert.True(edge01.ErrorCount >= 1); // saat çakışması
        var edge06 = list.Students.Single(s => s.StudentNo == "20231017");
        Assert.True(edge06.ErrorCount >= 1); // AA ile geçilmiş ders tekrar seçilmiş
    }

    [Fact]
    public async Task StudentList_TermFilter_OnlyStudentsWithThatTerm()
    {
        var admin = await LoginAs("admin");
        var list = await admin.GetFromJsonAsync<AdvisorStudentList>("/api/registrations/students?term=1", TestJson.Options);

        Assert.NotEmpty(list!.Students);
        Assert.All(list.Students, s => Assert.Equal(1, s.Term));
    }

    [Fact]
    public async Task Detail_Edge01_MarksConflictingSlotsAndCourses()
    {
        var adv = await LoginAs("danisman1");
        var detail = await adv.GetFromJsonAsync<RegistrationDetail>("/api/registrations/20241011", TestJson.Options);

        Assert.Contains(detail!.Issues, i => i.RuleCode == "TIME_CONFLICT");
        var conflicting = detail.WeeklySchedule.Where(s => s.InConflict).Select(s => s.CourseCode).ToHashSet();
        Assert.Contains("3301353", conflicting);
        Assert.Contains("3301552", conflicting);
        Assert.Contains(detail.Courses, c => c.CourseCode == "3301353" && c.InConflict);
        Assert.Contains(detail.WeeklySchedule, s => !s.InConflict);
    }

    [Fact]
    public async Task Detail_WeeklySchedule_ColorsTakenSoftConflictAndNotTakenCourses()
    {
        var admin = await LoginAs("admin");

        // EDGE-02: Atatürk İlkeleri-1 (devam şartı yok) Mantık Devreleri-1 ile çakışır: ikisi de "soft", bulgu yok.
        var soft = await admin.GetFromJsonAsync<RegistrationDetail>("/api/registrations/20251006", TestJson.Options);
        Assert.Contains(soft!.WeeklySchedule, s => s.CourseCode == "3301154" && s.Status == "soft");
        Assert.Contains(soft.WeeklySchedule, s => s.CourseCode == "3301353" && s.Status == "soft");
        Assert.Contains(soft.WeeklySchedule, s => s.Status == "ok");
        Assert.DoesNotContain(soft.WeeklySchedule, s => s.Status == "conflict");

        // EDGE-05: çakışan dersler kırmızı; dönemin alınmayan dersleri ve Veri Bilimi'nin İÖ şubesi gri.
        var hard = await admin.GetFromJsonAsync<RegistrationDetail>("/api/registrations/20231016", TestJson.Options);
        Assert.Contains(hard!.WeeklySchedule, s => s.CourseCode == "3301753" && s.Section == "NÖ" && s.Status == "conflict");
        Assert.Contains(hard.WeeklySchedule, s => s.CourseCode == "3301753" && s.Section == "İÖ" && s.Status == "notTaken");
    }

    [Fact]
    public async Task Detail_AdvisorCannotSeeAnotherAdvisorsStudent()
    {
        var adv2 = await LoginAs("danisman2");
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync("/api/registrations/20241011")).StatusCode);
    }

    [Fact]
    public async Task Detail_StudentSeesOnlyOwn()
    {
        var ogr = await LoginAs("ogrenci2"); // 20241011
        Assert.Equal(HttpStatusCode.OK, (await ogr.GetAsync("/api/registrations/20241011")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/registrations/20251006")).StatusCode);
    }

    [Fact]
    public async Task Detail_UnknownStudent_AdminGets404()
    {
        var admin = await LoginAs("admin");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/registrations/99999999")).StatusCode);
    }

    [Fact]
    public async Task Review_RevisionRequiresNote()
    {
        var adv = await LoginAs("danisman1");
        var res = await adv.PostAsJsonAsync("/api/registrations/20241011/review",
            new { term = 5, decision = "RevisionRequested", note = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Review_PendingIsNotAValidDecision()
    {
        var adv = await LoginAs("danisman1");
        var res = await adv.PostAsJsonAsync("/api/registrations/20241011/review",
            new { term = 5, decision = "Pending" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Review_StudentCannotReview()
    {
        var ogr = await LoginAs("ogrenci2");
        var res = await ogr.PostAsJsonAsync("/api/registrations/20241011/review",
            new { term = 5, decision = "Approved" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Review_OtherAdvisorCannotReview()
    {
        var adv2 = await LoginAs("danisman2");
        var res = await adv2.PostAsJsonAsync("/api/registrations/20241011/review",
            new { term = 5, decision = "Approved" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Review_LatestDecisionWins_AndHistoryIsKept()
    {
        var adv = await LoginAs("danisman1");

        var first = await adv.PostAsJsonAsync("/api/registrations/20231016/review",
            new { term = 7, decision = "RevisionRequested", note = "Sınav çakışmasını düzeltin." });
        first.EnsureSuccessStatusCode();
        await Task.Delay(5);
        var second = await adv.PostAsJsonAsync("/api/registrations/20231016/review",
            new { term = 7, decision = "Approved" });
        second.EnsureSuccessStatusCode();

        var detail = await adv.GetFromJsonAsync<RegistrationDetail>("/api/registrations/20231016?term=7", TestJson.Options);
        Assert.Equal(RegistrationDecision.Approved, detail!.Decision);
        Assert.Equal(2, detail.Reviews.Count);
        Assert.Equal("ADV1", detail.Reviews[0].ReviewedByAdvisorId);

        var list = await adv.GetFromJsonAsync<AdvisorStudentList>("/api/registrations/students", TestJson.Options);
        Assert.Equal(RegistrationDecision.Approved, list!.Students.Single(s => s.StudentNo == "20231016").Decision);
    }

    [Fact]
    public async Task Review_TermWithoutRegistrations_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var res = await adv.PostAsJsonAsync("/api/registrations/20241011/review",
            new { term = 8, decision = "Approved" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
