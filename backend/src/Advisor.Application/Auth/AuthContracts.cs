using Advisor.Domain.Enums;

namespace Advisor.Application.Auth;

public record LoginRequest(string Username, string Password);

public record UserInfo(
    Guid Id, string Username, string DisplayName, UserRole Role,
    string? LinkedAdvisorId, string? LinkedStudentNo);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserInfo User);

/// <summary>İstek bağlamındaki oturum açmış kullanıcı (JWT claim'lerinden okunur).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    UserRole Role { get; }
    string? LinkedAdvisorId { get; }
    string? LinkedStudentNo { get; }
}

/// <summary>"Danışman yalnızca kendi öğrencilerini görür" kuralının tek uygulama noktası.
/// Öğrenci verisine dokunan her endpoint erişimi buradan doğrulamalıdır.</summary>
public interface IStudentAccessGuard
{
    Task<bool> CanAccessStudentAsync(string studentNo, CancellationToken ct = default);
}
