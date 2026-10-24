using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Obis;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Advisor.Tests;

/// <summary>Mock OBİS uygulamasını bellek içinde, gerçek Minimal API + JSON seed ile ayağa kaldırır.</summary>
public class MockObisHost : WebApplicationFactory<Obis.MockApi.MockApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}

/// <summary>Ana uygulama + GERÇEK <see cref="HttpObisClient"/> + gerçek mock OBİS HTTP sözleşmesi.
/// Sahte istemci kullanılmadığı için JSON sözleşmesi (metin enum'lar, tarih/saat biçimleri) de test edilir.</summary>
public class FullStackFactory : WebApplicationFactory<Program>
{
    private readonly MockObisHost _mock = new();
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AdvisorDbContext>>();
            services.RemoveAll<AdvisorDbContext>();
            services.AddDbContext<AdvisorDbContext>(o => o.UseInMemoryDatabase(_dbName));

            var http = _mock.CreateClient();
            services.RemoveAll<IObisClient>();
            services.AddSingleton<IObisClient>(_ => new HttpObisClient(
                http, new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new ObisOptions { BaseUrl = "http://localhost", CacheSeconds = 600 })));

            services.RemoveAll<IFileStore>();
            services.AddSingleton<IFileStore, MemoryFileStore>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _mock.Dispose();
        base.Dispose(disposing);
    }
}

public class FullStackTests : IClassFixture<FullStackFactory>
{
    private readonly FullStackFactory _factory;

