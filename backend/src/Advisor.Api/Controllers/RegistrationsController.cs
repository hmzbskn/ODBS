using Advisor.Application.Auth;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Api.Controllers;

public record AdvisorStudentRow(
    string StudentNo, string FullName, int ClassYear, double Gpa, int Term,
    int CourseCount, int TotalEcts, int ErrorCount, int WarningCount, int InfoCount,
    RegistrationDecision Decision, int ConflictCount = 0);

public record AdvisorStudentList(int? Term, IReadOnlyList<int> AvailableTerms, IReadOnlyList<AdvisorStudentRow> Students);

public record RegisteredCourseDto(
    string CourseCode, string CourseName, string Section, int Ects, int CourseTerm, bool InConflict);

public record WeeklySlotDto(
    string CourseCode, string CourseName, string Section, DayOfWeek Day,
    string StartTime, string EndTime, string Room, bool InConflict);

public record ReviewDto(
    Guid Id, RegistrationDecision Decision, string? Note, string ReviewedByAdvisorId, DateTime ReviewedAt);

/// <summary>Öğrencinin danışmanı. OBİS'te e-posta alanı olmadığı için adres, Selçuk biçiminden
/// (adsoyad@selcuk.edu.tr) türetilir.</summary>
public record AdvisorContactDto(string Name, string Title, string Email);

public record RegistrationDetail(
    string StudentNo, string FullName, int ClassYear, int Term, double Gpa,
    int TotalEcts, int MaxEcts,
    IReadOnlyList<RegisteredCourseDto> Courses,
    IReadOnlyList<WeeklySlotDto> WeeklySchedule,
    IReadOnlyList<RegistrationIssue> Issues,
    int ErrorCount, int WarningCount, int InfoCount,
    RegistrationDecision Decision, IReadOnlyList<ReviewDto> Reviews, AdvisorContactDto? Advisor);

public record ReviewRequest(int Term, RegistrationDecision Decision, string? Note);

[ApiController]
[Route("api/registrations")]
public class RegistrationsController : ControllerBase
{
    private readonly IRegistrationValidationService _validation;
    private readonly IObisClient _obis;
    private readonly IStudentDirectory _students;
    private readonly AdvisorDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IStudentAccessGuard _guard;

    public RegistrationsController(
        IRegistrationValidationService validation, IObisClient obis, IStudentDirectory students, AdvisorDbContext db,
        ICurrentUser current, IStudentAccessGuard guard)
    {
        _validation = validation;
        _obis = obis;
        _students = students;
        _db = db;
        _current = current;
        _guard = guard;
    }

