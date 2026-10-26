using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Auth;
using Advisor.Application.Obis;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

/// <summary>OBİS'e gitmeyen, yalnızca danışman-öğrenci eşlemesini bilen sahte istemci.</summary>
internal class FakeObisClient : IObisClient
{
    private static readonly List<ObisStudent> Students = new()
    {
        new("20241011", "Buse", "Ateş", 2, "Bilgisayar Mühendisliği", "ADV1", 2.5, "Aktif"),
        new("20251006", "Selin", "Kılıç", 1, "Bilgisayar Mühendisliği", "ADV2", 2.0, "Aktif"),
    };

    public Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default) => Task.FromResult(Students);
    public Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default) =>
        Task.FromResult(Students.FirstOrDefault(s => s.StudentNo == studentNo));
    public Task<List<ObisTranscriptEntry>> GetTranscriptAsync(string studentNo, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisRegistration>> GetRegistrationsAsync(string studentNo, int? term = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisAttendance>> GetAttendanceAsync(string studentNo, int? term = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisCourse>> GetCoursesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisScheduleSlot>> GetScheduleAsync(int? term = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisExamSlot>> GetExamsAsync(int? term = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ObisCurriculum> GetCurriculumAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ObisInternship> GetInternshipAsync(string studentNo, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ObisAdvisor>> GetAdvisorsAsync(CancellationToken ct = default) => throw new NotSupportedException();
}

internal static class TestJson
{
    /// <summary>API enum'ları metin olarak döndürür.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}

public class AuthFactory : WebApplicationFactory<Program>
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
            services.AddScoped<IObisClient, FakeObisClient>();
        });
    }
}

public class AuthTests : IClassFixture<AuthFactory>
{
    private readonly AuthFactory _factory;

    public AuthTests(AuthFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> LoginAs(string username, string password = DemoDataSeeder.DemoPassword)
    {
        var client = Client();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var res = await Client().PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "yanlis"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var res = await Client().PostAsJsonAsync("/api/auth/login", new LoginRequest("yok", DemoDataSeeder.DemoPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var res = await Client().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsLinkedIdentity()
    {
        var client = await LoginAs("danisman1");
        var me = await client.GetFromJsonAsync<UserInfo>("/api/auth/me", TestJson.Options);
        Assert.Equal("ADV1", me!.LinkedAdvisorId);
    }

    [Fact]
    public async Task AdminEndpoint_OnlyAdmin()
    {
        Assert.Equal(HttpStatusCode.OK, (await (await LoginAs("admin")).GetAsync("/api/access/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await LoginAs("danisman1")).GetAsync("/api/access/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await LoginAs("ogrenci1")).GetAsync("/api/access/admin")).StatusCode);
    }

    [Fact]
    public async Task AdvisorArea_BlocksStudents()
    {
        Assert.Equal(HttpStatusCode.OK, (await (await LoginAs("danisman2")).GetAsync("/api/access/advisor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await LoginAs("ogrenci2")).GetAsync("/api/access/advisor")).StatusCode);
    }

    [Fact]
    public async Task Advisor_SeesOnlyOwnStudents()
    {
        // 20241011 -> ADV1, 20251006 -> ADV2
        var adv1 = await LoginAs("danisman1");
        Assert.Equal(HttpStatusCode.OK, (await adv1.GetAsync("/api/access/students/20241011")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv1.GetAsync("/api/access/students/20251006")).StatusCode);
    }

    [Fact]
    public async Task Student_SeesOnlyThemselves()
    {
        var ogr = await LoginAs("ogrenci2"); // 20241011
        Assert.Equal(HttpStatusCode.OK, (await ogr.GetAsync("/api/access/students/20241011")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/access/students/20251006")).StatusCode);
    }

    [Fact]
    public async Task Admin_SeesAnyStudent()
    {
        var admin = await LoginAs("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/access/students/20251006")).StatusCode);
    }

    [Fact]
    public async Task DemoSeeder_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var before = await db.Users.CountAsync();
        await DemoDataSeeder.SeedAsync(db);
        Assert.Equal(before, await db.Users.CountAsync());
        Assert.Equal(11, before);
    }
}