    public FullStackTests(FullStackFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static Task<T> Get<T>(HttpClient c, string url) => c.GetFromJsonAsync<T>(url, TestJson.Options)!;

    // ------------------------------------------------------------ OBİS HTTP sözleşmesi

    [Fact]
    public async Task ObisAdapter_ReadsEveryEndpoint_OverRealHttpContract()
    {
        var obis = _factory.Services.GetRequiredService<IObisClient>();

        var students = await obis.GetStudentsAsync();
        var courses = await obis.GetCoursesAsync();
        var schedule = await obis.GetScheduleAsync();
        var exams = await obis.GetExamsAsync();
        var curriculum = await obis.GetCurriculumAsync();

        Assert.Equal(63, students.Count);
        Assert.Equal(3, (await obis.GetAdvisorsAsync()).Count);
        Assert.Equal(52, courses.Count);
        Assert.Equal(42, curriculum.MandatoryCourseCodes.Count);
        Assert.Equal(212, curriculum.TotalEctsRequirement);
        // Metin olarak gelen enum'lar doğru çözülmeli (bu hata daha önce canlıda yakalanmıştı).
        Assert.All(schedule, s => Assert.True(s.Day is >= DayOfWeek.Monday and <= DayOfWeek.Friday));
        Assert.All(schedule, s => Assert.True(s.StartTime < s.EndTime));
        Assert.Contains(exams, e => e.Type == ObisExamType.Midterm);
        Assert.Contains(exams, e => e.Type == ObisExamType.Final);
        Assert.NotEmpty(await obis.GetTranscriptAsync("20241011"));
        Assert.NotEmpty(await obis.GetRegistrationsAsync("20241011", 5));
        Assert.NotEmpty(await obis.GetAttendanceAsync("20251008"));
        Assert.Equal(40, (await obis.GetInternshipAsync("20231019")).CompletedDays);
        Assert.Null(await obis.GetStudentAsync("99999999"));
    }

    // ------------------------------------------------------------ uç durumlar, API üzerinden

    [Fact]
    public async Task Edge01_TimeConflict_IsVisibleInTheWeeklySchedule()
    {
        var adv = await LoginAs("danisman1");

        var d = await Get<RegistrationDetail_>(adv, "/api/registrations/20241011");

        Assert.Contains(d.Issues, i => i.RuleCode == "TIME_CONFLICT");
        Assert.Contains(d.WeeklySchedule, s => s.CourseCode == "MAT101" && s.InConflict);
        Assert.Contains(d.WeeklySchedule, s => s.CourseCode == "BLM301" && s.InConflict);
    }

    [Theory]
    [InlineData("20251006", "PREREQUISITE")]
    [InlineData("20241012", "LOWER_SEMESTER_PRIORITY")]
    [InlineData("20251007", "CREDIT_LIMIT")]
    [InlineData("20231016", "EXAM_CONFLICT")]
    [InlineData("20231017", "RETAKE")]
    public async Task RegistrationEdgeCases_AreCaughtThroughTheApi(string studentNo, string rule)
    {
        var adv = await LoginAs("danisman1");

        var d = await Get<RegistrationDetail_>(adv, $"/api/registrations/{studentNo}");

        Assert.Contains(d.Issues, i => i.RuleCode == rule);
    }

    [Theory]
    [InlineData("20241013", "GPA_DROP")]
    [InlineData("20231018", "OVER_NORMAL_DURATION")]
    [InlineData("20251008", "ABSENCE_NEAR")]
    [InlineData("20251007", "ABSENCE_LIMIT")]
    [InlineData("20251007", "LOW_GPA")]
    public async Task RiskEdgeCases_AreCaughtThroughTheApi(string studentNo, string indicator)
    {
        var adv = await LoginAs("danisman1");

        var r = await Get<RiskDetail>(adv, $"/api/analytics/students/{studentNo}/risk");

        Assert.Contains(r.Indicators, i => i.Code == indicator);
    }

    [Fact]
    public async Task Edge09_AlmostGraduated_IsReportedWithProjection()
    {
        var adv = await LoginAs("danisman1");

        var g = await Get<GraduationAnalysis>(adv, "/api/analytics/students/20231019/graduation");

        Assert.False(g.CanGraduate);
        Assert.True(g.ProjectedCanGraduate);
        Assert.Equal(40, g.InternshipDays);
    }

    [Fact]
    public async Task EverySeedStudent_HasWorkingRegistrationRiskGraduationAndFile()
    {
        var admin = await LoginAs("admin");
        var students = await new SeedObisClient().GetStudentsAsync();
        var failures = new List<string>();

        foreach (var s in students)
        {
            foreach (var url in new[]
            {
                $"/api/registrations/{s.StudentNo}",
                $"/api/analytics/students/{s.StudentNo}/risk",
                $"/api/analytics/students/{s.StudentNo}/graduation",
                $"/api/student-files/{s.StudentNo}",
            })
            {
                var res = await admin.GetAsync(url);
                if (res.StatusCode != HttpStatusCode.OK) failures.Add($"{(int)res.StatusCode} {url}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task AdvisorLists_WorkAcrossAllModules()
    {
        var adv = await LoginAs("danisman1");

        Assert.NotEmpty((await Get<AdvisorStudentList_>(adv, "/api/registrations/students")).Students);
        Assert.NotEmpty(await Get<List<RiskRow>>(adv, "/api/analytics/risk"));
        Assert.True((await Get<DashboardResponse>(adv, "/api/analytics/dashboard")).StudentCount > 0);
        Assert.NotNull(await Get<List<SlotDto>>(adv, "/api/appointments/slots"));
        Assert.NotNull(await Get<List<AnnouncementSummary>>(adv, "/api/announcements"));
        Assert.NotNull(await Get<List<PetitionDto>>(adv, "/api/petitions"));
    }

    [Theory]
    [InlineData("ogrenci1", "20261001")]
    [InlineData("ogrenci2", "20241011")]
    [InlineData("ogrenci3", "20251006")]
    [InlineData("ogrenci4", "20231016")]
    public async Task EveryStudentDemoAccount_CanUseItsOwnScreens(string username, string studentNo)
    {
        var ogr = await LoginAs(username);

        Assert.Equal(studentNo, (await Get<RegistrationDetail_>(ogr, $"/api/registrations/{studentNo}")).StudentNo);
        Assert.Equal(studentNo, (await Get<GraduationAnalysis>(ogr, $"/api/analytics/students/{studentNo}/graduation")).StudentNo);
        Assert.NotNull(await Get<AvailabilityResponse>(ogr, "/api/appointments/availability"));
        Assert.NotNull(await Get<List<InboxItem>>(ogr, "/api/inbox"));
        Assert.NotNull(await Get<List<StudentSurveyItem_>>(ogr, "/api/surveys/mine"));
        Assert.NotNull(await Get<List<PetitionDto>>(ogr, "/api/petitions"));
    }

    // Yalnızca bu testin ihtiyaç duyduğu alanlara bakan hafif tipler (tam sözleşme ilgili testlerde).
    private record RegistrationDetail_(string StudentNo, List<RegistrationIssue> Issues, List<WeeklySlot_> WeeklySchedule);

    private record WeeklySlot_(string CourseCode, bool InConflict);

    private record AdvisorStudentList_(List<object> Students);

    private record StudentSurveyItem_(Guid Id);
}
