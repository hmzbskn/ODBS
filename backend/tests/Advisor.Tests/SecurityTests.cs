using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Advisor.Application.Auth;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

/// <summary>Ortam ve ayarları parametreli, seed verili API fabrikası (sertleştirme testleri için).</summary>
public class ConfigurableApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _settings;
    private readonly string _dbName = Guid.NewGuid().ToString();

    public ConfigurableApiFactory(string environment, Dictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _settings = settings ?? new();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        foreach (var (key, value) in _settings) builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AdvisorDbContext>>();
            services.RemoveAll<AdvisorDbContext>();
            services.AddDbContext<AdvisorDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<IObisClient>();
            services.AddSingleton<IObisClient, SeedObisClient>();
            services.RemoveAll<IFileStore>();
            services.AddSingleton<IFileStore, MemoryFileStore>();
        });
    }
}

/// <summary>Her endpoint'in kimlik doğrulama ve rol kapısı otomatik taranır: yeni bir endpoint eklenip
/// yetki düşünülmezse bu testler kırılır.</summary>
public class EndpointSecurityTests : IClassFixture<SeedApiFactory>
{
    private record EndpointInfo(string Method, string Pattern, string Url, bool AllowAnonymous, string[][] RoleSets);

    // Bilinçli olarak "giriş yapmış herkes" erişimli endpoint'ler: yetki servis/guard katmanında, role göre verilir.
    private static readonly HashSet<string> RoleAgnostic = new()
    {
        "GET api/auth/me",
        "GET api/registrations/{studentNo}",                  // IStudentAccessGuard
        "GET api/analytics/students/{studentNo}/graduation",   // IStudentAccessGuard
        "GET api/access/students/{studentNo}",                // IStudentAccessGuard (rol/sahiplik örneği)
        "GET api/appointments",                               // role göre kapsamlı liste
        "POST api/appointments/{id:guid}/cancel",             // servis: sahip öğrenci / ilgili danışman
        "GET api/petitions",                                  // role göre kapsamlı liste
        "GET api/petitions/{id:guid}/attachment",             // servis: IStudentAccessGuard
    };

    private readonly SeedApiFactory _factory;

    public EndpointSecurityTests(SeedApiFactory factory) => _factory = factory;

