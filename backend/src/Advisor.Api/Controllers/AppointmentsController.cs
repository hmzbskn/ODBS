using Advisor.Application.Communication;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Communication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Randevular. Rol ve sahiplik kuralları servis katmanında uygulanır; burada yalnızca rol kapısı vardır.</summary>
[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private const string Staff = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin);
    private const string Participants = nameof(UserRole.Ogrenci) + "," + Staff;

    private readonly AppointmentService _appointments;

    public AppointmentsController(AppointmentService appointments) => _appointments = appointments;

    // ---- danışman / yönetici ----

    [HttpGet("slots")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<List<SlotDto>>> Slots(CancellationToken ct) => Ok(await _appointments.ListSlotsAsync(ct));

    [HttpPost("slots")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<SlotDto>> CreateSlot(CreateSlotRequest request, CancellationToken ct) =>
        Ok(await _appointments.CreateSlotAsync(request, ct));

    [HttpDelete("slots/{id:guid}")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken ct)
    {
        await _appointments.DeleteSlotAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _appointments.CompleteAsync(id, ct);
        return NoContent();
    }

    // ---- öğrenci ----

    [HttpGet("availability")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public async Task<ActionResult<AvailabilityResponse>> Availability(CancellationToken ct) =>
        Ok(await _appointments.GetAvailabilityAsync(ct));

    [HttpPost("book")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public async Task<ActionResult<AppointmentDto>> Book(BookRequest request, CancellationToken ct) =>
        Ok(await _appointments.BookAsync(request, ct));

    // ---- ortak ----

    /// <summary>Öğrenci kendi, danışman kendi saatlerindeki, yönetici tüm randevuları görür.</summary>
    // Bölüm Başkanı / Dekan randevu modülünü kullanmaz; servis onları süzmediği için rol burada sınırlanır.
    [HttpGet]
    [Authorize(Roles = Participants)]
    public async Task<ActionResult<List<AppointmentDto>>> List(CancellationToken ct) => Ok(await _appointments.ListAppointmentsAsync(ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Participants)]
    public async Task<IActionResult> Cancel(Guid id, CancelRequest? request, CancellationToken ct)
    {
        await _appointments.CancelAsync(id, request ?? new CancelRequest(null), ct);
        return NoContent();
    }
}
