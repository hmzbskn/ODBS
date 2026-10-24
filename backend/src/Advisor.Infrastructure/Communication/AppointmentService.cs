using System.Globalization;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Communication;

/// <summary>Randevu sistemi: danışman haftalık görüşme saatleri tanımlar, öğrenci kendi danışmanının
/// boş saatinden randevu alır, iki taraf da iptal edebilir. Tüm zamanlar Türkiye saatidir.</summary>
public class AppointmentService
{
    public const int MaxActiveAppointments = 2;
    public const int HorizonDays = 28;
    public const int LeadMinutes = 60;
    private const int MinSlotMinutes = 10;
    private const int MaxSlotMinutes = 120;
    private static readonly TimeOnly OfficeStart = new(8, 0);
    private static readonly TimeOnly OfficeEnd = new(20, 0);

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public AppointmentService(AdvisorDbContext db, IObisClient obis, ICurrentUser user, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _user = user;
        _time = time;
    }

    private DateTime Now => TrTime.Now(_time);

    // ------------------------------------------------------------ danışman: görüşme saatleri

    public async Task<List<SlotDto>> ListSlotsAsync(CancellationToken ct)
    {
        EnsureStaff();
        var advisorId = _user.Role == UserRole.Admin ? null : _user.LinkedAdvisorId;
        var slots = await _db.AppointmentSlots
            .Where(s => s.IsActive && (advisorId == null || s.AdvisorId == advisorId))
            .OrderBy(s => s.AdvisorId).ThenBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        var scheduled = await _db.Appointments
            .Include(a => a.Slot)
            .Where(a => a.Status == AppointmentStatus.Scheduled)
            .ToListAsync(ct);
        var now = Now;

        return slots
            .Select(s => ToDto(s, scheduled.Count(a => a.SlotId == s.Id && StartOf(a) > now)))
            .ToList();
    }

