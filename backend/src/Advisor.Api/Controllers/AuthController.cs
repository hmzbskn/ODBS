using Advisor.Application.Auth;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Advisor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly AdvisorDbContext _db;
    private readonly ICurrentUser _current;

    public AuthController(AuthService auth, AdvisorDbContext db, ICurrentUser current)
    {
        _auth = auth;
        _db = db;
        _current = current;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Kullanıcı adı ve şifre zorunludur." });

        var result = await _auth.LoginAsync(request, ct);
        return result is null
            ? Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." })
            : Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { _current.UserId }, ct);
        return user is null ? Unauthorized() : Ok(AuthService.ToInfo(user));
    }
}
