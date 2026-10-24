using Advisor.Domain.Enums;

namespace Advisor.Application.Communication;

// ---- duyuru ----

public record AnnouncementFilter(int? ClassYear, double? MinGpa, double? MaxGpa, RiskLevel? RiskLevel, bool? InternshipComplete);

public record RecipientPreview(string StudentNo, string FullName, int ClassYear, double? Gpa, RiskLevel Risk, int InternshipDays);

public record AudiencePreview(int Count, List<RecipientPreview> Students);

public record CreateAnnouncementRequest(string Title, string Body, AnnouncementFilter Filter);

public record AnnouncementSummary(
    Guid Id, string Title, string Body, DateTime CreatedAt, string AdvisorId,
    AnnouncementFilter Filter, int RecipientCount, int ReadCount);

public record RecipientStatus(string StudentNo, string FullName, DateTime? ReadAt);

public record AnnouncementDetail(AnnouncementSummary Summary, List<RecipientStatus> Recipients);

// ---- gelen kutusu (öğrenci) ----

/// <summary>Kind: Announcement veya Notification.</summary>
public record InboxItem(Guid Id, string Kind, string Title, string Body, string? Sender, DateTime CreatedAt, bool Read);

public record UnreadCount(int Count);

// ---- talep / dilekçe ----

public record PetitionDto(
    Guid Id, string StudentNo, string StudentName, PetitionType Type, string Description,
    PetitionStatus Status, string? AdvisorNote, bool HasAttachment, string? AttachmentFileName,
    DateTime CreatedAt, DateTime? UpdatedAt);

public record SetPetitionStatusRequest(PetitionStatus Status, string? Note);

/// <summary>Yüklenen dosya: akış + özgün ad. Tür sunucuda uzantı ve içerik imzasından belirlenir (istemciye güvenilmez).</summary>
public record UploadedFile(Stream Content, string FileName, long Length);

public record AttachmentDownload(Stream Content, string FileName, string ContentType);

// ---- dosya deposu ----

public interface IFileStore
{
    /// <summary>Akışı rastgele bir adla kaydeder ve saklama adını döndürür.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    /// <summary>Saklama adıyla dosyayı açar; yoksa null.</summary>
    Task<Stream?> OpenAsync(string storedName, CancellationToken ct = default);
}
