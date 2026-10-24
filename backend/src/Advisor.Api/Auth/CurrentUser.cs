using System.Security.Claims;
using Advisor.Application.Auth;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;

namespace Advisor.Api.Auth;

public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor) => _principal = accessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(_principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? _principal?.FindFirstValue("sub"), out var id)
            ? id : Guid.Empty;

    public UserRole Role =>
        Enum.TryParse<UserRole>(_principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : UserRole.Ogrenci;

    public string? LinkedAdvisorId => _principal?.FindFirstValue(ClaimNames.LinkedAdvisorId);
    public string? LinkedStudentNo => _principal?.FindFirstValue(ClaimNames.LinkedStudentNo);
}

public class StudentAccessGuard : IStudentAccessGuard
{
    private readonly ICurrentUser _user;
    private readonly IObisClient _obis;

    public StudentAccessGuard(ICurrentUser user, IObisClient obis)
    {
        _user = user;
        _obis = obis;
    }

    public async Task<bool> CanAccessStudentAsync(string studentNo, CancellationToken ct = default)
    {
        if (!_user.IsAuthenticated) return false;
        switch (_user.Role)
        {
            case UserRole.Admin:
                return true;
            case UserRole.Ogrenci:
                return _user.LinkedStudentNo == studentNo;
            case UserRole.Danisman:
                var student = await _obis.GetStudentAsync(studentNo, ct);
                return student is not null && student.AdvisorId == _user.LinkedAdvisorId;
            default:
                return false;
        }
    }
}
