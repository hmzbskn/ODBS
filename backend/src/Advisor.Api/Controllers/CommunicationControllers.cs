using Advisor.Application.Communication;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Communication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Filtreli duyuru: yalnızca danışman/yönetici. Yetki ve sahiplik kuralları servis katmanındadır.</summary>
[ApiController]
[Route("api/announcements")]
[Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
public class AnnouncementsController : ControllerBase
{
    private readonly AnnouncementService _announcements;

    public AnnouncementsController(AnnouncementService announcements) => _announcements = announcements;

    [HttpPost("preview")]
    public async Task<ActionResult<AudiencePreview>> Preview(AnnouncementFilter filter, CancellationToken ct) =>
        Ok(await _announcements.PreviewAsync(filter, ct));

    [HttpPost]
    public async Task<ActionResult<AnnouncementSummary>> Create(CreateAnnouncementRequest request, CancellationToken ct) =>
        Ok(await _announcements.CreateAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementSummary>>> List(CancellationToken ct) => Ok(await _announcements.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnnouncementDetail>> Get(Guid id, CancellationToken ct) => Ok(await _announcements.GetAsync(id, ct));
}

/// <summary>Öğrencinin bildirim kutusu (duyurular + sistem bildirimleri).</summary>
[ApiController]
[Route("api/inbox")]
[Authorize(Roles = nameof(UserRole.Ogrenci))]
public class InboxController : ControllerBase
{
    private readonly InboxService _inbox;

    public InboxController(InboxService inbox) => _inbox = inbox;

    [HttpGet]
    public async Task<ActionResult<List<InboxItem>>> List(CancellationToken ct) => Ok(await _inbox.ListAsync(ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCount>> Unread(CancellationToken ct) => Ok(await _inbox.UnreadCountAsync(ct));

    [HttpPost("{kind}/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(string kind, Guid id, CancellationToken ct)
    {
        await _inbox.MarkReadAsync(kind, id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _inbox.MarkAllReadAsync(ct);
        return NoContent();
    }
}

public class CreatePetitionForm
{
    public PetitionType Type { get; set; }
    public string? Description { get; set; }
    public IFormFile? File { get; set; }
}

/// <summary>Talep/dilekçe. Öğrenci açar; danışman/yönetici durumunu değiştirir; ek dosyayı sahibi, danışmanı ve yönetici indirir.</summary>
[ApiController]
[Route("api/petitions")]
public class PetitionsController : ControllerBase
{
    private const long RequestLimit = AttachmentValidator.MaxBytes + 1024 * 1024; // dosya + form alanları

    private readonly PetitionService _petitions;

    public PetitionsController(PetitionService petitions) => _petitions = petitions;

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<ActionResult<PetitionDto>> Create([FromForm] CreatePetitionForm form, CancellationToken ct)
    {
        UploadedFile? upload = null;
        await using var buffer = new MemoryStream();
        if (form.File is { Length: > 0 } file)
        {
            // Boyut sınırı doğrulayıcıda; yine de belleğe bu sınırdan fazlasını almayız.
            if (file.Length > AttachmentValidator.MaxBytes) return BadRequest(new { message = "Dosya çok büyük (en fazla 5 MB)." });
            await file.CopyToAsync(buffer, ct);
            buffer.Seek(0, SeekOrigin.Begin);
            upload = new UploadedFile(buffer, file.FileName, file.Length);
        }

        return Ok(await _petitions.CreateAsync(form.Type, form.Description, upload, ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<PetitionDto>>> List([FromQuery] PetitionStatus? status, CancellationToken ct) =>
        Ok(await _petitions.ListAsync(status, ct));

    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
    public async Task<ActionResult<PetitionDto>> SetStatus(Guid id, SetPetitionStatusRequest request, CancellationToken ct) =>
        Ok(await _petitions.SetStatusAsync(id, request, ct));

    [HttpGet("{id:guid}/attachment")]
    public async Task<IActionResult> Attachment(Guid id, CancellationToken ct)
    {
        var file = await _petitions.DownloadAttachmentAsync(id, ct);
        // Tarayıcının içeriği yorumlayıp çalıştırmasını engelle: her zaman indirme olarak sun.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
