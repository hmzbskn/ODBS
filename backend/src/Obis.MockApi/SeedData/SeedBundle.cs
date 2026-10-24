using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>Üretilen tüm mock OBİS verisinin tek bir pakette tutulduğu taşıyıcı tip.
/// JSON'a serileştirilip dosyaya yazılır, API da aynı şekilde deserileştirip belleğe alır.</summary>
public record SeedBundle(
    List<AdvisorInfo> Advisors,
    List<StudentInfo> Students,
    List<CourseInfo> Courses,
    CurriculumInfo Curriculum,
    List<ScheduleSlot> Schedule,
    List<ExamSlot> Exams,
    List<TranscriptEntry> Transcripts,
    List<RegistrationEntry> Registrations,
    List<AttendanceRecord> Attendance,
    List<InternshipRecord> Internships,
    List<EdgeCaseNote> EdgeCases);

/// <summary>Hangi öğrenci numarasının hangi bilinçli uç durumu temsil ettiğinin kaydı —
/// testlerin ve dokümantasyonun belirli bir öğrenciyi aramak için kullanabileceği referans.</summary>
public record EdgeCaseNote(string StudentNo, string Tag);