    /// <summary>Danışmanın öğrenci listesi, her öğrencinin uyarı sayılarıyla. Admin tüm öğrencileri görür.
    /// <paramref name="term"/> verilirse yalnızca o dönemde kaydı olan öğrenciler listelenir.</summary>
    [HttpGet("students")]
    [Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
    public async Task<ActionResult<AdvisorStudentList>> Students([FromQuery] int? term, CancellationToken ct)
    {
        var students = await _students.GetVisibleStudentsAsync(null, ct);

        var results = new List<RegistrationValidationResult>();
        foreach (var s in students.OrderBy(s => s.StudentNo))
        {
            var result = await _validation.ValidateAsync(s.StudentNo, term, ct);
            if (result is null) continue;
            if (term.HasValue && result.Selected.Count == 0) continue;
            results.Add(result);
        }

        var studentNos = results.Select(r => r.Student.StudentNo).ToList();
        var reviews = await _db.RegistrationReviews
            .Where(r => studentNos.Contains(r.StudentNo))
            .ToListAsync(ct);

        RegistrationDecision DecisionFor(RegistrationValidationResult r) =>
            reviews.Where(v => v.StudentNo == r.Student.StudentNo && v.Term == r.Term)
                .OrderByDescending(v => v.ReviewedAt)
                .Select(v => v.Decision)
                .FirstOrDefault(RegistrationDecision.Pending);

        var rows = results
            .Select(r => new AdvisorStudentRow(
                r.Student.StudentNo, $"{r.Student.FirstName} {r.Student.LastName}", r.Student.ClassYear,
                r.Gpa, r.Term, r.Selected.Count, r.TotalEcts, r.ErrorCount, r.WarningCount, r.InfoCount,
                DecisionFor(r), r.Issues.Count(i => i.RuleCode == "TIME_CONFLICT")))
            .ToList();

        var maxTerm = results.Count > 0 ? results.Max(r => r.Term) : 0;
        return new AdvisorStudentList(term, Enumerable.Range(1, maxTerm).ToList(), rows);
    }

    /// <summary>Tek öğrencinin dönem kaydı: dersler, haftalık program (çakışanlar işaretli),
    /// kural bulguları ve danışman kararları.</summary>
    [HttpGet("{studentNo}")]
    public async Task<ActionResult<RegistrationDetail>> Detail(string studentNo, [FromQuery] int? term, CancellationToken ct)
    {
        if (!await _guard.CanAccessStudentAsync(studentNo, ct)) return Forbid();

        var result = await _validation.ValidateAsync(studentNo, term, ct);
        if (result is null) return NotFound(new { message = "Öğrenci bulunamadı." });

        var courses = (await _obis.GetCoursesAsync(ct)).ToDictionary(c => c.CourseCode);
        var schedule = await _obis.GetScheduleAsync(null, ct);

        var conflictCodes = result.Issues
            .Where(i => i.RuleCode is "TIME_CONFLICT")
            .SelectMany(i => i.CourseCodes)
            .ToHashSet();
        var examConflictCodes = result.Issues
            .Where(i => i.RuleCode is "EXAM_CONFLICT")
            .SelectMany(i => i.CourseCodes)
            .ToHashSet();

        var courseDtos = result.Selected
            .Select(r =>
            {
                courses.TryGetValue(r.CourseCode, out var c);
                return new RegisteredCourseDto(
                    r.CourseCode, c?.CourseName ?? r.CourseCode, r.Section, c?.Ects ?? 0, c?.Term ?? 0,
                    conflictCodes.Contains(r.CourseCode) || examConflictCodes.Contains(r.CourseCode));
            })
            .ToList();

        var weekly = result.Selected
            .SelectMany(r => schedule
                .Where(s => s.CourseCode == r.CourseCode && s.Section == r.Section)
                .Select(s => new WeeklySlotDto(
                    s.CourseCode, s.CourseName, s.Section, s.Day,
                    s.StartTime.ToString("HH\\:mm"), s.EndTime.ToString("HH\\:mm"), s.Room,
                    conflictCodes.Contains(s.CourseCode) && OverlapsOther(s, r, result.Selected, schedule))))
            .OrderBy(s => s.Day == DayOfWeek.Sunday ? 7 : (int)s.Day)
            .ThenBy(s => s.StartTime, StringComparer.Ordinal)
            .ToList();

        var reviews = await _db.RegistrationReviews
            .Where(r => r.StudentNo == studentNo && r.Term == result.Term)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync(ct);

        var advisorId = (await _students.GetStudentAsync(studentNo, ct))?.AdvisorId ?? result.Student.AdvisorId;
        var advisor = (await _obis.GetAdvisorsAsync(ct)).FirstOrDefault(a => a.AdvisorId == advisorId);
        var advisorDto = advisor is null
            ? null
            : new AdvisorContactDto($"{advisor.FirstName} {advisor.LastName}", advisor.Title, AdvisorEmail(advisor.FirstName, advisor.LastName));

        return new RegistrationDetail(
            result.Student.StudentNo, $"{result.Student.FirstName} {result.Student.LastName}",
            result.Student.ClassYear, result.Term, result.Gpa, result.TotalEcts, result.MaxEcts,
            courseDtos, weekly, result.Issues, result.ErrorCount, result.WarningCount, result.InfoCount,
            reviews.Count > 0 ? reviews[0].Decision : RegistrationDecision.Pending,
            reviews.Select(r => new ReviewDto(r.Id, r.Decision, r.Note, r.ReviewedByAdvisorId, r.ReviewedAt)).ToList(),
            advisorDto);
    }

    internal static string AdvisorEmail(string firstName, string lastName)
    {
        static string Ascii(string v) => new string(v.ToLowerInvariant()
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c')
            .Where(char.IsAsciiLetterOrDigit).ToArray());
        return $"{Ascii(firstName)}{Ascii(lastName)}@selcuk.edu.tr";
    }

    /// <summary>Danışman kararı: onayla ya da düzeltme iste (not zorunlu). Karar geçmişi tutulur;
    /// en son kayıt geçerlidir.</summary>
    [HttpPost("{studentNo}/review")]
    [Authorize(Roles = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin))]
    public async Task<ActionResult<ReviewDto>> Review(string studentNo, ReviewRequest request, CancellationToken ct)
    {
        if (!await _guard.CanAccessStudentAsync(studentNo, ct)) return Forbid();

        if (request.Decision == RegistrationDecision.Pending)
            return BadRequest(new { message = "Karar 'Onayla' ya da 'Düzeltme iste' olmalıdır." });
        if (request.Decision == RegistrationDecision.RevisionRequested && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Düzeltme isterken öğrenciye iletilecek bir not yazmalısınız." });

        var student = await _obis.GetStudentAsync(studentNo, ct);
        if (student is null) return NotFound(new { message = "Öğrenci bulunamadı." });

        var registrations = await _obis.GetRegistrationsAsync(studentNo, request.Term, ct);
        if (registrations.Count == 0)
            return BadRequest(new { message = "Bu öğrencinin seçilen dönemde ders kaydı yok." });

        var review = new RegistrationReview
        {
            Id = Guid.NewGuid(),
            StudentNo = studentNo,
            Term = request.Term,
            Decision = request.Decision,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ReviewedByAdvisorId = _current.LinkedAdvisorId ?? "ADMIN",
            ReviewedAt = DateTime.UtcNow,
        };
        _db.RegistrationReviews.Add(review);
        await _db.SaveChangesAsync(ct);

        return Ok(new ReviewDto(review.Id, review.Decision, review.Note, review.ReviewedByAdvisorId, review.ReviewedAt));
    }

    // Bir ders yuvası, yalnızca seçimdeki BAŞKA bir dersin yuvasıyla gerçekten kesişiyorsa kırmızı olur
    // (aynı derste çakışmayan ikinci yuva boşuna işaretlenmesin).
    private static bool OverlapsOther(
        ObisScheduleSlot slot, ObisRegistration self,
        IReadOnlyList<ObisRegistration> selected, IReadOnlyList<ObisScheduleSlot> schedule)
    {
        return selected
            .Where(o => o.CourseCode != self.CourseCode)
            .SelectMany(o => schedule.Where(s => s.CourseCode == o.CourseCode && s.Section == o.Section))
            .Any(o => o.Day == slot.Day && slot.StartTime < o.EndTime && o.StartTime < slot.EndTime);
    }
}
