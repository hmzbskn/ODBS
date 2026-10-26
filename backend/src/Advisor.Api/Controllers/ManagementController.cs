using Advisor.Application.Analytics;
using Advisor.Application.Management;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Danışmanların üstündeki katman (bölüm başkanı, dekan, yönetici): danışman atama ve
/// danışman bazlı analiz. Kapsam servis katmanında uygulanır (bölüm başkanı yalnız kendi bölümü).</summary>
[ApiController]
[Route("api/management")]
[Authorize(Roles = Roles)]
public class ManagementController : ControllerBase
{
    public const string Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.BolumBaskani) + "," + nameof(UserRole.Dekan);

    private readonly ManagementService _service;

    public ManagementController(ManagementService service) => _service = service;

    [HttpGet("overview")]
    public async Task<ActionResult<ManagementOverview>> Overview(CancellationToken ct) =>
        Ok(await _service.GetOverviewAsync(ct));

    [HttpGet("advisors")]
    public async Task<ActionResult<List<ManagedAdvisor>>> Advisors(CancellationToken ct) =>
        Ok(await _service.GetAdvisorsAsync(ct));

    [HttpGet("advisors/{advisorId}/dashboard")]
    public async Task<ActionResult<DashboardResponse>> AdvisorDashboard(string advisorId, CancellationToken ct) =>
        Ok(await _service.GetAdvisorDashboardAsync(advisorId, ct));

    [HttpGet("students")]
    public async Task<ActionResult<List<ManagedStudent>>> Students([FromQuery] string? advisorId, CancellationToken ct) =>
        Ok(await _service.GetStudentsAsync(advisorId, ct));

    [HttpPost("assignments")]
    public async Task<ActionResult<AssignResult>> Assign(AssignRequest request, CancellationToken ct) =>
        Ok(await _service.AssignAsync(request, ct));
}