    public async Task<SlotDto> CreateSlotAsync(CreateSlotRequest request, CancellationToken ct)
    {
        EnsureStaff();
        if (_user.Role != UserRole.Danisman || _user.LinkedAdvisorId is null)
            throw AppException.BadRequest("Görüşme saatlerini yalnızca danışmanlar tanımlayabilir.");

        if (!TimeOnly.TryParseExact(request.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !TimeOnly.TryParseExact(request.EndTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw AppException.BadRequest("Saatler SS:dd biçiminde olmalıdır (ör. 14:30).");
        if (request.Day is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw AppException.BadRequest("Görüşme saatleri yalnızca hafta içi günler için tanımlanabilir.");
        if (end <= start) throw AppException.BadRequest("Bitiş saati başlangıç saatinden sonra olmalıdır.");
        if (start < OfficeStart || end > OfficeEnd)
            throw AppException.BadRequest($"Görüşme saatleri {OfficeStart:HH\\:mm} ile {OfficeEnd:HH\\:mm} arasında olmalıdır.");

        var minutes = (end - start).TotalMinutes;
        if (minutes < MinSlotMinutes || minutes > MaxSlotMinutes)
            throw AppException.BadRequest($"Görüşme süresi {MinSlotMinutes} ile {MaxSlotMinutes} dakika arasında olmalıdır.");

        var advisorId = _user.LinkedAdvisorId;
        var overlaps = await _db.AppointmentSlots.AnyAsync(
            s => s.IsActive && s.AdvisorId == advisorId && s.DayOfWeek == request.Day && s.StartTime < end && start < s.EndTime, ct);
        if (overlaps) throw AppException.Conflict("Bu saat aralığı, tanımlı başka bir görüşme saatiyle çakışıyor.");

        var slot = new AppointmentSlot { Id = Guid.NewGuid(), AdvisorId = advisorId, DayOfWeek = request.Day, StartTime = start, EndTime = end };
        _db.AppointmentSlots.Add(slot);
        await _db.SaveChangesAsync(ct);
        return ToDto(slot, 0);
    }

    public async Task DeleteSlotAsync(Guid id, CancellationToken ct)
    {
        EnsureStaff();
        var slot = await _db.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("Görüşme saati bulunamadı.");
        if (_user.Role != UserRole.Admin && slot.AdvisorId != _user.LinkedAdvisorId)
            throw AppException.Forbidden("Bu görüşme saati size ait değil.");

        var appointments = await _db.Appointments.Include(a => a.Slot).Where(a => a.SlotId == id).ToListAsync(ct);
        var now = Now;
        if (appointments.Any(a => a.Status == AppointmentStatus.Scheduled && StartOf(a) > now))
            throw AppException.Conflict("Bu saatte gelecek tarihli randevular var; önce onları iptal edin.");

        if (appointments.Count == 0) _db.AppointmentSlots.Remove(slot);
        else slot.IsActive = false; // Geçmiş randevular kaybolmasın.
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ randevular

    public async Task<List<AppointmentDto>> ListAppointmentsAsync(CancellationToken ct)
    {
        var query = _db.Appointments.Include(a => a.Slot).AsQueryable();
        switch (_user.Role)
        {
            case UserRole.Ogrenci:
                var no = _user.LinkedStudentNo ?? throw AppException.Forbidden();
                query = query.Where(a => a.StudentNo == no);
                break;
            case UserRole.Danisman:
                var advisor = _user.LinkedAdvisorId ?? throw AppException.Forbidden();
                query = query.Where(a => a.Slot!.AdvisorId == advisor);
                break;
        }

        var list = await query.ToListAsync(ct);
        var names = (await _obis.GetStudentsAsync(ct)).ToDictionary(s => s.StudentNo, s => $"{s.FirstName} {s.LastName}");
        var now = Now;
        return list
            .OrderByDescending(StartOf)
            .Select(a => ToDto(a, names.GetValueOrDefault(a.StudentNo, a.StudentNo), now))
            .ToList();
    }

    /// <summary>Öğrencinin kendi danışmanının önümüzdeki 4 haftadaki randevu saatleri ve doluluk durumu.</summary>
    public async Task<AvailabilityResponse> GetAvailabilityAsync(CancellationToken ct)
    {
        var student = await CurrentStudentAsync(ct);
        var advisors = await _obis.GetAdvisorsAsync(ct);
        var advisor = advisors.FirstOrDefault(a => a.AdvisorId == student.AdvisorId);
        var advisorName = advisor is null ? student.AdvisorId : $"{advisor.Title} {advisor.FirstName} {advisor.LastName}";

        var slots = await _db.AppointmentSlots.Where(s => s.IsActive && s.AdvisorId == student.AdvisorId).ToListAsync(ct);
        var scheduled = await _db.Appointments
            .Include(a => a.Slot)
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.Slot!.AdvisorId == student.AdvisorId)
            .ToListAsync(ct);

        var now = Now;
        var today = TrTime.Today(_time);
        var result = new List<AvailableSlotDto>();
        for (var offset = 0; offset <= HorizonDays; offset++)
        {
            var date = today.AddDays(offset);
            foreach (var slot in slots.Where(s => s.DayOfWeek == date.DayOfWeek).OrderBy(s => s.StartTime))
            {
                if (TrTime.At(date, slot.StartTime) < now.AddMinutes(LeadMinutes)) continue;
                var taken = scheduled.FirstOrDefault(a => a.SlotId == slot.Id && a.Date == date);
                result.Add(new AvailableSlotDto(
                    slot.Id, date, slot.DayOfWeek, Fmt(slot.StartTime), Fmt(slot.EndTime),
                    taken is null, taken?.StudentNo == student.StudentNo));
            }
        }

        var mine = scheduled.Count(a => a.StudentNo == student.StudentNo && StartOf(a) > now);
        return new AvailabilityResponse(student.AdvisorId, advisorName, MaxActiveAppointments, mine, result);
    }

    public async Task<AppointmentDto> BookAsync(BookRequest request, CancellationToken ct)
    {
        var student = await CurrentStudentAsync(ct);
        var slot = await _db.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == request.SlotId, ct)
            ?? throw AppException.NotFound("Randevu saati bulunamadı.");
        if (!slot.IsActive) throw AppException.BadRequest("Bu görüşme saati artık geçerli değil.");
        if (slot.AdvisorId != student.AdvisorId)
            throw AppException.Forbidden("Yalnızca kendi danışmanınızdan randevu alabilirsiniz.");

        var today = TrTime.Today(_time);
        if (request.Date < today) throw AppException.BadRequest("Geçmiş bir tarih için randevu alınamaz.");
        if (request.Date > today.AddDays(HorizonDays))
            throw AppException.BadRequest($"Randevu en fazla {HorizonDays} gün sonrasına alınabilir.");
        if (request.Date.DayOfWeek != slot.DayOfWeek)
            throw AppException.BadRequest("Seçilen tarih bu görüşme saatinin gününe denk gelmiyor.");

        var now = Now;
        if (TrTime.At(request.Date, slot.StartTime) < now.AddMinutes(LeadMinutes))
            throw AppException.BadRequest($"Randevu saatine en az {LeadMinutes} dakika kala randevu alınamaz.");

        var mine = await _db.Appointments.Include(a => a.Slot)
            .Where(a => a.StudentNo == student.StudentNo && a.Status == AppointmentStatus.Scheduled).ToListAsync(ct);
        if (mine.Count(a => StartOf(a) > now) >= MaxActiveAppointments)
            throw AppException.Conflict($"En fazla {MaxActiveAppointments} aktif randevunuz olabilir. Önce birini iptal edin.");

        if (await _db.Appointments.AnyAsync(a => a.SlotId == slot.Id && a.Date == request.Date && a.Status == AppointmentStatus.Scheduled, ct))
            throw AppException.Conflict("Bu saat dolu. Lütfen başka bir saat seçin.");

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(), SlotId = slot.Id, Slot = slot, StudentNo = student.StudentNo,
            Date = request.Date, Status = AppointmentStatus.Scheduled, BookedAt = _time.GetUtcNow().UtcDateTime,
        };
        _db.Appointments.Add(appointment);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Aynı saate eşzamanlı iki istek: kısmi tekil indeks ikincisini reddeder.
            throw AppException.Conflict("Bu saat dolu. Lütfen başka bir saat seçin.");
        }

        return ToDto(appointment, $"{student.FirstName} {student.LastName}", now);
    }

    public async Task CancelAsync(Guid id, CancelRequest request, CancellationToken ct)
    {
        var appointment = await _db.Appointments.Include(a => a.Slot).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("Randevu bulunamadı.");

        var byStudent = _user.Role == UserRole.Ogrenci;
        if (byStudent)
        {
            if (appointment.StudentNo != _user.LinkedStudentNo) throw AppException.NotFound("Randevu bulunamadı.");
        }
        else if (_user.Role == UserRole.Danisman && appointment.Slot!.AdvisorId != _user.LinkedAdvisorId)
        {
            throw AppException.Forbidden("Bu randevu size ait değil.");
        }

        if (appointment.Status != AppointmentStatus.Scheduled)
            throw AppException.BadRequest("Bu randevu zaten iptal edilmiş ya da tamamlanmış.");
        if (StartOf(appointment) <= Now)
            throw AppException.BadRequest("Başlamış ya da geçmiş bir randevu iptal edilemez.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 300 }) throw AppException.BadRequest("İptal gerekçesi en fazla 300 karakter olabilir.");

        appointment.Status = byStudent ? AppointmentStatus.CancelledByStudent : AppointmentStatus.CancelledByAdvisor;
        appointment.CancellationReason = reason;

        if (!byStudent)
        {
            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), StudentNo = appointment.StudentNo, CreatedAt = _time.GetUtcNow().UtcDateTime,
                Message = $"Danışmanınız {appointment.Date:dd.MM.yyyy} {Fmt(appointment.Slot!.StartTime)} randevunuzu iptal etti." +
                          (reason is null ? string.Empty : $" Gerekçe: {reason}"),
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid id, CancellationToken ct)
    {
        EnsureStaff();
        var appointment = await _db.Appointments.Include(a => a.Slot).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("Randevu bulunamadı.");
        if (_user.Role == UserRole.Danisman && appointment.Slot!.AdvisorId != _user.LinkedAdvisorId)
            throw AppException.Forbidden("Bu randevu size ait değil.");
        if (appointment.Status != AppointmentStatus.Scheduled)
            throw AppException.BadRequest("Yalnızca planlanmış randevular tamamlandı olarak işaretlenebilir.");
        if (StartOf(appointment) > Now)
            throw AppException.BadRequest("Randevu henüz gerçekleşmedi.");

        appointment.Status = AppointmentStatus.Completed;
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ yardımcılar

    private void EnsureStaff()
    {
        if (_user.Role is not (UserRole.Danisman or UserRole.Admin)) throw AppException.Forbidden();
    }

    private async Task<ObisStudent> CurrentStudentAsync(CancellationToken ct)
    {
        if (_user.Role != UserRole.Ogrenci || _user.LinkedStudentNo is null) throw AppException.Forbidden();
        return await _obis.GetStudentAsync(_user.LinkedStudentNo, ct)
            ?? throw AppException.NotFound("Öğrenci kaydınız OBİS'te bulunamadı.");
    }

    private static DateTime StartOf(Appointment a) => TrTime.At(a.Date, a.Slot!.StartTime);

    private static string Fmt(TimeOnly t) => t.ToString("HH\\:mm", CultureInfo.InvariantCulture);

    private static SlotDto ToDto(AppointmentSlot s, int upcoming) =>
        new(s.Id, s.AdvisorId, s.DayOfWeek, Fmt(s.StartTime), Fmt(s.EndTime), s.IsActive, upcoming);

    private static AppointmentDto ToDto(Appointment a, string studentName, DateTime now) =>
        new(a.Id, a.SlotId, a.Slot!.AdvisorId, a.StudentNo, studentName, a.Date, Fmt(a.Slot.StartTime), Fmt(a.Slot.EndTime),
            a.Status, a.CancellationReason, a.BookedAt, StartOf(a) <= now);
}
