using System.Globalization;
using Advisor.Application.Obis;

namespace Advisor.Application.Analytics;

/// <summary>Müfredata göre mezuniyet analizi (saf). Koşullar: tüm zorunlu dersler, seçmeli kredi,
/// toplam AKTS, staj günü ve asgari GANO. Bu dönem alınan (henüz sonuçlanmamış) dersler eksik sayılır
/// ancak "bu dönem alıyor" olarak işaretlenir.</summary>
public static class GraduationAnalyzer
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static GraduationAnalysis Analyze(
        ObisStudent student,
        IReadOnlyList<ObisTranscriptEntry> transcript,
        IReadOnlyList<ObisRegistration> currentTermRegistrations,
        IReadOnlyList<ObisCourse> courses,
        ObisCurriculum curriculum,
        int internshipDays,
        int currentTerm,
        RiskOptions o)
    {
        var courseByCode = courses.ToDictionary(c => c.CourseCode);
        var mandatory = curriculum.MandatoryCourseCodes.Distinct().ToList();

        // Geçilmiş dersler (tekrarlarda bir kez sayılır).
        var passed = transcript
            .Where(t => t.Passed)
            .GroupBy(t => t.CourseCode)
            .ToDictionary(g => g.Key, g => g.First());
        var attempted = transcript.Select(t => t.CourseCode).ToHashSet();
        var inProgress = currentTermRegistrations.Select(r => r.CourseCode).ToHashSet();

        var missing = new List<MissingCourse>();
        foreach (var code in mandatory.Where(c => !passed.ContainsKey(c)))
        {
            courseByCode.TryGetValue(code, out var course);
            var status = inProgress.Contains(code) ? "InProgress" : attempted.Contains(code) ? "Failed" : "NotTaken";
            missing.Add(new MissingCourse(code, course?.CourseName ?? code, course?.Term ?? 0, course?.Ects ?? 0, status));
        }
        missing = missing.OrderBy(m => m.Term).ThenBy(m => m.CourseCode, StringComparer.Ordinal).ToList();

        var mandatorySet = mandatory.ToHashSet();
        var electives = passed.Values
            .Where(t => !mandatorySet.Contains(t.CourseCode))
            .Select(t => new PassedElective(t.CourseCode, t.CourseName, t.Credit, t.Ects))
            .OrderBy(e => e.CourseCode, StringComparer.Ordinal)
            .ToList();
        var electiveCredits = electives.Sum(e => e.Credit);

        var earnedEcts = passed.Values.Sum(t => t.Ects);
        var progress = curriculum.TotalEctsRequirement == 0
            ? 100
            : Math.Round(Math.Min(100.0, earnedEcts * 100.0 / curriculum.TotalEctsRequirement), 1);

        var gpa = Registration.RegistrationValidationService.ComputeGpa(transcript);
        var gpaOk = transcript.Count > 0 && gpa >= o.GraduationMinGpa;

        var gpaText = gpa.ToString("0.00", Tr);
        var minGpaText = o.GraduationMinGpa.ToString("0.00", Tr);

        List<string> Evaluate(int missingMandatory, int inProgressMandatory, int electiveCr, int ects)
        {
            var list = new List<string>();
            if (missingMandatory > 0)
            {
                list.Add($"{missingMandatory} zorunlu ders tamamlanmadı" +
                         (inProgressMandatory > 0 ? $" ({inProgressMandatory} tanesi bu dönem alınıyor)." : "."));
            }

            if (electiveCr < curriculum.ElectiveCreditRequirement)
                list.Add($"Seçmeli ders kredisi eksik: {electiveCr} / {curriculum.ElectiveCreditRequirement}.");
            if (ects < curriculum.TotalEctsRequirement)
                list.Add($"Toplam AKTS eksik: {ects} / {curriculum.TotalEctsRequirement}.");
            if (internshipDays < curriculum.InternshipDaysRequirement)
                list.Add($"Staj eksik: {internshipDays} / {curriculum.InternshipDaysRequirement} gün.");
            if (!gpaOk)
                list.Add($"GANO {gpaText}; mezuniyet için en az {minGpaText} gerekir.");
            return list;
        }

        var blockers = Evaluate(missing.Count, missing.Count(m => m.Status == "InProgress"), electiveCredits, earnedEcts);

        // Projeksiyon: bu dönem alınan dersler geçilirse hangi koşullar hâlâ eksik kalır?
        var inProgressCourses = currentTermRegistrations
            .Select(r => r.CourseCode)
            .Distinct()
            .Where(code => !passed.ContainsKey(code) && courseByCode.ContainsKey(code))
            .Select(code => courseByCode[code])
            .ToList();
        var projectedEcts = earnedEcts + inProgressCourses.Sum(c => c.Ects);
        var projectedElectiveCredits = electiveCredits + inProgressCourses.Where(c => !mandatorySet.Contains(c.CourseCode)).Sum(c => c.Credit);
        var projectedMissing = missing.Count(m => m.Status != "InProgress");
        var projectedBlockers = Evaluate(projectedMissing, 0, projectedElectiveCredits, projectedEcts);

        var canGraduate = blockers.Count == 0;
        return new GraduationAnalysis(
            student.StudentNo, $"{student.FirstName} {student.LastName}", currentTerm,
            gpa, o.GraduationMinGpa, gpaOk,
            earnedEcts, curriculum.TotalEctsRequirement, progress,
            mandatory.Count, mandatory.Count - missing.Count, missing,
            electiveCredits, curriculum.ElectiveCreditRequirement, electives,
            internshipDays, curriculum.InternshipDaysRequirement,
            canGraduate, blockers, projectedBlockers.Count == 0, projectedBlockers,
            EstimateRemainingTerms(canGraduate, earnedEcts, curriculum, missing, o));
    }

    private static int EstimateRemainingTerms(
        bool canGraduate, int earnedEcts, ObisCurriculum curriculum, List<MissingCourse> missing, RiskOptions o)
    {
        if (canGraduate) return 0;

        var missingMandatoryEcts = missing.Sum(m => m.Ects);
        var remainingEcts = Math.Max(curriculum.TotalEctsRequirement - earnedEcts, missingMandatoryEcts);
        var perTerm = Math.Max(1, o.TypicalTermEcts);
        // Yalnızca staj ya da GANO engeliyorsa bile en az bir dönem gerekir.
        return Math.Max(1, (int)Math.Ceiling(remainingEcts / (double)perTerm));
    }
}
