namespace Advisor.Application.Registration.Rules;

/// <summary>Kalınmış ya da hiç alınmamış zorunlu alt dönem dersi varken, seçimde o dersler
/// olmadan üst dönem dersi seçilmişse Uyarı. Seçmeli dersler kapsam dışıdır (müfredatın
/// zorunlu ders listesine göre değerlendirilir).</summary>
public class LowerSemesterPriorityRule : IRegistrationRule
{
    public const string RuleCode = "LOWER_SEMESTER_PRIORITY";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        var selectedCodes = ctx.Selected.Select(r => r.CourseCode).ToHashSet();

        var unmet = ctx.Curriculum.MandatoryCourseCodes
            .Where(code => ctx.Courses.ContainsKey(code))
            .Where(code => !ctx.IsPassed(code) && !selectedCodes.Contains(code))
            // Dersler yalnızca kendi yarıyılında (güz: tek, bahar: çift dönem) açılır; diğer yarıyılın dersi bu dönem seçilemez.
            .Where(code => ctx.Courses[code].Term % 2 == ctx.Term % 2)
            .ToList();
        if (unmet.Count == 0) yield break;

        var failed = unmet.Where(code => ctx.History.Any(h => h.CourseCode == code)).ToList();
        var neverTaken = unmet.Except(failed).ToList();

        if (failed.Count > 0)
        {
            var issue = Build(ctx, failed, "kalınmış");
            if (issue is not null) yield return issue;
        }

        if (neverTaken.Count > 0)
        {
            var issue = Build(ctx, neverTaken, "hiç alınmamış");
            if (issue is not null) yield return issue;
        }
    }

    private RegistrationIssue? Build(RegistrationContext ctx, List<string> lowerCodes, string kind)
    {
        var lowestTerm = lowerCodes.Min(c => ctx.Courses[c].Term);
        var higher = ctx.Selected
            .Select(r => r.CourseCode)
            .Where(c => ctx.Courses.TryGetValue(c, out var course) && course.Term > lowestTerm)
            .ToList();
        if (higher.Count == 0) return null;

        return new RegistrationIssue(
            Code, IssueSeverity.Warning,
            kind == "kalınmış"
                ? $"Alttan alınması gereken kalınmış ders(ler) seçilmemiş: {ctx.Names(lowerCodes)}."
                : $"Alt dönemden hiç alınmamış zorunlu ders(ler) var: {ctx.Names(lowerCodes)}.",
            lowerCodes.Concat(higher).ToList());
    }
}
