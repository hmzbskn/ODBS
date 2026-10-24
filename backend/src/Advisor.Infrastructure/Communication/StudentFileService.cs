using System.Globalization;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Communication;

/// <summary>Öğrenci dosyası: görüşme notları ve zaman çizelgesi. Yalnızca öğrencinin danışmanı ve yönetici
/// erişir; notlar öğrenciye KAPALIDIR (öğrenci hiçbir uçtan göremez).</summary>
public class StudentFileService
{
    private const int MaxNoteLength = 2000;

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly ICurrentUser _user;
    private readonly IStudentAccessGuard _guard;
    private readonly TimeProvider _time;

    public StudentFileService(AdvisorDbContext db, IObisClient obis, ICurrentUser user, IStudentAccessGuard guard, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _user = user;
        _guard = guard;
        _time = time;
    }

    public async Task<StudentFile> GetAsync(string studentNo, CancellationToken ct)
    {
        var student = await AuthorizeAsync(studentNo, ct);
        var transcript = await _obis.GetTranscriptAsync(studentNo, ct);

        var notes = await _db.MeetingNotes.Where(n => n.StudentNo == studentNo).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        var appointments = await _db.Appointments.Include(a => a.Slot).Where(a => a.StudentNo == studentNo).ToListAsync(ct);
        var petitions = await _db.PetitionRequests.Where(p => p.StudentNo == studentNo).ToListAsync(ct);

        var timeline = new List<TimelineItem>();
        timeline.AddRange(notes.Select(n => new TimelineItem("Note", n.CreatedAt, "Görüşme notu", n.Content, n.Id)));
        timeline.AddRange(appointments.Select(a => new TimelineItem(
            "Appointment", TrTime.At(a.Date, a.Slot!.StartTime).AddHours(-3),
            $"Randevu: {a.Date:dd.MM.yyyy} {a.Slot.StartTime:HH\\:mm}-{a.Slot.EndTime:HH\\:mm}",
            AppointmentStatusLabel(a.Status) + (string.IsNullOrWhiteSpace(a.CancellationReason) ? string.Empty : $" · {a.CancellationReason}"), a.Id)));
        timeline.AddRange(petitions.Select(p => new TimelineItem(
            "Petition", p.CreatedAt, $"Talep: {PetitionTypeLabel(p.Type)}", $"{PetitionStatusLabel(p.Status)} · {Truncate(p.Description, 160)}", p.Id)));

        return new StudentFile(
            studentNo, $"{student.FirstName} {student.LastName}", student.ClassYear,
            RegistrationValidationService.ComputeGpa(transcript),
            timeline.OrderByDescending(t => t.Date).ToList(),
            notes.Select(ToDto).ToList());
    }

    public async Task<NoteDto> AddNoteAsync(string studentNo, CreateNoteRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(studentNo, ct);
        if (_user.Role != UserRole.Danisman || _user.LinkedAdvisorId is null)
            throw AppException.BadRequest("Görüşme notunu yalnızca öğrencinin danışmanı ekleyebilir.");

        var content = request.Content?.Trim();
        if (string.IsNullOrEmpty(content)) throw AppException.BadRequest("Not boş olamaz.");
        if (content.Length > MaxNoteLength) throw AppException.BadRequest($"Not en fazla {MaxNoteLength} karakter olabilir.");

        var note = new MeetingNote
        {
            Id = Guid.NewGuid(), AdvisorId = _user.LinkedAdvisorId, StudentNo = studentNo,
            Content = content, CreatedAt = _time.GetUtcNow().UtcDateTime,
        };
        _db.MeetingNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return ToDto(note);
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken ct)
    {
        if (_user.Role is not (UserRole.Danisman or UserRole.Admin)) throw AppException.Forbidden();
        var note = await _db.MeetingNotes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw AppException.NotFound("Not bulunamadı.");
        if (_user.Role != UserRole.Admin && note.AdvisorId != _user.LinkedAdvisorId)
            throw AppException.Forbidden("Bu notu yalnızca yazan danışman silebilir.");

        _db.MeetingNotes.Remove(note);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ObisStudent> AuthorizeAsync(string studentNo, CancellationToken ct)
    {
        if (_user.Role is not (UserRole.Danisman or UserRole.Admin)) throw AppException.Forbidden();
        if (!await _guard.CanAccessStudentAsync(studentNo, ct)) throw AppException.Forbidden("Bu öğrenci size bağlı değil.");
        return await _obis.GetStudentAsync(studentNo, ct) ?? throw AppException.NotFound("Öğrenci bulunamadı.");
    }

    private static NoteDto ToDto(MeetingNote n) => new(n.Id, n.AdvisorId, n.StudentNo, n.Content, n.CreatedAt);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public static string AppointmentStatusLabel(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Scheduled => "Planlandı",
        AppointmentStatus.CancelledByStudent => "Öğrenci iptal etti",
        AppointmentStatus.CancelledByAdvisor => "Danışman iptal etti",
        AppointmentStatus.Completed => "Tamamlandı",
        _ => s.ToString(),
    };

    public static string PetitionTypeLabel(PetitionType t) => t switch
    {
        PetitionType.DersEkleBirak => "Ders ekle-bırak",
        PetitionType.KayitDondurma => "Kayıt dondurma",
        PetitionType.Muafiyet => "Muafiyet",
        _ => "Diğer",
    };

    public static string PetitionStatusLabel(PetitionStatus s) => s switch
    {
        PetitionStatus.Beklemede => "Beklemede",
        PetitionStatus.Inceleniyor => "İnceleniyor",
        PetitionStatus.Onaylandi => "Onaylandı",
        PetitionStatus.Reddedildi => "Reddedildi",
        _ => s.ToString(),
    };
}
