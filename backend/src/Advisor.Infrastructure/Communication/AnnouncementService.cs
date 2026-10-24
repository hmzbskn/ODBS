using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Communication;

/// <summary>Filtreli duyuru: danışman kriter seçer (sınıf, GANO aralığı, risk seviyesi, staj durumu), hedef kitleyi
/// önizler ve gönderir. Her öğrenci için ayrı bir alıcı kaydı tutulur; okundu durumu buradadır.</summary>
public class AnnouncementService
{
    private const int MaxTitle = 150;
    private const int MaxBody = 2000;

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly IAnalyticsService _analytics;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public AnnouncementService(AdvisorDbContext db, IObisClient obis, IAnalyticsService analytics, ICurrentUser user, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _analytics = analytics;
        _user = user;
        _time = time;
    }

    public async Task<AudiencePreview> PreviewAsync(AnnouncementFilter filter, CancellationToken ct)
    {
        var students = await ResolveAudienceAsync(filter, ct);
        var rows = students
            .Select(p => new RecipientPreview(p.StudentNo, p.FullName, p.ClassYear, p.HasGpa ? p.Gpa : null, p.Risk, p.InternshipDays))
            .OrderBy(r => r.StudentNo, StringComparer.Ordinal)
            .ToList();
        return new AudiencePreview(rows.Count, rows);
    }

    public async Task<AnnouncementSummary> CreateAsync(CreateAnnouncementRequest request, CancellationToken ct)
    {
        var owner = OwnerId();
        var title = request.Title?.Trim() ?? string.Empty;
        var body = request.Body?.Trim() ?? string.Empty;
        if (title.Length < 3 || title.Length > MaxTitle)
            throw AppException.BadRequest($"Başlık 3 ile {MaxTitle} karakter arasında olmalıdır.");
        if (body.Length < 5 || body.Length > MaxBody)
            throw AppException.BadRequest($"Duyuru metni 5 ile {MaxBody} karakter arasında olmalıdır.");

        var audience = await ResolveAudienceAsync(request.Filter, ct);
        if (audience.Count == 0) throw AppException.BadRequest("Seçilen filtreyle eşleşen öğrenci yok; duyuru gönderilmedi.");

        var f = request.Filter;
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(), AdvisorId = owner, Title = title, Body = body, CreatedAt = _time.GetUtcNow().UtcDateTime,
            FilterClassYear = f.ClassYear, FilterMinGpa = f.MinGpa, FilterMaxGpa = f.MaxGpa,
            FilterRiskLevel = f.RiskLevel, FilterInternshipComplete = f.InternshipComplete,
        };
        foreach (var s in audience)
        {
            announcement.Recipients.Add(new AnnouncementRecipient { Id = Guid.NewGuid(), StudentNo = s.StudentNo });
        }

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(ct);
        return ToSummary(announcement, audience.Count, 0);
    }

    public async Task<List<AnnouncementSummary>> ListAsync(CancellationToken ct)
    {
        var owner = OwnerFilter();
        var items = await _db.Announcements
            .Include(a => a.Recipients)
            .Where(a => owner == null || a.AdvisorId == owner)
            .ToListAsync(ct);
        return items
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToSummary(a, a.Recipients.Count, a.Recipients.Count(r => r.ReadAt != null)))
            .ToList();
    }

    public async Task<AnnouncementDetail> GetAsync(Guid id, CancellationToken ct)
    {
        EnsureStaff();
        var a = await _db.Announcements.Include(x => x.Recipients).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("Duyuru bulunamadı.");
        if (_user.Role != UserRole.Admin && a.AdvisorId != _user.LinkedAdvisorId)
            throw AppException.Forbidden("Bu duyuru size ait değil.");

        var names = (await _obis.GetStudentsAsync(ct)).ToDictionary(s => s.StudentNo, s => $"{s.FirstName} {s.LastName}");
        var recipients = a.Recipients
            .Select(r => new RecipientStatus(r.StudentNo, names.GetValueOrDefault(r.StudentNo, r.StudentNo), r.ReadAt))
            .OrderBy(r => r.ReadAt.HasValue).ThenBy(r => r.StudentNo, StringComparer.Ordinal)
            .ToList();
        return new AnnouncementDetail(ToSummary(a, recipients.Count, recipients.Count(r => r.ReadAt != null)), recipients);
    }

    // ------------------------------------------------------------ hedef kitle

    private async Task<List<StudentProfile>> ResolveAudienceAsync(AnnouncementFilter f, CancellationToken ct)
    {
        EnsureStaff();
        ValidateFilter(f);

        var students = await _obis.GetStudentsAsync(ct);
        var scope = _user.Role == UserRole.Danisman
            ? students.Where(s => s.AdvisorId == _user.LinkedAdvisorId).ToList()
            : students;

        var profiles = await _analytics.GetProfilesAsync(scope, ct);
        var gpaFiltered = f.MinGpa.HasValue || f.MaxGpa.HasValue;

        return profiles
            .Where(p => f.ClassYear is null || p.ClassYear == f.ClassYear)
            .Where(p => !gpaFiltered || p.HasGpa) // Notlandırılmış dersi olmayanın GANO'su yoktur
            .Where(p => f.MinGpa is null || p.Gpa >= f.MinGpa)
            .Where(p => f.MaxGpa is null || p.Gpa <= f.MaxGpa)
            .Where(p => f.RiskLevel is null || p.Risk == f.RiskLevel)
            .Where(p => f.InternshipComplete is null || p.InternshipComplete == f.InternshipComplete)
            .ToList();
    }

    private static void ValidateFilter(AnnouncementFilter f)
    {
        if (f is null) throw AppException.BadRequest("Filtre bilgisi eksik.");
        if (f.ClassYear is < 1 or > 4) throw AppException.BadRequest("Sınıf 1 ile 4 arasında olmalıdır.");
        if (f.MinGpa is < 0 or > 4 || f.MaxGpa is < 0 or > 4 || (f.MinGpa.HasValue && !double.IsFinite(f.MinGpa.Value))
            || (f.MaxGpa.HasValue && !double.IsFinite(f.MaxGpa.Value)))
            throw AppException.BadRequest("GANO değerleri 0 ile 4 arasında olmalıdır.");
        if (f.MinGpa.HasValue && f.MaxGpa.HasValue && f.MinGpa > f.MaxGpa)
            throw AppException.BadRequest("En düşük GANO, en yüksek GANO'dan büyük olamaz.");
    }

    // ------------------------------------------------------------ yardımcılar

    private void EnsureStaff()
    {
        if (_user.Role is not (UserRole.Danisman or UserRole.Admin)) throw AppException.Forbidden();
    }

    private string OwnerId()
    {
        EnsureStaff();
        return _user.Role == UserRole.Admin ? "ADMIN" : _user.LinkedAdvisorId ?? throw AppException.Forbidden();
    }

    private string? OwnerFilter()
    {
        EnsureStaff();
        return _user.Role == UserRole.Admin ? null : _user.LinkedAdvisorId ?? throw AppException.Forbidden();
    }

    private static AnnouncementSummary ToSummary(Announcement a, int recipients, int read) =>
        new(a.Id, a.Title, a.Body, a.CreatedAt, a.AdvisorId,
            new AnnouncementFilter(a.FilterClassYear, a.FilterMinGpa, a.FilterMaxGpa, a.FilterRiskLevel, a.FilterInternshipComplete),
            recipients, read);
}
