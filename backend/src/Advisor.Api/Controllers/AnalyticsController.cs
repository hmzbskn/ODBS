using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Erken uyarı (risk), mezuniyet analizi ve danışman paneli. Risk yalnızca danışman/yönetici
/// içindir; mezuniyet analizini öğrenci kendisi için de görebilir.</summary>
[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private const string Staff = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin);

    private readonly IAnalyticsService _analytics;
    private readonly IObisClient _obis;
    private readonly ICurrentUser _current;
    private readonly IStudentAccessGuard _guard;

    public AnalyticsController(IAnalyticsService analytics, IObisClient obis, ICurrentUser current, IStudentAccessGuard guard)
    {
        _analytics = analytics;
        _obis = obis;
        _current = current;
        _guard = guard;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken ct) =>
        Ok(await _analytics.GetDashboardAsync(await ScopeAsync(ct), ct));

    [HttpGet("risk")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<List<RiskRow>>> RiskList(CancellationToken ct) =>
        Ok(await _analytics.GetRiskListAsync(await ScopeAsync(ct), ct));

    [HttpGet("students/{studentNo}/risk")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> Risk(string studentNo, CancellationToken ct) =>
        await _guard.CanAccessStudentAsync(studentNo, ct) ? Ok(await _analytics.GetRiskAsync(studentNo, ct)) : Forbid();

    [HttpGet("students/{studentNo}/graduation")]
    public async Task<IActionResult> Graduation(string studentNo, CancellationToken ct) =>
        await _guard.CanAccessStudentAsync(studentNo, ct) ? Ok(await _analytics.GetGraduationAsync(studentNo, ct)) : Forbid();

    /// <summary>Danışman yalnızca kendi öğrencilerini, yönetici hepsini görür.</summary>
    private async Task<IReadOnlyList<ObisStudent>> ScopeAsync(CancellationToken ct)
    {
        var students = await _obis.GetStudentsAsync(ct);
        return _current.Role == UserRole.Danisman
            ? students.Where(s => s.AdvisorId == _current.LinkedAdvisorId).ToList()
            : students;
    }
}
