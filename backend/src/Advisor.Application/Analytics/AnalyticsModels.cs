using Advisor.Domain.Enums;

namespace Advisor.Application.Analytics;

/// <summary>Risk ve mezuniyet eşikleri. appsettings.json içindeki "RiskRules" bölümünden okunur.</summary>
public class RiskOptions
{
    public const string SectionName = "RiskRules";

    // GANO göstergesi
    public double LowGpaThreshold { get; set; } = 2.0;
    public double WatchGpaThreshold { get; set; } = 2.3;
    public int LowGpaPoints { get; set; } = 35;
    public int WatchGpaPoints { get; set; } = 15;

    // Son iki tamamlanan dönem arasındaki dönem ortalaması düşüşü
    public double GpaDropWarn { get; set; } = 0.5;
    public double GpaDropSevere { get; set; } = 1.0;
    public int GpaDropPoints { get; set; } = 20;
    public int GpaDropSeverePoints { get; set; } = 30;

    // Üst üste kalınan dersler
    public int RepeatedFailPointsEach { get; set; } = 15;
    public int RepeatedFailMaxPoints { get; set; } = 30;
    public int ConsecutiveFailTermsPoints { get; set; } = 10;

    // Devamsızlık oranı (mevcut dönemde, ders başına)
    public double AbsenceWarnRate { get; set; } = 0.20;
    public double AbsenceLimitRate { get; set; } = 0.30;
    public int AbsenceWarnPoints { get; set; } = 20;
    public int AbsenceLimitPoints { get; set; } = 40;

    // Azami öğrenim süresi (dönem sayısı)
    public int NormalTerms { get; set; } = 8;
    public int MaxTerms { get; set; } = 14;
    public int OverNormalBasePoints { get; set; } = 10;
    public int OverNormalPointsPerTerm { get; set; } = 5;
    public int OverNormalMaxPoints { get; set; } = 30;
    public int NearMaxPoints { get; set; } = 40;

    // Risk seviyesi sınırları (toplam puan 0-100)
    public int MediumRiskScore { get; set; } = 30;
    public int HighRiskScore { get; set; } = 60;

    // Mezuniyet
    public double GraduationMinGpa { get; set; } = 2.0;
    public int TypicalTermEcts { get; set; } = 30;
}

/// <summary>Risk puanına katkı yapan tek bir gösterge; neden puan verildiği metinle açıklanır.</summary>
public record RiskIndicator(string Code, string Title, int Points, string Explanation);

public record RiskAssessment(string StudentNo, int Score, RiskLevel Level, List<RiskIndicator> Indicators);

public record RiskRow(
    string StudentNo, string FullName, int ClassYear, double Gpa, int Term,
    int Score, RiskLevel Level, List<string> IndicatorTitles);

public record RiskDetail(
    string StudentNo, string FullName, int ClassYear, double Gpa, int Term,
    int Score, RiskLevel Level, List<RiskIndicator> Indicators);

// ---- mezuniyet ----

/// <summary>Status: Failed (denendi, geçilemedi), NotTaken (hiç alınmadı), InProgress (bu dönem alınıyor).</summary>
public record MissingCourse(string CourseCode, string CourseName, int Term, int Ects, string Status);

public record PassedElective(string CourseCode, string CourseName, int Credit, int Ects);

public record GraduationAnalysis(
    string StudentNo, string FullName, int CurrentTerm,
    double Gpa, double MinGpa, bool GpaOk,
    int EarnedEcts, int RequiredEcts, double EctsProgressPercent,
    int MandatoryTotal, int MandatoryPassed, List<MissingCourse> MissingMandatory,
    int ElectiveEarnedCredits, int ElectiveRequiredCredits, List<PassedElective> PassedElectives,
    int InternshipDays, int InternshipRequiredDays,
    bool CanGraduate, List<string> Blockers,
    bool ProjectedCanGraduate, List<string> ProjectedBlockers,
    int EstimatedRemainingTerms);

// ---- danışman paneli ----

public record RiskDistribution(int Low, int Medium, int High);

public record TermGpaPoint(int Term, double AverageGpa, int StudentCount);

public record CourseSuccess(string CourseCode, string CourseName, int Attempts, int Passed, double SuccessRate);

public record DashboardResponse(
    int StudentCount, double AverageGpa, RiskDistribution Risk,
    List<TermGpaPoint> GpaTrend, List<CourseSuccess> CourseSuccess, int GraduationCandidates);

/// <summary>Duyuru hedefleme gibi süzgeçler için öğrencinin özet profili.</summary>
public record StudentProfile(
    string StudentNo, string FullName, int ClassYear, double Gpa, bool HasGpa,
    RiskLevel Risk, int RiskScore, int InternshipDays, bool InternshipComplete);
