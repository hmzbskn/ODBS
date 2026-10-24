namespace Advisor.Application.Registration.Rules;

/// <summary>Seçilen iki dersin sınavı aynı tarihte kesişen saatlerde ise Hata.</summary>
public class ExamConflictRule : IRegistrationRule
{
    public const string RuleCode = "EXAM_CONFLICT";
    public string Code => RuleCode;

    public IEnumerable<RegistrationIssue> Evaluate(RegistrationContext ctx)
    {
        var exams = ctx.Selected
            .SelectMany(r => ctx.ExamsFor(r).Select(e => (Reg: r, Exam: e)))
            .ToList();

        for (var i = 0; i < exams.Count; i++)
        {
            for (var j = i + 1; j < exams.Count; j++)
            {
                var a = exams[i];
                var b = exams[j];
                if (a.Reg.CourseCode == b.Reg.CourseCode) continue;
                if (a.Exam.Date != b.Exam.Date) continue;
                if (a.Exam.StartTime >= b.Exam.EndTime || b.Exam.StartTime >= a.Exam.EndTime) continue;

                yield return new RegistrationIssue(
                    Code, IssueSeverity.Error,
                    $"Sınav çakışması: {ctx.Label(a.Reg.CourseCode)} ({TypeName(a.Exam.Type)}) ile " +
                    $"{ctx.Label(b.Reg.CourseCode)} ({TypeName(b.Exam.Type)}) " +
                    $"{a.Exam.Date:dd.MM.yyyy} tarihinde {a.Exam.StartTime:HH\\:mm}-{a.Exam.EndTime:HH\\:mm} / " +
                    $"{b.Exam.StartTime:HH\\:mm}-{b.Exam.EndTime:HH\\:mm} saatlerinde çakışıyor.",
                    new[] { a.Reg.CourseCode, b.Reg.CourseCode });
            }
        }
    }

    private static string TypeName(Obis.ObisExamType type) =>
        type == Obis.ObisExamType.Midterm ? "ara sınav" : "final";
}
