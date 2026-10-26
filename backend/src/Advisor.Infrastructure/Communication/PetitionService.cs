using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Communication;

/// <summary>Talep/dilekçe akışı: öğrenci açar (isteğe bağlı dosya ekiyle), danışman inceler ve sonuçlandırır.
/// Durumlar: Beklemede → İnceleniyor → Onaylandı / Reddedildi. Sonuçlanan talep değişmez.</summary>
public class PetitionService
{
    public const int MaxOpenPerStudent = 5;
    private const int MinDescription = 10;
    private const int MaxDescription = 2000;

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly IStudentDirectory _students;
    private readonly ICurrentUser _user;
    private readonly IStudentAccessGuard _guard;
    private readonly IFileStore _files;
    private readonly TimeProvider _time;

    public PetitionService(AdvisorDbContext db, IObisClient obis, IStudentDirectory students, ICurrentUser user, IStudentAccessGuard guard, IFileStore files, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _students = students;
        _user = user;
        _guard = guard;
        _files = files;
        _time = time;
    }

    public async Task<PetitionDto> CreateAsync(PetitionType type, string? description, UploadedFile? file, CancellationToken ct)
    {
        if (_user.Role != UserRole.Ogrenci || _user.LinkedStudentNo is null) throw AppException.Forbidden("Talebi yalnızca öğrenci açabilir.");
        var student = await _students.GetStudentAsync(_user.LinkedStudentNo, ct) ?? throw AppException.NotFound("Öğrenci kaydınız OBİS'te bulunamadı.");

        if (!Enum.IsDefined(type)) throw AppException.BadRequest("Geçersiz talep türü.");
        var text = description?.Trim() ?? string.Empty;
        if (text.Length < MinDescription || text.Length > MaxDescription)
            throw AppException.BadRequest($"Açıklama {MinDescription} ile {MaxDescription} karakter arasında olmalıdır.");

        var open = await _db.PetitionRequests.CountAsync(
            p => p.StudentNo == student.StudentNo && (p.Status == PetitionStatus.Beklemede || p.Status == PetitionStatus.Inceleniyor), ct);
        if (open >= MaxOpenPerStudent)
            throw AppException.Conflict($"En fazla {MaxOpenPerStudent} açık talebiniz olabilir. Mevcut taleplerin sonuçlanmasını bekleyin.");

        var petition = new PetitionRequest
        {
            Id = Guid.NewGuid(), StudentNo = student.StudentNo, Type = type, Description = text,
            Status = PetitionStatus.Beklemede, CreatedAt = _time.GetUtcNow().UtcDateTime,
        };

        if (file is not null)
        {
            var (safeName, contentType, extension) = AttachmentValidator.Validate(file.FileName, file.Content, file.Length);
            petition.AttachmentPath = await _files.SaveAsync(file.Content, extension, ct);
            petition.AttachmentFileName = safeName;
            petition.AttachmentContentType = contentType;
        }

        _db.PetitionRequests.Add(petition);
        await _db.SaveChangesAsync(ct);
        return ToDto(petition, $"{student.FirstName} {student.LastName}");
    }

