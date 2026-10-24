namespace Advisor.Application.Registration.Rules;

/// <summary>Seçilen derslerin toplam AKTS'si GANO'ya bağlı üst sınırı aşarsa Hata.
/// Eşikler <see cref="RegistrationRuleOptions"/> ile ayarlanır. Geçmişi olmayan (ilk dönem)
/// öğrenci için normal sınır uygulanır.</summary>
public class CreditLimitRule : IRegistrationRule
{
    public const string RuleCode = "CREDIT_LIMIT";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        var total = TotalEcts(ctx);
        var max = MaxEctsFor(ctx);
        if (total <= max) yield break;

        var band = !ctx.HasHistory ? "normal"
            : ctx.Gpa < ctx.Options.LowGpaThreshold ? "düşük GANO"
            : ctx.Gpa >= ctx.Options.HighGpaThreshold ? "yüksek GANO" : "normal";

        yield return new RegistrationIssue(
            Code, IssueSeverity.Error,
            $"AKTS limiti aşıldı: seçilen derslerin toplamı {total} AKTS, GANO {ctx.Gpa:0.00} ({band}) için üst sınır {max} AKTS.",
            ctx.Selected.Select(r => r.CourseCode).ToList());
    }

    public static int TotalEcts(RegistrationContext ctx) =>
        ctx.Selected.Where(r => ctx.Courses.ContainsKey(r.CourseCode)).Sum(r => ctx.Courses[r.CourseCode].Ects);

    public static int MaxEctsFor(RegistrationContext ctx)
    {
        var o = ctx.Options;
        if (!ctx.HasHistory) return o.NormalMaxEcts;
        if (ctx.Gpa < o.LowGpaThreshold) return o.LowGpaMaxEcts;
        if (ctx.Gpa >= o.HighGpaThreshold) return o.HighGpaMaxEcts;
        return o.NormalMaxEcts;
    }
}