    private List<EndpointInfo> Endpoints()
    {
        var result = new List<EndpointInfo>();
        foreach (var e in _factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>())
        {
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods is null || e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is null) continue;

            var pattern = e.RoutePattern.RawText!;
            var url = "/" + Regex.Replace(pattern, @"\{(?<n>\w+)(:(?<c>[^}]+))?\}", m =>
                m.Groups["c"].Value == "guid" ? Guid.NewGuid().ToString() : m.Groups["n"].Value == "studentNo" ? "20241011" : "x");
            var roleSets = e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(a => (a.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(r => r.Length > 0).ToArray();
            foreach (var method in methods)
                result.Add(new EndpointInfo(method, pattern, url, e.Metadata.GetMetadata<IAllowAnonymous>() is not null, roleSets));
        }

        return result;
    }

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> Send(HttpClient c, EndpointInfo e) =>
        c.SendAsync(new HttpRequestMessage(new HttpMethod(e.Method), e.Url));

    [Fact]
    public void TheScanActuallyFindsTheEndpoints()
    {
        Assert.True(Endpoints().Count >= 49, $"Beklenenden az endpoint bulundu: {Endpoints().Count}");
    }

    [Fact]
    public void OnlyLogin_IsAnonymous()
    {
        var anonymous = Endpoints().Where(e => e.AllowAnonymous).Select(e => $"{e.Method} {e.Pattern}").ToList();

        Assert.Equal(new[] { "POST api/auth/login" }, anonymous);
    }

    [Fact]
    public async Task EveryOtherEndpoint_Rejects_AnonymousRequests_With401()
    {
        var anon = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var failures = new List<string>();

        foreach (var e in Endpoints().Where(e => !e.AllowAnonymous))
        {
            var res = await Send(anon, e);
            if (res.StatusCode != HttpStatusCode.Unauthorized) failures.Add($"{e.Method} {e.Url} -> {(int)res.StatusCode}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task RoleRestrictedEndpoints_RejectEveryOtherRole_With403()
    {
        var clients = new Dictionary<string, HttpClient>
        {
            ["Admin"] = await LoginAs("admin"),
            ["Danisman"] = await LoginAs("danisman1"),
            ["Ogrenci"] = await LoginAs("ogrenci1"),
        };
        var failures = new List<string>();
        var checkedEndpoints = 0;

        foreach (var e in Endpoints().Where(e => e.RoleSets.Length > 0))
        {
            checkedEndpoints++;
            var allowed = e.RoleSets.Aggregate((IEnumerable<string>)clients.Keys, (acc, set) => acc.Intersect(set)).ToHashSet();
            foreach (var (role, client) in clients.Where(c => !allowed.Contains(c.Key)))
            {
                var res = await Send(client, e);
                if (res.StatusCode != HttpStatusCode.Forbidden) failures.Add($"{role}: {e.Method} {e.Url} -> {(int)res.StatusCode}");
            }
        }

        Assert.True(checkedEndpoints >= 40, $"Rol kısıtlı endpoint sayısı beklenenden az: {checkedEndpoints}");
        Assert.Empty(failures);
    }

    [Fact]
    public void EndpointsWithoutRoleRestriction_AreAnExplicitReviewedList()
    {
        // Rol kısıtı olmayan her endpoint bilinçli olarak onaylanmış olmalı (servis/guard katmanında yetkilendirilir).
        var unrestricted = Endpoints()
            .Where(e => !e.AllowAnonymous && e.RoleSets.Length == 0)
            .Select(e => $"{e.Method} {e.Pattern}")
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(RoleAgnostic.OrderBy(x => x), unrestricted);
    }
}

public class SwaggerTests
{
    [Fact]
    public async Task Swagger_IsServed_InDevelopment_AndDocumentsEveryController()
    {
        using var factory = new ConfigurableApiFactory("Development");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.True(paths.Count >= 44, $"Swagger yol sayısı az: {paths.Count}");
        foreach (var expected in new[]
        {
            "/api/auth/login", "/api/registrations/{studentNo}", "/api/surveys/{id}/import", "/api/analytics/dashboard",
            "/api/appointments/book", "/api/announcements/preview", "/api/petitions", "/api/petitions/{id}/attachment",
        })
            Assert.Contains(expected, paths);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task Swagger_IsNotExposed_InProduction()
    {
        using var factory = new ConfigurableApiFactory("Production", new() { ["Jwt:SigningKey"] = new string('k', 48) });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
    }
}

public class HardeningTests
{
    private static readonly Dictionary<string, string?> StrongKey = new() { ["Jwt:SigningKey"] = "production-key-" + new string('x', 40) };

    [Theory]
    [InlineData("DEV-ONLY-CHANGE-THIS-KEY-32-CHARS-MIN-0123456789")]
    [InlineData("kisa-anahtar")]
    [InlineData("")]
    public void Production_RefusesToStart_WithWeakOrDevelopmentJwtKey(string key)
    {
        using var factory = new ConfigurableApiFactory("Production", new() { ["Jwt:SigningKey"] = key });

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains("Jwt:SigningKey", Flatten(ex!));
    }

    [Fact]
    public async Task Production_DoesNotCreateDemoAccounts_ByDefault()
    {
        using var factory = new ConfigurableApiFactory("Production", StrongKey);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", DemoDataSeeder.DemoPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Production_CanSeedDemoAccounts_WhenExplicitlyEnabled()
    {
        var settings = new Dictionary<string, string?>(StrongKey) { ["Demo:SeedAccounts"] = "true" };
        using var factory = new ConfigurableApiFactory("Production", settings);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", DemoDataSeeder.DemoPassword));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Login_IsRateLimited_PerClient()
    {
        using var factory = new ConfigurableApiFactory("Testing", new() { ["Auth:LoginAttemptsPerMinute"] = "3" });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 5; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "yanlis"))).StatusCode);

        Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests }, statuses);
        var limited = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", DemoDataSeeder.DemoPassword));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); // doğru şifre de sınırı aşamaz
        Assert.Contains("Çok fazla giriş denemesi", await limited.Content.ReadAsStringAsync());
        // Diğer endpoint'ler etkilenmez.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        using var factory = new ConfigurableApiFactory("Testing");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.GetAsync("/api/auth/me");

        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", res.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task ExpiredOrForgedTokens_AreRejected()
    {
        using var factory = new ConfigurableApiFactory("Testing");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", DemoDataSeeder.DemoPassword)))
            .Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        var token = login!.AccessToken;

        // İmzası bozulmuş token
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        // Rolü değiştirilmiş (imza geçersiz) token
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload)).Replace("\"Admin\"", "\"Ogrenci\"");
        var tampered = $"{parts[0]}.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.{parts[2]}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException!) parts.Add(e.Message);
        return string.Join(" | ", parts);
    }
}
