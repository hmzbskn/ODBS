using Advisor.Application.Auth;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Management;

public class StudentDirectory : IStudentDirectory
{
    private readonly IObisClient _obis;
    private readonly AdvisorDbContext _db;
    private readonly ICurrentUser _user;

    public StudentDirectory(IObisClient obis, AdvisorDbContext db, ICurrentUser user)
    {
        _obis = obis;
        _db = db;
        _user = user;
    }

    public async Task<List<ObisStudent>> GetStudentsAsync(CancellationToken ct = default)
    {
        var students = await _obis.GetStudentsAsync(ct);
        var overrides = await _db.AdvisorAssignments.AsNoTracking().ToDictionaryAsync(a => a.StudentNo, a => a.AdvisorId, ct);
        if (overrides.Count == 0) return students;
        return students
            .Select(s => overrides.TryGetValue(s.StudentNo, out var advisor) ? s with { AdvisorId = advisor } : s)
            .ToList();
    }

    public async Task<ObisStudent?> GetStudentAsync(string studentNo, CancellationToken ct = default)
    {
        var student = await _obis.GetStudentAsync(studentNo, ct);
        if (student is null) return null;
        var advisor = await _db.AdvisorAssignments.AsNoTracking()
            .Where(a => a.StudentNo == studentNo).Select(a => a.AdvisorId).FirstOrDefaultAsync(ct);
        return advisor is null ? student : student with { AdvisorId = advisor };
    }

    public async Task<List<ObisStudent>> GetVisibleStudentsAsync(string? advisorId = null, CancellationToken ct = default)
    {
        var students = await GetStudentsAsync(ct);
        switch (_user.Role)
        {
            case UserRole.Danisman:
                return students.Where(s => s.AdvisorId == _user.LinkedAdvisorId).ToList();
            case UserRole.BolumBaskani:
                // Bölümü atanmamış bölüm başkanı hiçbir şey göremez (güvenli varsayılan).
                students = students.Where(s => _user.Department is not null && s.Department == _user.Department).ToList();
                break;
            case UserRole.Admin or UserRole.Dekan:
                break;
            default:
                return new List<ObisStudent>();
        }
        return advisorId is null ? students : students.Where(s => s.AdvisorId == advisorId).ToList();
    }
}
