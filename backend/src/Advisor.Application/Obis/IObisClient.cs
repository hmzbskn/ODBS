namespace Advisor.Application.Obis;

/// <summary>Öğrenci bilgi sistemine (OBİS) tek bağlantı noktası. Ana uygulamanın her
/// yeri sadece bu arayüzü bilir; gerçek OBİS API'si geldiğinde yapılacak iş yeni bir
/// implementasyon yazıp DI kaydını değiştirmektir (bkz. docs/ARCHITECTURE.md).</summary>
public interface IObisClient
{
    Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default);
    Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default);
    Task<List<ObisTranscriptEntry>> GetTranscriptAsync(string studentNo, CancellationToken ct = default);
    Task<List<ObisRegistration>> GetRegistrationsAsync(string studentNo, int? term = null, CancellationToken ct = default);
    Task<List<ObisAttendance>> GetAttendanceAsync(string studentNo, int? term = null, CancellationToken ct = default);
    Task<List<ObisCourse>> GetCoursesAsync(CancellationToken ct = default);
    Task<List<ObisScheduleSlot>> GetScheduleAsync(int? term = null, CancellationToken ct = default);
    Task<List<ObisExamSlot>> GetExamsAsync(int? term = null, CancellationToken ct = default);
    Task<ObisCurriculum> GetCurriculumAsync(CancellationToken ct = default);
    Task<List<ObisAdvisor>> GetAdvisorsAsync(CancellationToken ct = default);
}
