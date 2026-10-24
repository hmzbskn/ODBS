using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Communication;

/// <summary>Öğrencinin bildirim kutusu: duyurular ve sistem bildirimleri (randevu iptali, talep durumu...) tek listede,
/// okundu durumuyla. Öğrenci yalnızca kendi kutusunu görür.</summary>
public class InboxService
{
    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public InboxService(AdvisorDbContext db, IObisClient obis, ICurrentUser user, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _user = user;
        _time = time;
    }

    public async Task<List<InboxItem>> ListAsync(CancellationToken ct)
    {
        var no = StudentNo();
        var recipients = await _db.AnnouncementRecipients.Include(r => r.Announcement).Where(r => r.StudentNo == no).ToListAsync(ct);
        var notifications = await _db.Notifications.Where(n => n.StudentNo == no).ToListAsync(ct);
        var advisors = (await _obis.GetAdvisorsAsync(ct)).ToDictionary(a => a.AdvisorId, a => $"{a.Title} {a.FirstName} {a.LastName}");

        var items = recipients.Select(r => new InboxItem(
                r.Announcement!.Id, "Announcement", r.Announcement.Title, r.Announcement.Body,
                advisors.GetValueOrDefault(r.Announcement.AdvisorId, "Yönetim"), r.Announcement.CreatedAt, r.ReadAt != null))
            .Concat(notifications.Select(n => new InboxItem(n.Id, "Notification", "Sistem bildirimi", n.Message, null, n.CreatedAt, n.ReadAt != null)))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
        return items;
    }

    public async Task<UnreadCount> UnreadCountAsync(CancellationToken ct)
    {
        var no = StudentNo();
        var a = await _db.AnnouncementRecipients.CountAsync(r => r.StudentNo == no && r.ReadAt == null, ct);
        var n = await _db.Notifications.CountAsync(x => x.StudentNo == no && x.ReadAt == null, ct);
        return new UnreadCount(a + n);
    }

    public async Task MarkReadAsync(string kind, Guid id, CancellationToken ct)
    {
        var no = StudentNo();
        var now = _time.GetUtcNow().UtcDateTime;
        switch (kind.ToLowerInvariant())
        {
            case "announcement":
            {
                var r = await _db.AnnouncementRecipients.FirstOrDefaultAsync(x => x.AnnouncementId == id && x.StudentNo == no, ct)
                    ?? throw AppException.NotFound("Duyuru bulunamadı.");
                r.ReadAt ??= now;
                break;
            }
            case "notification":
            {
                var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.StudentNo == no, ct)
                    ?? throw AppException.NotFound("Bildirim bulunamadı.");
                n.ReadAt ??= now;
                break;
            }
            default:
                throw AppException.BadRequest("Bilinmeyen bildirim türü.");
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var no = StudentNo();
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var r in await _db.AnnouncementRecipients.Where(x => x.StudentNo == no && x.ReadAt == null).ToListAsync(ct)) r.ReadAt = now;
        foreach (var n in await _db.Notifications.Where(x => x.StudentNo == no && x.ReadAt == null).ToListAsync(ct)) n.ReadAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private string StudentNo() =>
        _user.Role == UserRole.Ogrenci && _user.LinkedStudentNo is { } no ? no : throw AppException.Forbidden();
}
