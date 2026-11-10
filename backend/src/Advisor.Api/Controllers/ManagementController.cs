using Advisor.Application.Analytics;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Bölümün tüm ders programı satırı (öğretim elemanıyla). <see cref="Term"/> yarıyıl numarası (1-8).</summary>
public record ManagementScheduleSlot(
    int Term, string CourseCode, string CourseName, string Section, DayOfWeek Day,
    string StartTime, string EndTime, string Room, string? Instructor);

public record ManagementSchedule(string Semester, IReadOnlyList<ManagementScheduleSlot> Slots);

/// <summary>Danışmanların üstündeki katman (bölüm başkanı, dekan, yönetici): danışman atama ve
/// danışman bazlı analiz. Kapsam servis katmanında uygulanır (bölüm başkanı yalnız kendi bölümü).</summary>
[ApiController]
[Route("api/management")]
[Authorize(Roles = Roles)]
public class ManagementController : ControllerBase
{
    public const string Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.BolumBaskani) + "," + nameof(UserRole.Dekan);

    private readonly ManagementService _service;
    private readonly IObisClient _obis;

    public ManagementController(ManagementService service, IObisClient obis)
    {
        _service = service;
        _obis = obis;
    }

    /// <summary>Okuldaki tüm derslerin haftalık programı. <paramref name="semester"/>: "fall" (güz, tek yarıyıllar) ya da
    /// "spring" (bahar, çift yarıyıllar); verilmezse tarihe göre (Eylül-Ocak güz) seçilir.</summary>
    [HttpGet("schedule")]
    public async Task<ActionResult<ManagementSchedule>> Schedule([FromQuery] string? semester, CancellationToken ct)
    {
        var fall = semester is null ? DateTime.Today.Month is >= 9 or 1 : !string.Equals(semester, "spring", StringComparison.OrdinalIgnoreCase);
        var slots = (await _obis.GetScheduleAsync(null, ct))
            .Where(s => (s.Term % 2 == 1) == fall)
            .OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day).ThenBy(s => s.StartTime).ThenBy(s => s.CourseCode).ThenBy(s => s.Section)
            .Select(s => new ManagementScheduleSlot(
                s.Term, s.CourseCode, s.CourseName, s.Section, s.Day,
                s.StartTime.ToString("HH\\:mm"), s.EndTime.ToString("HH\\:mm"), s.Room, s.Instructor))
            .ToList();
        return Ok(new ManagementSchedule(fall ? "fall" : "spring", slots));
    }

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
