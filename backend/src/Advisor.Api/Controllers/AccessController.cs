using Advisor.Application.Auth;
using Advisor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Rol ve sahiplik kurallarının uçtan uca doğrulandığı küçük uç noktalar.
/// Sonraki aşamalardaki gerçek controller'lar aynı kalıbı kullanır
/// (<c>[Authorize(Roles=...)]</c> + <see cref="IStudentAccessGuard"/>).</summary>
[ApiController]
[Route("api/access")]
public class AccessController : ControllerBase
{
    private readonly IStudentAccessGuard _guard;

    public AccessController(IStudentAccessGuard guard) => _guard = guard;

    [HttpGet("admin")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public IActionResult AdminOnly() => Ok(new { ok = true });

    [HttpGet("advisor")]
    [Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
    public IActionResult AdvisorArea() => Ok(new { ok = true });

    [HttpGet("students/{studentNo}")]
    public async Task<IActionResult> Student(string studentNo, CancellationToken ct) =>
        await _guard.CanAccessStudentAsync(studentNo, ct)
            ? Ok(new { ok = true, studentNo })
            : Forbid();
}
