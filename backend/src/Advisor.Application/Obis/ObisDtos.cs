namespace Advisor.Application.Obis;

// Bu DTO'lar OBİS'in döndürdüğü verinin ANA UYGULAMA tarafındaki sözleşmesidir.
// Obis.MockApi'nin kendi Models tipleriyle KASITLI olarak aynı tip değildir —
// adapter deseninin amacı bu: Advisor.Application, Obis.MockApi projesine hiçbir
// şekilde bağımlı değil (ProjectReference yok), sadece HTTP üzerinden JSON alır.
// Gerçek OBİS'e geçişte burası değişmez, sadece IObisClient implementasyonu değişir.

public record ObisInternship(string StudentNo, int CompletedDays);

public record ObisAdvisor(string AdvisorId, string FirstName, string LastName, string Title);

public record ObisStudent(
    string StudentNo, string FirstName, string LastName, int ClassYear,
    string Department, string AdvisorId, double Gpa, string Status,
    string? Email = null, string? Phone = null, string? Residence = null, string? Program = null);

public record ObisCourse(
    string CourseCode, string CourseName, int Credit, int Ects, int Term,
    bool IsMandatory, List<string> Prerequisites);

public record ObisCurriculum(
    string Department, List<string> MandatoryCourseCodes, int ElectiveCreditRequirement,
    int TotalEctsRequirement, int InternshipDaysRequirement);

public record ObisTranscriptEntry(
    string StudentNo, int Term, string CourseCode, string CourseName,
    int Credit, int Ects, string LetterGrade, bool Passed);

/// <summary><paramref name="AttendanceRequired"/>: öğrencinin bu ders için devam şartı var mı. Alttan alınan ve geçmişte
/// devam şartı sağlanmış derslerde OBİS "aranmaz" döner; bu durumda ders saati çakışması sayılmaz.</summary>
public record ObisRegistration(
    string StudentNo, int Term, string CourseCode, string Section, string ApprovalStatus, bool AttendanceRequired = true);

public record ObisAttendance(string StudentNo, int Term, string CourseCode, int TotalSessions, int AttendedSessions)
{
    public double AbsenceRate => TotalSessions == 0 ? 0 : Math.Round((double)(TotalSessions - AttendedSessions) / TotalSessions, 2);
}

public record ObisScheduleSlot(
    int Term, string CourseCode, string CourseName, string Section,
    DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime, string Room, string? Instructor = null);

public enum ObisExamType { Midterm, Final }

public record ObisExamSlot(
    int Term, string CourseCode, string CourseName, string Section, ObisExamType Type,
    DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Room);
