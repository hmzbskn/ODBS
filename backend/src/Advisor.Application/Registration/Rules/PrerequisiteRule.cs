namespace Advisor.Application.Registration.Rules;

/// <summary>Önkoşulu geçilmemiş ders seçimi Hata. Aynı dönemde birlikte alınan önkoşul
/// "geçilmiş" sayılmaz.</summary>
public class PrerequisiteRule : IRegistrationRule
{
    public const string RuleCode = "PREREQUISITE";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        foreach (var reg in ctx.Selected)
        {
            if (!ctx.Courses.TryGetValue(reg.CourseCode, out var course)) continue;

            var missing = course.Prerequisites.Where(p => !ctx.IsPassed(p)).ToList();
            if (missing.Count == 0) continue;

            yield return new RegistrationIssue(
                Code, IssueSeverity.Error,
                $"{ctx.Label(reg.CourseCode)} dersinin önkoşulu geçilmemiş: {ctx.Labels(missing)}.",
                new[] { reg.CourseCode }.Concat(missing).ToList());
        }
    }
}
