using System.Globalization;
using Advisor.Application.Obis;
using Advisor.Application.Registration;
using Advisor.Domain.Enums;

namespace Advisor.Application.Analytics;

/// <summary>Kurala dayalı akademik risk puanı (saf; veri çekmez). Her gösterge bağımsızdır ve
/// neden puan verdiğini açıklar; toplam 100 ile sınırlanır. Makine öğrenmesi yoktur: danışman
/// her puanın gerekçesini görebilir ve eşikler appsettings.json'dan ayarlanabilir.</summary>
public static class RiskCalculator
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <param name="transcript">Öğrencinin tüm geçmiş dönem kayıtları.</param>
    /// <param name="attendance">Öğrencinin devamsızlık kayıtları (yalnızca <paramref name="currentTerm"/> kullanılır).</param>
    /// <param name="currentTerm">Öğrencinin içinde bulunduğu dönem (kayıtlardan bulunur); bilinmiyorsa 0.</param>
    public static RiskAssessment Assess(
        string studentNo,
        IReadOnlyList<ObisTranscriptEntry> transcript,
        IReadOnlyList<ObisAttendance> attendance,
        int currentTerm,
        RiskOptions o)
    {
        var indicators = new List<RiskIndicator>();

        AddGpa(indicators, transcript, o);
        AddGpaDrop(indicators, transcript, o);
        AddFailures(indicators, transcript, o);
        AddAttendance(indicators, attendance, currentTerm, o);
        AddDuration(indicators, currentTerm, o);

        var score = Math.Min(100, indicators.Sum(i => i.Points));
        var level = score >= o.HighRiskScore ? RiskLevel.Yuksek : score >= o.MediumRiskScore ? RiskLevel.Orta : RiskLevel.Dusuk;
        return new RiskAssessment(studentNo, score, level, indicators);
    }

    private static void AddGpa(List<RiskIndicator> list, IReadOnlyList<ObisTranscriptEntry> transcript, RiskOptions o)
    {
        if (transcript.Count == 0) return;

        var gpa = RegistrationValidationService.ComputeGpa(transcript);
        if (gpa < o.LowGpaThreshold)
        {
            list.Add(new RiskIndicator("LOW_GPA", "Düşük GANO", o.LowGpaPoints,
                $"GANO {Fmt(gpa)}, alt sınır olan {Fmt(o.LowGpaThreshold)} değerinin altında."));
        }
        else if (gpa < o.WatchGpaThreshold)
        {
            list.Add(new RiskIndicator("WATCH_GPA", "GANO sınırda", o.WatchGpaPoints,
                $"GANO {Fmt(gpa)}; {Fmt(o.LowGpaThreshold)} sınırına yakın ({Fmt(o.WatchGpaThreshold)} altında)."));
        }
    }

    private static void AddGpaDrop(List<RiskIndicator> list, IReadOnlyList<ObisTranscriptEntry> transcript, RiskOptions o)
    {
        var terms = TermGpas(transcript);
        if (terms.Count < 2) return;

        var previous = terms[^2];
        var last = terms[^1];
        var drop = previous.Gpa - last.Gpa;
        if (drop >= o.GpaDropSevere)
        {
            list.Add(new RiskIndicator("GPA_DROP", "Dönem ortalamasında sert düşüş", o.GpaDropSeverePoints,
                $"{previous.Term}. dönem ortalaması {Fmt(previous.Gpa)} iken {last.Term}. dönemde {Fmt(last.Gpa)}'e düştü (−{Fmt(drop)})."));
        }
        else if (drop >= o.GpaDropWarn)
        {
            list.Add(new RiskIndicator("GPA_DROP", "Dönem ortalamasında düşüş", o.GpaDropPoints,
                $"{previous.Term}. dönem ortalaması {Fmt(previous.Gpa)} iken {last.Term}. dönemde {Fmt(last.Gpa)}'e düştü (−{Fmt(drop)})."));
        }
    }

    private static void AddFailures(List<RiskIndicator> list, IReadOnlyList<ObisTranscriptEntry> transcript, RiskOptions o)
    {
        // Aynı dersten birden fazla kez kalma.
        var repeated = transcript
            .Where(t => !t.Passed)
            .GroupBy(t => t.CourseCode)
            .Where(g => g.Count() >= 2)
            .Select(g => g.First())
            .ToList();
        if (repeated.Count > 0)
        {
            var points = Math.Min(o.RepeatedFailMaxPoints, repeated.Count * o.RepeatedFailPointsEach);
            list.Add(new RiskIndicator("REPEATED_FAIL", "Aynı dersten tekrar tekrar kalma", points,
                $"Birden fazla kez kalınan ders(ler): {string.Join(", ", repeated.Select(t => $"{t.CourseCode} ({t.CourseName})"))}."));
        }

        // Art arda iki dönemde ders kaldı.
        var byTerm = transcript.GroupBy(t => t.Term).OrderBy(g => g.Key).ToList();
        if (byTerm.Count >= 2)
        {
            var a = byTerm[^2];
            var b = byTerm[^1];
            if (a.Any(t => !t.Passed) && b.Any(t => !t.Passed))
            {
                list.Add(new RiskIndicator("CONSECUTIVE_FAIL_TERMS", "Üst üste iki dönem ders kaldı", o.ConsecutiveFailTermsPoints,
                    $"{a.Key}. ve {b.Key}. dönemlerin ikisinde de başarısız ders var."));
            }
        }
    }

    private static void AddAttendance(List<RiskIndicator> list, IReadOnlyList<ObisAttendance> attendance, int currentTerm, RiskOptions o)
    {
        var current = attendance.Where(a => a.Term == currentTerm && a.TotalSessions > 0).ToList();
        if (current.Count == 0) return;

        var exceeded = current.Where(a => Rate(a) >= o.AbsenceLimitRate).ToList();
        var near = current.Where(a => Rate(a) >= o.AbsenceWarnRate && Rate(a) < o.AbsenceLimitRate).ToList();

        if (exceeded.Count > 0)
        {
            list.Add(new RiskIndicator("ABSENCE_LIMIT", "Devamsızlık sınırı aşıldı", o.AbsenceLimitPoints,
                $"Devamsızlık oranı %{Pct(o.AbsenceLimitRate)} sınırında ya da üzerinde: {Describe(exceeded)}."));
        }
        else if (near.Count > 0)
        {
            list.Add(new RiskIndicator("ABSENCE_NEAR", "Devamsızlık sınırına yaklaşıyor", o.AbsenceWarnPoints,
                $"Devamsızlık oranı %{Pct(o.AbsenceWarnRate)} üzerinde (sınır %{Pct(o.AbsenceLimitRate)}): {Describe(near)}."));
        }
    }

    private static void AddDuration(List<RiskIndicator> list, int currentTerm, RiskOptions o)
    {
        if (currentTerm <= o.NormalTerms) return;

        if (currentTerm >= o.MaxTerms - 1)
        {
            list.Add(new RiskIndicator("MAX_DURATION", "Azami öğrenim süresine yaklaşıyor", o.NearMaxPoints,
                $"{currentTerm}. dönemde; azami süre {o.MaxTerms} dönem."));
            return;
        }

        var extra = currentTerm - o.NormalTerms;
        var points = Math.Min(o.OverNormalMaxPoints, o.OverNormalBasePoints + (extra - 1) * o.OverNormalPointsPerTerm);
        list.Add(new RiskIndicator("OVER_NORMAL_DURATION", "Normal öğrenim süresi aşıldı", points,
            $"{currentTerm}. dönemde; normal süre {o.NormalTerms} dönem, azami süre {o.MaxTerms} dönem."));
    }

    /// <summary>Her dönemin kredi ağırlıklı dönem ortalaması, dönem sırasına göre.</summary>
    public static List<(int Term, double Gpa)> TermGpas(IEnumerable<ObisTranscriptEntry> transcript) =>
        transcript
            .GroupBy(t => t.Term)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var credits = g.Sum(t => t.Credit);
                var gpa = credits == 0 ? 0 : g.Sum(t => GradeScale.PointsOf(t.LetterGrade) * t.Credit) / credits;
                return (g.Key, Math.Round(gpa, 2));
            })
            .ToList();

    private static double Rate(ObisAttendance a) => (double)(a.TotalSessions - a.AttendedSessions) / a.TotalSessions;

    private static string Describe(IEnumerable<ObisAttendance> items) =>
        string.Join(", ", items.Select(a => $"{a.CourseCode} (%{Pct(Rate(a))}, {a.TotalSessions - a.AttendedSessions}/{a.TotalSessions} ders)"));

    private static string Pct(double rate) => Math.Round(rate * 100).ToString("0", Tr);
    private static string Fmt(double value) => value.ToString("0.00", Tr);
}
