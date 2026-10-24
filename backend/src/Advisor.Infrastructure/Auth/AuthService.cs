using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Advisor.Application.Auth;
using Advisor.Domain.Entities;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Advisor.Infrastructure.Auth;

public static class ClaimNames
{
    public const string LinkedAdvisorId = "advisor_id";
    public const string LinkedStudentNo = "student_no";
}

public class AuthService
{
    private readonly AdvisorDbContext _db;
    private readonly JwtOptions _options;

    public AuthService(AdvisorDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>Kullanıcı adı/şifre doğrularsa token döner; yanlışsa null. "Kullanıcı yok" ile
    /// "şifre yanlış" ayrımı dışarı sızdırılmaz.</summary>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        return new LoginResponse(CreateToken(user, expires), expires, ToInfo(user));
    }

    public static UserInfo ToInfo(AppUser u) =>
        new(u.Id, u.Username, u.DisplayName, u.Role, u.LinkedAdvisorId, u.LinkedStudentNo);

    private string CreateToken(AppUser user, DateTime expiresUtc)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.LinkedAdvisorId is not null) claims.Add(new(ClaimNames.LinkedAdvisorId, user.LinkedAdvisorId));
        if (user.LinkedStudentNo is not null) claims.Add(new(ClaimNames.LinkedStudentNo, user.LinkedStudentNo));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
