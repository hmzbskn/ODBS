using Advisor.Application.Common;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Application.Analytics;

public interface IAnalyticsService
{
    Task<RiskDetail> GetRiskAsync(string studentNo, CancellationToken ct = default);
    Task<GraduationAnalysis> GetGraduationAsync(string studentNo, CancellationToken ct = default);
    Task<List<RiskRow>> GetRiskListAsync(IReadOnlyList<ObisStudent> scope, CancellationToken ct = default);
    Task<DashboardResponse> GetDashboardAsync(IReadOnlyList<ObisStudent> scope, CancellationToken ct = default);
}

/// <summary>Risk, mezuniyet ve panel hesaplarını OBİS verisinden üretir. Erişim denetimi çağıran
/// katmandadır (kapsam listesi ve <c>IStudentAccessGuard</c>); bu sınıf yalnızca hesaplar.</summary>
public class AnalyticsService : IAnalyticsService
{
    private const int MaxParallelism = 8;

    private readonly IObisClient _obis;
    private readonly RiskOptions _options;

    public AnalyticsService(IObisClient obis, RiskOptions options)
    {
        _obis = obis;
        _options = options;
    }

    private sealed record StudentData(
        ObisStudent Student,
        IReadOnlyList<ObisTranscriptEntry> Transcript,
        IReadOnlyList<ObisRegistration> Registrations,
        IReadOnlyList<ObisAttendance> Attendance,
        int InternshipDays)
    {
        public int CurrentTerm => RegistrationValidationService.ResolveCurrentTerm(Registrations);

        public IReadOnlyList<ObisRegistration> CurrentRegistrations =>
            Registrations.Where(r => r.Term == CurrentTerm && r.ApprovalStatus == "Beklemede").ToList();
    }

    public async Task<RiskDetail> GetRiskAsync(string studentNo, CancellationToken ct = default)
    {
        var data = await LoadAsync(studentNo, ct);
        var assessment = Assess(data);
        return new RiskDetail(
            studentNo, FullName(data.Student), data.Student.ClassYear, Gpa(data), data.CurrentTerm,
            assessment.Score, assessment.Level, assessment.Indicators);
    }

    public async Task<GraduationAnalysis> GetGraduationAsync(string studentNo, CancellationToken ct = default)
    {
        var data = await LoadAsync(studentNo, ct);
        var courses = await _obis.GetCoursesAsync(ct);
        var curriculum = await _obis.GetCurriculumAsync(ct);
        return Graduation(data, courses, curriculum);
    }

    public async Task<List<RiskRow>> GetRiskListAsync(IReadOnlyList<ObisStudent> scope, CancellationToken ct = default)
    {
        var all = await LoadManyAsync(scope, ct);
        return all
            .Select(d =>
            {
                var a = Assess(d);
                return new RiskRow(
                    d.Student.StudentNo, FullName(d.Student), d.Student.ClassYear, Gpa(d), d.CurrentTerm,
                    a.Score, a.Level, a.Indicators.Select(i => i.Title).ToList());
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.StudentNo, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<DashboardResponse> GetDashboardAsync(IReadOnlyList<ObisStudent> scope, CancellationToken ct = default)
    {
        var all = await LoadManyAsync(scope, ct);
        var courses = await _obis.GetCoursesAsync(ct);
        var curriculum = await _obis.GetCurriculumAsync(ct);

        var withHistory = all.Where(d => d.Transcript.Count > 0).ToList();
        var avgGpa = withHistory.Count == 0 ? 0 : Math.Round(withHistory.Average(Gpa), 2);

        var levels = all.Select(d => Assess(d).Level).ToList();
        var risk = new RiskDistribution(
            levels.Count(l => l == RiskLevel.Dusuk), levels.Count(l => l == RiskLevel.Orta), levels.Count(l => l == RiskLevel.Yuksek));

        var trend = all
            .SelectMany(d => RiskCalculator.TermGpas(d.Transcript))
            .GroupBy(t => t.Term)
            .OrderBy(g => g.Key)
            .Select(g => new TermGpaPoint(g.Key, Math.Round(g.Average(x => x.Gpa), 2), g.Count()))
            .ToList();

        var courseNames = courses.ToDictionary(c => c.CourseCode, c => c.CourseName);
        var success = all
            .SelectMany(d => d.Transcript)
            .GroupBy(t => t.CourseCode)
            .Select(g => new CourseSuccess(
                g.Key, courseNames.GetValueOrDefault(g.Key, g.First().CourseName), g.Count(), g.Count(t => t.Passed),
                Math.Round(g.Count(t => t.Passed) * 100.0 / g.Count(), 1)))
            .Where(c => c.Attempts >= 5)
            .OrderBy(c => c.SuccessRate)
            .ThenByDescending(c => c.Attempts)
            .ThenBy(c => c.CourseCode, StringComparer.Ordinal)
            .Take(12)
            .ToList();

        var candidates = all.Count(d =>
        {
            var g = Graduation(d, courses, curriculum);
            return g.CanGraduate || g.EstimatedRemainingTerms <= 1;
        });

        return new DashboardResponse(all.Count, avgGpa, risk, trend, success, candidates);
    }

    // ---- yardımcılar ----

    private RiskAssessment Assess(StudentData d) =>
        RiskCalculator.Assess(d.Student.StudentNo, d.Transcript, d.Attendance, d.CurrentTerm, _options);

    private GraduationAnalysis Graduation(StudentData d, IReadOnlyList<ObisCourse> courses, ObisCurriculum curriculum) =>
        GraduationAnalyzer.Analyze(
            d.Student, d.Transcript, d.CurrentRegistrations, courses, curriculum, d.InternshipDays, d.CurrentTerm, _options);

    private static double Gpa(StudentData d) => RegistrationValidationService.ComputeGpa(d.Transcript);

    private static string FullName(ObisStudent s) => $"{s.FirstName} {s.LastName}";

    private async Task<StudentData> LoadAsync(string studentNo, CancellationToken ct)
    {
        var student = await _obis.GetStudentAsync(studentNo, ct)
            ?? throw AppException.NotFound("Öğrenci bulunamadı.");
        return await LoadAsync(student, ct);
    }

    private async Task<StudentData> LoadAsync(ObisStudent student, CancellationToken ct)
    {
        var transcript = _obis.GetTranscriptAsync(student.StudentNo, ct);
        var registrations = _obis.GetRegistrationsAsync(student.StudentNo, null, ct);
        var attendance = _obis.GetAttendanceAsync(student.StudentNo, null, ct);
        var internship = _obis.GetInternshipAsync(student.StudentNo, ct);
        await Task.WhenAll(transcript, registrations, attendance, internship);
        return new StudentData(student, transcript.Result, registrations.Result, attendance.Result, internship.Result.CompletedDays);
    }

    private async Task<List<StudentData>> LoadManyAsync(IReadOnlyList<ObisStudent> scope, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(MaxParallelism);
        var tasks = scope.Select(async s =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await LoadAsync(s, ct);
            }
            finally
            {
                gate.Release();
            }
        });
        return (await Task.WhenAll(tasks)).OrderBy(d => d.Student.StudentNo, StringComparer.Ordinal).ToList();
    }
}

public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddAnalytics(this IServiceCollection services, RiskOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        return services;
    }
}
