namespace Advisor.Application.Registration.Rules;

/// <summary>Daha önce yüksek notla (varsayılan BB ve üstü) geçilmiş dersin yeniden seçilmesi
/// için Bilgi. Engellemez; danışmanın dikkatini çeker.</summary>
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
                Code, IssueSeverity.Info,
                $"{ctx.Label(reg.CourseCode)} daha önce {best.Term}. dönemde {best.LetterGrade} ile geçilmiş; ders yeniden seçilmiş.",
                new[] { reg.CourseCode });
        }
    }
}
