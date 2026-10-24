namespace Advisor.Application.Registration.Rules;

/// <summary>Seçilen derslerin şubeleri aynı gün ve kesişen saat aralığında ise Hata.</summary>
public class TimeConflictRule : IRegistrationRule
{
    public const string RuleCode = "TIME_CONFLICT";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        var entries = ctx.Selected
            .SelectMany(r => ctx.SlotsFor(r).Select(s => (Reg: r, Slot: s)))
            .ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            for (var j = i + 1; j < entries.Count; j++)
            {
                var a = entries[i];
                var b = entries[j];
                if (a.Reg.CourseCode == b.Reg.CourseCode) continue;
                if (a.Slot.Day != b.Slot.Day) continue;
                if (a.Slot.StartTime >= b.Slot.EndTime || b.Slot.StartTime >= a.Slot.EndTime) continue;

                yield return new RegistrationIssue(
                    Code, IssueSeverity.Error,
                    $"Ders saati çakışması: {ctx.Label(a.Reg.CourseCode)} ({a.Reg.Section} şubesi) ile " +
                    $"{ctx.Label(b.Reg.CourseCode)} ({b.Reg.Section} şubesi) {GunAdi(a.Slot.Day)} günü " +
                    $"{a.Slot.StartTime:HH\\:mm}-{a.Slot.EndTime:HH\\:mm} / {b.Slot.StartTime:HH\\:mm}-{b.Slot.EndTime:HH\\:mm} saatlerinde çakışıyor.",
                    new[] { a.Reg.CourseCode, b.Reg.CourseCode });
            }
        }
    }

    internal static string GunAdi(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        _ => "Pazar",
    };
}
