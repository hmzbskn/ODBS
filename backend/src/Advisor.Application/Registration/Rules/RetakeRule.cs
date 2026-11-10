namespace Advisor.Application.Registration.Rules;

/// <summary>Geçilmiş ders yalnızca not yükseltmek için ve yalnızca CB ve altı notla geçildiyse tekrar alınabilir.
/// BB ve üstüyle (varsayılan eşik <see cref="RegistrationRuleOptions.RetakeHighGrade"/>) geçilmiş dersin yeniden seçilmesi Hata.</summary>
public class RetakeRule : IRegistrationRule
{
    public const string RuleCode = "RETAKE";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        foreach (var reg in ctx.Selected)
        {
            var best = ctx.History
                .Where(h => h.CourseCode == reg.CourseCode && h.Passed)
                .OrderByDescending(h => GradeScale.PointsOf(h.LetterGrade))
                .FirstOrDefault();
            if (best is null || !GradeScale.IsAtLeast(best.LetterGrade, ctx.Options.RetakeHighGrade)) continue;

            yield return new RegistrationIssue(
                Code, IssueSeverity.Error,
                $"{ctx.Name(reg.CourseCode)} daha önce {best.LetterGrade} ile geçilmiş; yalnızca CB ve altı notla geçilen dersler tekrar alınabilir.",
                new[] { reg.CourseCode });
        }
    }
}
