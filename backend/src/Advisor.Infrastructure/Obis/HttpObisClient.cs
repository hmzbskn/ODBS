using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advisor.Application.Obis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Advisor.Infrastructure.Obis;

/// <summary>
/// <see cref="IObisClient"/>'in tek implementasyonu: Obis.MockApi'ye HTTP üzerinden
/// bağlanır. Gerçek OBİS API'si geldiğinde bu dosya yeni bir implementasyonla
/// değiştirilir (ör. SOAP/farklı kimlik modeli), Application/Domain/Api hiç değişmez.
/// Yanıtlar kısa süreli <see cref="IMemoryCache"/> ile önbelleğe alınır.
/// </summary>
public class HttpObisClient : IObisClient
{
    // OBİS enum'ları (gün, sınav türü) metin olarak gönderir.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public HttpObisClient(HttpClient http, IMemoryCache cache, IOptions<ObisOptions> options)
    {
        _http = http;
        _http.BaseAddress ??= new Uri(options.Value.BaseUrl);
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(options.Value.CacheSeconds);
    }

    public Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default) =>
        GetCachedAsync("obis:students", "/api/students", ct, new List<ObisStudent>());

    public async Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default)
    {
        var key = $"obis:student:{studentNo}";
        if (_cache.TryGetValue(key, out ObisStudent? cached))
        {
            return cached;
        }

        var response = await _http.GetAsync($"/api/students/{studentNo}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<ObisStudent>(JsonOptions, ct);
        _cache.Set(key, student, _ttl);
        return student;
    }

    public Task<List<ObisTranscriptEntry>> GetTranscriptAsync(string studentNo, CancellationToken ct = default) =>
        GetCachedAsync($"obis:transcript:{studentNo}", $"/api/students/{studentNo}/transcript", ct, new List<ObisTranscriptEntry>());

    public Task<List<ObisRegistration>> GetRegistrationsAsync(string studentNo, int? term = null, CancellationToken ct = default) =>
        GetCachedAsync(
            $"obis:registrations:{studentNo}:{term}",
            $"/api/students/{studentNo}/registrations{TermQuery(term)}",
            ct, new List<ObisRegistration>());

    public Task<List<ObisAttendance>> GetAttendanceAsync(string studentNo, int? term = null, CancellationToken ct = default) =>
        GetCachedAsync(
            $"obis:attendance:{studentNo}:{term}",
            $"/api/students/{studentNo}/attendance{TermQuery(term)}",
            ct, new List<ObisAttendance>());

    public Task<List<ObisCourse>> GetCoursesAsync(CancellationToken ct = default) =>
        GetCachedAsync("obis:courses", "/api/courses", ct, new List<ObisCourse>());

    public Task<List<ObisScheduleSlot>> GetScheduleAsync(int? term = null, CancellationToken ct = default) =>
        GetCachedAsync($"obis:schedule:{term}", $"/api/schedule{TermQuery(term)}", ct, new List<ObisScheduleSlot>());

    public Task<List<ObisExamSlot>> GetExamsAsync(int? term = null, CancellationToken ct = default) =>
        GetCachedAsync($"obis:exams:{term}", $"/api/exams{TermQuery(term)}", ct, new List<ObisExamSlot>());

    public Task<ObisCurriculum> GetCurriculumAsync(CancellationToken ct = default) =>
        GetCachedAsync("obis:curriculum", "/api/curriculum", ct, new ObisCurriculum("", new(), 0, 0, 0));

    public Task<List<ObisAdvisor>> GetAdvisorsAsync(CancellationToken ct = default) =>
        GetCachedAsync("obis:advisors", "/api/advisors", ct, new List<ObisAdvisor>());

    private static string TermQuery(int? term) => term.HasValue ? $"?term={term.Value}" : string.Empty;

    private async Task<T> GetCachedAsync<T>(string cacheKey, string requestUri, CancellationToken ct, T fallback)
    {
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var result = await _http.GetFromJsonAsync<T>(requestUri, JsonOptions, ct) ?? fallback;
        _cache.Set(cacheKey, result, _ttl);
        return result;
    }
}
