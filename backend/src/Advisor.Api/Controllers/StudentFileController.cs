using Advisor.Application.Communication;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Communication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Öğrenci dosyası: görüşme notları ve zaman çizelgesi. Yalnızca danışman/yönetici; notlar öğrenciye kapalıdır.</summary>
[ApiController]
[Route("api/student-files")]
[Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
public class StudentFileController : ControllerBase
{
    private readonly StudentFileService _files;

    public StudentFileController(StudentFileService files) => _files = files;

    [HttpGet("{studentNo}")]
    public async Task<ActionResult<StudentFile>> Get(string studentNo, CancellationToken ct) => Ok(await _files.GetAsync(studentNo, ct));

    [HttpPost("{studentNo}/notes")]
    public async Task<ActionResult<NoteDto>> AddNote(string studentNo, CreateNoteRequest request, CancellationToken ct) =>
        Ok(await _files.AddNoteAsync(studentNo, request, ct));

    [HttpDelete("notes/{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        await _files.DeleteNoteAsync(id, ct);
        return NoContent();
    }
}
