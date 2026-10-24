using Advisor.Domain.Enums;

namespace Advisor.Domain.Entities;

/// <summary>Danışmanın haftalık tanımladığı randevu saat aralığı (tekrarlanan, haftanın
/// belirli günü/saati). Rezervasyon durumu <see cref="Appointment"/>'ta tutulur.</summary>
public class AppointmentSlot
{
    public Guid Id { get; set; }
    public required string AdvisorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Appointment
{
    public Guid Id { get; set; }
    public Guid SlotId { get; set; }
    public AppointmentSlot? Slot { get; set; }
    public required string StudentNo { get; set; }

    /// <summary>Randevunun gerçekleştiği takvim tarihi (slot haftalık tekrar eder,
    /// bu alan hangi haftaya denk geldiğini sabitler).</summary>
    public DateOnly Date { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public DateTime BookedAt { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>Danışmanın bir görüşme için tuttuğu not. Öğrenciye kapalı.</summary>
public class MeetingNote
{
    public Guid Id { get; set; }
    public required string AdvisorId { get; set; }
    public required string StudentNo { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Danışmanın filtreli hedef kitleye gönderdiği duyuru.</summary>
public class Announcement
{
    public Guid Id { get; set; }
    public required string AdvisorId { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }

    // Basit filtre kriterleri — null ise o kritere bakılmaz.
    public int? FilterClassYear { get; set; }
    public double? FilterMinGpa { get; set; }
    public double? FilterMaxGpa { get; set; }
    public RiskLevel? FilterRiskLevel { get; set; }

    public List<AnnouncementRead> Reads { get; set; } = new();
}

public class AnnouncementRead
{
    public Guid Id { get; set; }
    public Guid AnnouncementId { get; set; }
    public Announcement? Announcement { get; set; }
    public required string StudentNo { get; set; }
    public DateTime ReadAt { get; set; }
}

/// <summary>Öğrencinin açtığı talep/dilekçe.</summary>
public class PetitionRequest
{
    public Guid Id { get; set; }
    public required string StudentNo { get; set; }
    public PetitionType Type { get; set; }
    public required string Description { get; set; }
    public string? AttachmentPath { get; set; }
    public PetitionStatus Status { get; set; } = PetitionStatus.Beklemede;
    public string? AdvisorNote { get; set; }
    public string? HandledByAdvisorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Öğrenciye gönderilen bildirim kutusu kaydı (duyuru dışında, sistem
/// bildirimleri — ör. talep durumu değişti).</summary>
public class Notification
{
    public Guid Id { get; set; }
    public required string StudentNo { get; set; }
    public required string Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
