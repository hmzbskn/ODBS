using Advisor.Application.Obis;

namespace Advisor.Application.Registration;

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Bir kuralın bulduğu tek bir bulgu. <see cref="CourseCodes"/> ilgili dersleri
/// listeler (arayüzde çakışan dersleri kırmızı göstermek için kullanılır).</summary>
public record RegistrationIssue(
    string RuleCode,
    IssueSeverity Severity,
    string Message,
    IReadOnlyList<string> CourseCodes);

/// <summary>Kural eşikleri. appsettings.json içindeki "RegistrationRules" bölümünden okunur;
/// kod değiştirmeden ayarlanabilir.</summary>
public class RegistrationRuleOptions
{
    public const string SectionName = "RegistrationRules";

    /// <summary>GANO bu değerin altındaysa <see cref="LowGpaMaxEcts"/> uygulanır.</summary>
    public double LowGpaThreshold { get; set; } = 2.0;

    /// <summary>GANO bu değer ve üstündeyse <see cref="HighGpaMaxEcts"/> (ek kredi hakkı) uygulanır.</summary>
    public double HighGpaThreshold { get; set; } = 3.0;

    public int LowGpaMaxEcts { get; set; } = 24;
    public int NormalMaxEcts { get; set; } = 31;
    public int HighGpaMaxEcts { get; set; } = 37;

    /// <summary>Tekrar alma bilgi uyarısı için "yüksek not" eşiği (bu not ve üstü).</summary>
    public string RetakeHighGrade { get; set; } = "BB";
}

/// <summary>Bir öğrencinin bir dönemdeki ders seçimini doğrulamak için gereken tüm veri.
/// Kurallar saf fonksiyon gibi çalışır: yalnızca bu bağlamı okur, dış sisteme gitmez.</summary>
public class RegistrationContext
{
    public required ObisStudent Student { get; init; }

    /// <summary>Doğrulanan dönem (öğrencinin kendi dönem sırası).</summary>
    public required int Term { get; init; }

    /// <summary>Yalnızca <see cref="Term"/>'den ÖNCEKİ dönemlerin transkript kayıtları.
    /// Sonraki dönemlerin sonuçları, geçmiş bir dönemi doğrularken kullanılmaz.</summary>
    public required IReadOnlyList<ObisTranscriptEntry> History { get; init; }

    /// <summary>Seçilen dersler (aynı ders kodu bir kez).</summary>
    public required IReadOnlyList<ObisRegistration> Selected { get; init; }

    public required IReadOnlyDictionary<string, ObisCourse> Courses { get; init; }
    public required IReadOnlyList<ObisScheduleSlot> Schedule { get; init; }
    public required IReadOnlyList<ObisExamSlot> Exams { get; init; }
    public required ObisCurriculum Curriculum { get; init; }
    public required RegistrationRuleOptions Options { get; init; }

    /// <summary>Önceki dönemlerden hesaplanan GANO; geçmişi yoksa 0.</summary>
    public double Gpa { get; init; }

    public bool HasHistory => History.Count > 0;

    public IEnumerable<ObisScheduleSlot> SlotsFor(ObisRegistration r) =>
        Schedule.Where(s => s.CourseCode == r.CourseCode && s.Section == r.Section);

    public IEnumerable<ObisExamSlot> ExamsFor(ObisRegistration r) =>
        Exams.Where(e => e.CourseCode == r.CourseCode && e.Section == r.Section);

    public bool IsPassed(string courseCode) =>
        History.Any(h => h.CourseCode == courseCode && h.Passed);

    public string Label(string courseCode) =>
        Courses.TryGetValue(courseCode, out var c) ? $"{courseCode} ({c.CourseName})" : courseCode;

    public string Labels(IEnumerable<string> codes) => string.Join(", ", codes.Select(Label));

    /// <summary>Şube öğrencinin öğretim türüne uygun mu (bkz. <see cref="EducationType"/>).</summary>
    public bool SectionAllowed(string section) => EducationType.SectionAllowed(Student.Program, section);

    /// <summary>Kısa bulgu metinleri için yalnızca ders adı.</summary>
    public string Name(string courseCode) => Courses.TryGetValue(courseCode, out var c) ? c.CourseName : courseCode;

    public string Names(IEnumerable<string> codes) => string.Join(", ", codes.Select(Name));
}

public class RegistrationValidationResult
{
    public required ObisStudent Student { get; init; }
    public required int Term { get; init; }
    public double Gpa { get; init; }
    public int TotalEcts { get; init; }
    public int MaxEcts { get; init; }
    public required IReadOnlyList<ObisRegistration> Selected { get; init; }
    public required IReadOnlyList<RegistrationIssue> Issues { get; init; }

    public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == IssueSeverity.Info);
}
