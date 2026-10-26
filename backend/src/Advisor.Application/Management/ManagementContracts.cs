using Advisor.Application.Analytics;
using Advisor.Application.Obis;

namespace Advisor.Application.Management;

/// <summary>Öğrenci–danışman eşlemesinin tek kaynağı: OBİS'teki danışman, yönetimin yaptığı
/// atamalarla ezilmiş halde döner. Danışman kapsamına bakan her yer OBİS yerine bunu kullanır.</summary>
public interface IStudentDirectory
{
    Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default);
    Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default);

    /// <summary>Oturumdaki kullanıcının görebildiği öğrenciler: danışman kendi öğrencilerini, bölüm başkanı
    /// bölümünü, dekan/yönetici hepsini. <paramref name="advisorId"/> verilirse yalnız o danışmanın öğrencileri
    /// (danışman rolünde yok sayılır).</summary>
    Task<List<ObisStudent>> GetVisibleStudentsAsync(string? advisorId = null, CancellationToken ct = default);
}

public record AdvisorSummary(
    string AdvisorId, string FullName, string Title, int StudentCount, double AverageGpa,
    RiskDistribution Risk, int GraduationCandidates);

public record ManagementOverview(DashboardResponse Total, List<AdvisorSummary> Advisors);

public record ManagedStudent(
    string StudentNo, string FullName, int ClassYear, string Department, double Gpa,
    string AdvisorId, string AdvisorName, string ObisAdvisorId, bool IsReassigned);

public record ManagedAdvisor(string AdvisorId, string FullName, string Title, int StudentCount);

public record AssignRequest(string AdvisorId, List<string> StudentNos);

public record AssignResult(int Assigned, int Unchanged, int RestoredToObis);
