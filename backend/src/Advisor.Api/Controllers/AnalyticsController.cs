using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Management;
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
    // Danışmanın üstündeki yönetim katmanı da analizleri görür (kendi kapsamında).
    private const string Staff = nameof(UserRole.Danisman) + "," + ManagementController.Roles;

    private readonly IAnalyticsService _analytics;
    private readonly IStudentDirectory _students;
    private readonly ICurrentUser _current;
    private readonly IStudentAccessGuard _guard;

    public AnalyticsController(IAnalyticsService analytics, IStudentDirectory students, ICurrentUser current, IStudentAccessGuard guard)
    {
        _analytics = analytics;
        _students = students;
        _current = current;
        _guard = guard;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<DashboardResponse>> Dashboard([FromQuery] string? advisorId, CancellationToken ct) =>
        Ok(await _analytics.GetDashboardAsync(await _students.GetVisibleStudentsAsync(advisorId, ct), ct));

    [HttpGet("risk")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<List<RiskRow>>> RiskList([FromQuery] string? advisorId, CancellationToken ct) =>
        Ok(await _analytics.GetRiskListAsync(await _students.GetVisibleStudentsAsync(advisorId, ct), ct));

    [HttpGet("students/{studentNo}/risk")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> Risk(string studentNo, CancellationToken ct) =>
        await _guard.CanAccessStudentAsync(studentNo, ct) ? Ok(await _analytics.GetRiskAsync(studentNo, ct)) : Forbid();

    [HttpGet("students/{studentNo}/graduation")]
    public async Task<IActionResult> Graduation(string studentNo, CancellationToken ct) =>
        await _guard.CanAccessStudentAsync(studentNo, ct) ? Ok(await _analytics.GetGraduationAsync(studentNo, ct)) : Forbid();
}
