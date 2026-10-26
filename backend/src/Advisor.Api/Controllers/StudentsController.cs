using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

public record StudentListRow(
    string StudentNo, string FullName, int ClassYear, string Program, double Gpa, string AdvisorName, string Status);

public record StudentSchoolInfo(
    string StudentNo, string FullName, string Department, int ClassYear, int Term, string Program, string Status);

public record StudentContactInfo(string? Email, string? Phone, string? Residence);

public record StudentAcademicInfo(
    double Gpa, int EarnedEcts, int RequiredEcts, int RemainingEcts, int CurrentTermCourseCount);

public record StudentProfileDetail(
    StudentSchoolInfo School, StudentContactInfo Contact, StudentAcademicInfo Academic, AdvisorContactDto? Advisor);

/// <summary>Danışman / yönetim için Öğrenciler sekmesi: görebildiği öğrencilerin listesi ve tek öğrencinin
/// okul, iletişim ve akademik özeti. Kapsam <see cref="IStudentDirectory"/> ve erişim bekçisiyle sınırlıdır.</summary>
[ApiController]
[Route("api/students")]
[Authorize(Roles = nameof(UserRole.Danisman) + "," + ManagementController.Roles)]
public class StudentsController : ControllerBase
{
    private readonly IStudentDirectory _students;
    private readonly IObisClient _obis;
    private readonly IAnalyticsService _analytics;
    private readonly IStudentAccessGuard _guard;

    public StudentsController(IStudentDirectory students, IObisClient obis, IAnalyticsService analytics, IStudentAccessGuard guard)
    {
        _students = students;
        _obis = obis;
        _analytics = analytics;
        _guard = guard;
    }

    [HttpGet]
    public async Task<ActionResult<List<StudentListRow>>> List(CancellationToken ct)
    {
        var students = await _students.GetVisibleStudentsAsync(null, ct);
        var advisors = (await _obis.GetAdvisorsAsync(ct)).ToDictionary(a => a.AdvisorId);
        var rows = new List<StudentListRow>();
        foreach (var s in students.OrderBy(s => s.ClassYear).ThenBy(s => s.StudentNo))
        {
            var gpa = RegistrationValidationService.ComputeGpa(await _obis.GetTranscriptAsync(s.StudentNo, ct));
            rows.Add(new StudentListRow(
                s.StudentNo, $"{s.FirstName} {s.LastName}", s.ClassYear, s.Program ?? "-", gpa,
                advisors.TryGetValue(s.AdvisorId, out var a) ? $"{a.Title} {a.FirstName} {a.LastName}" : "-", s.Status));
        }
        return rows;
    }

    [HttpGet("{studentNo}")]
    public async Task<ActionResult<StudentProfileDetail>> Detail(string studentNo, CancellationToken ct)
    {
        if (!await _guard.CanAccessStudentAsync(studentNo, ct)) return Forbid();
        var student = await _students.GetStudentAsync(studentNo, ct);
        if (student is null) return NotFound(new { message = "Öğrenci bulunamadı." });

        var graduation = await _analytics.GetGraduationAsync(studentNo, ct);
        var registrations = await _obis.GetRegistrationsAsync(studentNo, graduation.CurrentTerm, ct);
        var advisor = (await _obis.GetAdvisorsAsync(ct)).FirstOrDefault(a => a.AdvisorId == student.AdvisorId);

        return new StudentProfileDetail(
            new StudentSchoolInfo(
                student.StudentNo, $"{student.FirstName} {student.LastName}", student.Department, student.ClassYear,
                graduation.CurrentTerm, student.Program ?? "-", student.Status),
            new StudentContactInfo(student.Email, student.Phone, student.Residence),
            new StudentAcademicInfo(
                graduation.Gpa, graduation.EarnedEcts, graduation.RequiredEcts,
                Math.Max(graduation.RequiredEcts - graduation.EarnedEcts, 0),
                registrations.Select(r => r.CourseCode).Distinct().Count()),
            advisor is null
                ? null
                : new AdvisorContactDto(
                    $"{advisor.FirstName} {advisor.LastName}", advisor.Title,
                    RegistrationsController.AdvisorEmail(advisor.FirstName, advisor.LastName)));
    }
}
