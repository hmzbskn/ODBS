using Advisor.Domain.Enums;

namespace Advisor.Application.Communication;

public record CreateSlotRequest(DayOfWeek Day, string StartTime, string EndTime);

public record SlotDto(Guid Id, string AdvisorId, DayOfWeek Day, string StartTime, string EndTime, bool IsActive, int UpcomingBookings);

public record AppointmentDto(
    Guid Id, Guid SlotId, string AdvisorId, string StudentNo, string StudentName,
    DateOnly Date, string StartTime, string EndTime, AppointmentStatus Status,
    string? CancellationReason, DateTime BookedAt, bool IsPast);

public record AvailableSlotDto(
    Guid SlotId, DateOnly Date, DayOfWeek Day, string StartTime, string EndTime, bool Available, bool BookedByMe);

public record AvailabilityResponse(string AdvisorId, string AdvisorName, int MaxActiveAppointments, int ActiveAppointments, List<AvailableSlotDto> Slots);

public record BookRequest(Guid SlotId, DateOnly Date);

public record CancelRequest(string? Reason);

// ---- görüşme notları ve zaman çizelgesi ----

public record NoteDto(Guid Id, string AdvisorId, string StudentNo, string Content, DateTime CreatedAt);

public record CreateNoteRequest(string Content);

/// <summary>Kind: Note, Appointment, Petition, Announcement.</summary>
public record TimelineItem(string Kind, DateTime Date, string Title, string? Detail, Guid? RefId);

public record StudentFile(string StudentNo, string FullName, int ClassYear, double Gpa, List<TimelineItem> Timeline, List<NoteDto> Notes);
