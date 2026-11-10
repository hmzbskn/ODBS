namespace Obis.MockApi.Models;

/// <summary>Bir öğrencinin bir dönemde aldığı bir dersin transkript satırı.</summary>
public record TranscriptEntry(
    string StudentNo,
    int Term,
    string CourseCode,
    string CourseName,
    int Credit,
    int Ects,
    string LetterGrade,
    bool Passed);

/// <summary>Bir öğrencinin bir dönemde seçtiği ders kaydı (OBİS'teki onay durumu).</summary>
public record RegistrationEntry(
    string StudentNo,
    int Term,
    string CourseCode,
    string Section,
    string ApprovalStatus,
    bool AttendanceRequired = true);

/// <summary>Bir öğrencinin bir dönem/ders bazında devamsızlık bilgisi.</summary>
public record AttendanceRecord(
    string StudentNo,
    int Term,
    string CourseCode,
    int TotalSessions,
    int AttendedSessions)
{
    public double AbsenceRate => TotalSessions == 0
        ? 0
        : Math.Round((double)(TotalSessions - AttendedSessions) / TotalSessions, 2);
}

/// <summary>Bir öğrencinin OBİS'e işlenmiş tamamlanan staj gün sayısı.</summary>
public record InternshipRecord(string StudentNo, int CompletedDays);
