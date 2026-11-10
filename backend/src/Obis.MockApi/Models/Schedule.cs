namespace Obis.MockApi.Models;

/// <summary>Bir ders şubesinin haftalık program satırı.</summary>
public record ScheduleSlot(
    int Term,
    string CourseCode,
    string CourseName,
    string Section,
    DayOfWeek Day,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room,
    string? Instructor = null);

public enum ExamType
{
    Midterm,
    Final
}

/// <summary>Bir ders şubesinin sınav takvimi satırı.</summary>
public record ExamSlot(
    int Term,
    string CourseCode,
    string CourseName,
    string Section,
    ExamType Type,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Room);
