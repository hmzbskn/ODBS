namespace Advisor.Application.Registration.Rules;

/// <summary>Öğrenci yalnızca kendi öğretim türünün şubesinden ders alabilir: normal öğretim öğrencisi "NÖ" ya da
/// "NÖ/İÖ", ikinci öğretim öğrencisi "İÖ" ya da "NÖ/İÖ". Aykırı şube seçimi Hata.</summary>
public class SectionProgramRule : IRegistrationRule
{
    public const string RuleCode = "SECTION_PROGRAM";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        foreach (var reg in ctx.Selected.Where(r => !ctx.SectionAllowed(r.Section)))
        {
            yield return new RegistrationIssue(
                Code, IssueSeverity.Error,
                $"{ctx.Name(reg.CourseCode)} {reg.Section} şubesinden seçilmiş; " +
                $"{EducationType.Describe(ctx.Student.Program)} öğrencisi yalnızca {EducationType.OwnSection(ctx.Student.Program)} " +
                "ya da NÖ/İÖ şubelerinden ders alabilir.",
                new[] { reg.CourseCode });
        }
    }
}
