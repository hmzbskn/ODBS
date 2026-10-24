using Advisor.Application.Obis;
using Advisor.Application.Registration.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Application.Registration;

/// <summary>Kuralları çalıştıran saf motor: bağlamı alır, tüm <see cref="IRegistrationRule"/>
/// bulgularını birleştirir (hata → uyarı → bilgi sırasıyla).</summary>
public class RegistrationValidator
{
    private readonly IEnumerable<IRegistrationRule> _rules;

    public RegistrationValidator(IEnumerable<IRegistrationRule> rules) => _rules = rules;

    public RegistrationValidationResult Validate(RegistrationContext ctx)
    {
        var issues = _rules
            .SelectMany(r => r.Evaluate(ctx))
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleCode, StringComparer.Ordinal)
            .ToList();

        return new RegistrationValidationResult
        {
            Student = ctx.Student,
            Term = ctx.Term,
            Gpa = ctx.Gpa,
            TotalEcts = CreditLimitRule.TotalEcts(ctx),
            MaxEcts = CreditLimitRule.MaxEctsFor(ctx),
            Selected = ctx.Selected,
            Issues = issues,
        };
    }
}

public interface IRegistrationValidationService
{
    /// <summary>Öğrencinin ders seçimini OBİS'ten çekip doğrular. <paramref name="term"/>
    /// verilmezse bekleyen (onaylanmamış) kaydın dönemi, o da yoksa son dönem kullanılır.
    /// Öğrenci OBİS'te yoksa null.</summary>
    Task<RegistrationValidationResult?> ValidateAsync(string studentNo, int? term = null, CancellationToken ct = default);
}

public class RegistrationValidationService : IRegistrationValidationService
{
    private const string PendingStatus = "Beklemede";

    private readonly IObisClient _obis;
    private readonly RegistrationValidator _validator;
    private readonly RegistrationRuleOptions _options;

    public RegistrationValidationService(IObisClient obis, RegistrationValidator validator, RegistrationRuleOptions options)
    {
        _obis = obis;
        _validator = validator;
        _options = options;
    }

    public async Task<RegistrationValidationResult?> ValidateAsync(string studentNo, int? term = null, CancellationToken ct = default)
    {
        var student = await _obis.GetStudentAsync(studentNo, ct);
        if (student is null) return null;

        var allRegistrations = await _obis.GetRegistrationsAsync(studentNo, null, ct);
        var resolvedTerm = term ?? ResolveCurrentTerm(allRegistrations);

        var transcript = await _obis.GetTranscriptAsync(studentNo, ct);
        var courses = await _obis.GetCoursesAsync(ct);
        var schedule = await _obis.GetScheduleAsync(null, ct);
        var exams = await _obis.GetExamsAsync(null, ct);
        var curriculum = await _obis.GetCurriculumAsync(ct);

        var ctx = BuildContext(student, resolvedTerm, allRegistrations, transcript, courses, schedule, exams, curriculum, _options);
        return _validator.Validate(ctx);
    }

    internal static int ResolveCurrentTerm(IReadOnlyList<ObisRegistration> registrations)
    {
        var pending = registrations.Where(r => r.ApprovalStatus == PendingStatus).Select(r => r.Term).ToList();
        if (pending.Count > 0) return pending.Max();
        return registrations.Count > 0 ? registrations.Max(r => r.Term) : 0;
    }

    public static RegistrationContext BuildContext(
        ObisStudent student, int term,
        IEnumerable<ObisRegistration> registrations,
        IEnumerable<ObisTranscriptEntry> transcript,
        IEnumerable<ObisCourse> courses,
        IEnumerable<ObisScheduleSlot> schedule,
        IEnumerable<ObisExamSlot> exams,
        ObisCurriculum curriculum,
        RegistrationRuleOptions options)
    {
        var history = transcript.Where(t => t.Term < term).ToList();
        var selected = registrations
            .Where(r => r.Term == term)
            .GroupBy(r => r.CourseCode)
            .Select(g => g.First())
            .ToList();

        return new RegistrationContext
        {
            Student = student,
            Term = term,
            History = history,
            Selected = selected,
            Courses = courses.ToDictionary(c => c.CourseCode),
            Schedule = schedule.ToList(),
            Exams = exams.ToList(),
            Curriculum = curriculum,
            Options = options,
            Gpa = ComputeGpa(history),
        };
    }

    /// <summary>Kredi ağırlıklı GANO; tekrar edilen derste son deneme (en yüksek dönem) geçerli.</summary>
    public static double ComputeGpa(IEnumerable<ObisTranscriptEntry> history)
    {
        var latest = history
            .GroupBy(h => h.CourseCode)
            .Select(g => g.OrderByDescending(h => h.Term).First())
            .ToList();
        var credits = latest.Sum(h => h.Credit);
        if (credits == 0) return 0;
        return Math.Round(latest.Sum(h => GradeScale.PointsOf(h.LetterGrade) * h.Credit) / credits, 2);
    }
}

public static class RegistrationServiceCollectionExtensions
{
    /// <summary>Bu derlemedeki tüm <see cref="IRegistrationRule"/> sınıflarını kaydeder;
    /// yeni kural eklemek için başka kayıt gerekmez.</summary>
    public static IServiceCollection AddRegistrationRules(this IServiceCollection services, RegistrationRuleOptions options)
    {
        var ruleTypes = typeof(IRegistrationRule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRegistrationRule).IsAssignableFrom(t));
        foreach (var type in ruleTypes)
        {
            services.AddSingleton(typeof(IRegistrationRule), type);
        }

        services.AddSingleton(options);
        services.AddSingleton<RegistrationValidator>();
        services.AddScoped<IRegistrationValidationService, RegistrationValidationService>();
        return services;
    }
}