    public async Task<List<PetitionDto>> ListAsync(PetitionStatus? status, CancellationToken ct)
    {
        var query = _db.PetitionRequests.AsQueryable();
        if (status.HasValue) query = query.Where(p => p.Status == status);

        List<PetitionRequest> petitions;
        switch (_user.Role)
        {
            case UserRole.Ogrenci:
                var no = _user.LinkedStudentNo ?? throw AppException.Forbidden();
                petitions = await query.Where(p => p.StudentNo == no).ToListAsync(ct);
                break;
            case UserRole.Danisman:
                var advisorId = _user.LinkedAdvisorId ?? throw AppException.Forbidden();
                var mine = (await _students.GetStudentsAsync(ct)).Where(s => s.AdvisorId == advisorId).Select(s => s.StudentNo).ToHashSet();
                petitions = (await query.ToListAsync(ct)).Where(p => mine.Contains(p.StudentNo)).ToList();
                break;
            default:
                petitions = await query.ToListAsync(ct);
                break;
        }

        var names = (await _students.GetStudentsAsync(ct)).ToDictionary(s => s.StudentNo, s => $"{s.FirstName} {s.LastName}");
        return petitions
            .OrderBy(p => p.Status is PetitionStatus.Onaylandi or PetitionStatus.Reddedildi) // açık talepler önce
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => ToDto(p, names.GetValueOrDefault(p.StudentNo, p.StudentNo)))
            .ToList();
    }

    public async Task<PetitionDto> SetStatusAsync(Guid id, SetPetitionStatusRequest request, CancellationToken ct)
    {
        if (_user.Role is not (UserRole.Danisman or UserRole.Admin)) throw AppException.Forbidden();
        var petition = await _db.PetitionRequests.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw AppException.NotFound("Talep bulunamadı.");
        if (!await _guard.CanAccessStudentAsync(petition.StudentNo, ct)) throw AppException.Forbidden("Bu talep size bağlı bir öğrenciye ait değil.");

        if (!Enum.IsDefined(request.Status)) throw AppException.BadRequest("Geçersiz durum.");
        if (petition.Status is PetitionStatus.Onaylandi or PetitionStatus.Reddedildi)
            throw AppException.Conflict("Sonuçlanmış bir talebin durumu değiştirilemez.");
        if (request.Status == petition.Status) throw AppException.BadRequest("Talep zaten bu durumda.");
        if (request.Status == PetitionStatus.Beklemede) throw AppException.BadRequest("Talep tekrar 'Beklemede' durumuna alınamaz.");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.Status == PetitionStatus.Reddedildi && note is null)
            throw AppException.BadRequest("Talebi reddederken öğrenciye iletilecek bir gerekçe yazmalısınız.");
        if (note is { Length: > 1000 }) throw AppException.BadRequest("Yanıt notu en fazla 1000 karakter olabilir.");

        petition.Status = request.Status;
        petition.AdvisorNote = note ?? petition.AdvisorNote;
        petition.HandledByAdvisorId = _user.LinkedAdvisorId ?? "ADMIN";
        petition.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), StudentNo = petition.StudentNo, CreatedAt = petition.UpdatedAt.Value,
            Message = $"{StudentFileService.PetitionTypeLabel(petition.Type)} talebiniz \"{StudentFileService.PetitionStatusLabel(petition.Status)}\" durumuna alındı." +
                      (note is null ? string.Empty : $" Danışman notu: {note}"),
        });

        await _db.SaveChangesAsync(ct);
        var student = await _students.GetStudentAsync(petition.StudentNo, ct);
        return ToDto(petition, student is null ? petition.StudentNo : $"{student.FirstName} {student.LastName}");
    }

    /// <summary>Ek dosyayı yalnızca talep sahibi öğrenci, öğrencinin danışmanı ve yönetici indirebilir.</summary>
    public async Task<AttachmentDownload> DownloadAttachmentAsync(Guid id, CancellationToken ct)
    {
        var petition = await _db.PetitionRequests.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw AppException.NotFound("Talep bulunamadı.");
        if (!await _guard.CanAccessStudentAsync(petition.StudentNo, ct)) throw AppException.NotFound("Talep bulunamadı.");
        if (petition.AttachmentPath is null) throw AppException.NotFound("Bu talepte ek dosya yok.");

        var stream = await _files.OpenAsync(petition.AttachmentPath, ct) ?? throw AppException.NotFound("Ek dosya bulunamadı.");
        return new AttachmentDownload(stream, petition.AttachmentFileName ?? "ek", petition.AttachmentContentType ?? "application/octet-stream");
    }

    private static PetitionDto ToDto(PetitionRequest p, string studentName) =>
        new(p.Id, p.StudentNo, studentName, p.Type, p.Description, p.Status, p.AdvisorNote,
            p.AttachmentPath is not null, p.AttachmentFileName, p.CreatedAt, p.UpdatedAt);
}
