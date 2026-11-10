using Advisor.Application.Obis;

namespace Advisor.Application.Registration.Rules;

/// <summary>Seçilen derslerin şubeleri aynı gün ve kesişen saat aralığında ise Hata.
/// <list type="bullet">
/// <item>İki dersten en az birinde devam şartı yoksa (<see cref="ObisRegistration.AttendanceRequired"/> false)
/// öğrenci iki dersi de takip edebilir; çakışma sayılmaz.</item>
/// <item>Çakışmada alttan alınan (daha önce kalınmış) ders temeldir ve kesinlikle alınmak zorundadır;
/// çakışan üstten dersin yerine başka saatteki şubesi önerilir. İkisi de alttan veya ikisi de üstten ise
/// alt dönem dersi temel sayılır.</item>
/// </list></summary>
public class TimeConflictRule : IRegistrationRule
{
    public const string RuleCode = "TIME_CONFLICT";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        var selected = ctx.Selected.ToList();

        for (var i = 0; i < selected.Count; i++)
        {
            for (var j = i + 1; j < selected.Count; j++)
            {
                var a = selected[i];
                var b = selected[j];
                if (a.CourseCode == b.CourseCode) continue;
                if (!a.AttendanceRequired || !b.AttendanceRequired) continue;

                var overlap = FirstOverlap(ctx.SlotsFor(a), ctx.SlotsFor(b));
                if (overlap is null) continue;

                var (baseReg, upperReg) = Priority(ctx, a, b);
                var (x, y) = overlap.Value;
                var (baseSlot, upperSlot) = ReferenceEquals(baseReg, a) ? (x, y) : (y, x);

                yield return new RegistrationIssue(
                    Code, IssueSeverity.Error,
                    $"{ctx.Name(baseReg.CourseCode)} ile {ctx.Name(upperReg.CourseCode)} aynı saatte " +
                    $"({GunAdi(baseSlot.Day)} {Later(baseSlot.StartTime, upperSlot.StartTime):HH\\:mm}). " +
                    $"{ctx.Name(baseReg.CourseCode)} {BaseReason(ctx, baseReg)}; {ctx.Name(upperReg.CourseCode)} için {Suggest(ctx, upperReg)}",
                    new[] { baseReg.CourseCode, upperReg.CourseCode });
            }
        }
    }

    private static (ObisScheduleSlot, ObisScheduleSlot)? FirstOverlap(
        IEnumerable<ObisScheduleSlot> left, IEnumerable<ObisScheduleSlot> right)
    {
        var rightList = right.ToList();
        foreach (var l in left)
        {
            foreach (var r in rightList)
            {
                if (Overlaps(l, r)) return (l, r);
            }
        }

        return null;
    }

    private static bool Overlaps(ObisScheduleSlot a, ObisScheduleSlot b) =>
        a.Day == b.Day && a.StartTime < b.EndTime && b.StartTime < a.EndTime;

    /// <summary>Daha önce kalınmış ve hâlâ geçilmemiş ders (alttan ders).</summary>
    private static bool IsRetake(RegistrationContext ctx, ObisRegistration r) =>
        ctx.History.Any(h => h.CourseCode == r.CourseCode) && !ctx.IsPassed(r.CourseCode);

    private static (ObisRegistration Base, ObisRegistration Upper) Priority(
        RegistrationContext ctx, ObisRegistration a, ObisRegistration b)
    {
        var aRetake = IsRetake(ctx, a);
        var bRetake = IsRetake(ctx, b);
        if (aRetake != bRetake) return aRetake ? (a, b) : (b, a);

        var aTerm = ctx.Courses.TryGetValue(a.CourseCode, out var ca) ? ca.Term : int.MaxValue;
        var bTerm = ctx.Courses.TryGetValue(b.CourseCode, out var cb) ? cb.Term : int.MaxValue;
        return bTerm < aTerm ? (b, a) : (a, b);
    }

    private static string BaseReason(RegistrationContext ctx, ObisRegistration r) =>
        IsRetake(ctx, r) ? "alttan alındığı için zorunlu" : "alt dönem dersi olduğu için öncelikli";

    private static TimeOnly Later(TimeOnly a, TimeOnly b) => a > b ? a : b;

    /// <summary>Üstten dersin, öğrencinin öğretim türüne uygun ve diğer (devam şartı olan) seçimleriyle çakışmayan diğer şubeleri.</summary>
    private static string Suggest(RegistrationContext ctx, ObisRegistration upper)
    {
        var others = ctx.Selected
            .Where(r => r.CourseCode != upper.CourseCode && r.AttendanceRequired)
            .SelectMany(ctx.SlotsFor)
            .ToList();

        var alternatives = ctx.Schedule
            .Where(s => s.CourseCode == upper.CourseCode && s.Section != upper.Section && ctx.SectionAllowed(s.Section))
            .GroupBy(s => s.Section)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Where(g => !g.Any(slot => others.Any(o => Overlaps(slot, o))))
            .Select(g => $"{g.Key} şubesi ({string.Join(", ", g.Select(s => $"{GunAdi(s.Day)} {s.StartTime:HH\\:mm}-{s.EndTime:HH\\:mm}"))})")
            .ToList();

        return alternatives.Count == 0
            ? "alternatif şube bulunamadı."
            : $"{string.Join(" ya da ", alternatives)} önerilir.";
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
