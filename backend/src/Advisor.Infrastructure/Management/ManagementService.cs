using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Management;

/// <summary>Bölüm başkanı / dekan / yönetici işleri: danışmanlara öğrenci atama ve her danışmanın
/// öğrencileri için analiz özeti. Kapsam <see cref="IStudentDirectory.GetVisibleStudentsAsync"/> ile
/// belirlenir; bölüm başkanı yalnızca kendi bölümünün öğrencilerini atayabilir.</summary>
public class ManagementService
{
    private static readonly DashboardResponse EmptyDashboard =
        new(0, 0, new RiskDistribution(0, 0, 0), new List<TermGpaPoint>(), new List<CourseSuccess>(), 0);

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly IStudentDirectory _directory;
    private readonly IAnalyticsService _analytics;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public ManagementService(
        AdvisorDbContext db, IObisClient obis, IStudentDirectory directory, IAnalyticsService analytics,
        ICurrentUser user, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _directory = directory;
        _analytics = analytics;
        _user = user;
        _time = time;
    }

    public async Task<ManagementOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        EnsureManagement();
        var scope = await _directory.GetVisibleStudentsAsync(null, ct);
        var advisors = await _obis.GetAdvisorsAsync(ct);
        var total = await _analytics.GetDashboardAsync(scope, ct);

        var rows = new List<AdvisorSummary>();
        foreach (var advisor in advisors.OrderBy(a => a.LastName, StringComparer.CurrentCulture))
        {
            var mine = scope.Where(s => s.AdvisorId == advisor.AdvisorId).ToList();
            var d = mine.Count == 0 ? EmptyDashboard : await _analytics.GetDashboardAsync(mine, ct);
            rows.Add(new AdvisorSummary(
                advisor.AdvisorId, $"{advisor.FirstName} {advisor.LastName}", advisor.Title,
                d.StudentCount, d.AverageGpa, d.Risk, d.GraduationCandidates));
        }
        return new ManagementOverview(total, rows);
    }

    public async Task<List<ManagedAdvisor>> GetAdvisorsAsync(CancellationToken ct = default)
    {
        EnsureManagement();
        var scope = await _directory.GetVisibleStudentsAsync(null, ct);
        return (await _obis.GetAdvisorsAsync(ct))
            .Select(a => new ManagedAdvisor(
                a.AdvisorId, $"{a.FirstName} {a.LastName}", a.Title, scope.Count(s => s.AdvisorId == a.AdvisorId)))
            .ToList();
    }

    public async Task<List<ManagedStudent>> GetStudentsAsync(string? advisorId, CancellationToken ct = default)
    {
        EnsureManagement();
        var scope = await _directory.GetVisibleStudentsAsync(advisorId, ct);
        var original = (await _obis.GetStudentsAsync(ct)).ToDictionary(s => s.StudentNo, s => s.AdvisorId);
        var names = (await _obis.GetAdvisorsAsync(ct)).ToDictionary(a => a.AdvisorId, a => $"{a.Title} {a.FirstName} {a.LastName}");

        return scope
            .OrderBy(s => s.StudentNo, StringComparer.Ordinal)
            .Select(s =>
            {
                var obisAdvisor = original.GetValueOrDefault(s.StudentNo, s.AdvisorId);
                return new ManagedStudent(
                    s.StudentNo, $"{s.FirstName} {s.LastName}", s.ClassYear, s.Department, s.Gpa,
                    s.AdvisorId, names.GetValueOrDefault(s.AdvisorId, s.AdvisorId), obisAdvisor, s.AdvisorId != obisAdvisor);
            })
            .ToList();
    }

    /// <summary>Öğrencileri danışmana atar. Hedef, öğrencinin OBİS'teki danışmanıysa ezme kaydı silinir.
    /// Kapsam dışı ya da bilinmeyen öğrenci numarası tüm isteği reddettirir (kısmi uygulama yok).</summary>
    public async Task<AssignResult> AssignAsync(AssignRequest request, CancellationToken ct = default)
    {
        EnsureManagement();
        var studentNos = request.StudentNos?.Distinct().ToList() ?? new List<string>();
        if (studentNos.Count == 0) throw AppException.BadRequest("En az bir öğrenci seçmelisiniz.");

        var advisors = await _obis.GetAdvisorsAsync(ct);
        if (advisors.All(a => a.AdvisorId != request.AdvisorId))
            throw AppException.NotFound("Danışman bulunamadı.");

        var scope = (await _directory.GetVisibleStudentsAsync(null, ct)).ToDictionary(s => s.StudentNo);
        var unknown = studentNos.Where(n => !scope.ContainsKey(n)).ToList();
        if (unknown.Count > 0)
            throw AppException.Forbidden($"Şu öğrenciler için atama yetkiniz yok ya da öğrenci bulunamadı: {string.Join(", ", unknown)}");

        var original = (await _obis.GetStudentsAsync(ct)).ToDictionary(s => s.StudentNo, s => s.AdvisorId);
        var existing = await _db.AdvisorAssignments.Where(a => studentNos.Contains(a.StudentNo)).ToDictionaryAsync(a => a.StudentNo, ct);

        int assigned = 0, unchanged = 0, restored = 0;
        var now = _time.GetUtcNow().UtcDateTime;
        var assignedBy = await CurrentUserNameAsync(ct);
        foreach (var no in studentNos)
        {
            var current = scope[no].AdvisorId;
            if (current == request.AdvisorId)
            {
                unchanged++;
                continue;
            }

            existing.TryGetValue(no, out var row);
            if (original[no] == request.AdvisorId)
            {
                if (row is not null) _db.AdvisorAssignments.Remove(row);
                restored++;
                continue;
            }

            if (row is null)
            {
                row = new AdvisorAssignment { Id = Guid.NewGuid(), StudentNo = no, AdvisorId = request.AdvisorId, AssignedByName = assignedBy };
                _db.AdvisorAssignments.Add(row);
            }
            row.PreviousAdvisorId = current;
            row.AdvisorId = request.AdvisorId;
            row.AssignedByUserId = _user.UserId;
            row.AssignedByName = assignedBy;
            row.AssignedAt = now;
            assigned++;
        }

        await _db.SaveChangesAsync(ct);
        return new AssignResult(assigned, unchanged, restored);
    }

    /// <summary>Bir danışmanın öğrencileri için panel (yönetim kapsamıyla kesişir).</summary>
    public async Task<DashboardResponse> GetAdvisorDashboardAsync(string advisorId, CancellationToken ct = default)
    {
        EnsureManagement();
        var mine = await _directory.GetVisibleStudentsAsync(advisorId, ct);
        return mine.Count == 0 ? EmptyDashboard : await _analytics.GetDashboardAsync(mine, ct);
    }

    private async Task<string> CurrentUserNameAsync(CancellationToken ct) =>
        await _db.Users.Where(u => u.Id == _user.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? _user.Role.ToString();

    private void EnsureManagement()
    {
        if (!_user.IsAuthenticated || !_user.Role.IsManagement()) throw AppException.Forbidden();
    }
}
