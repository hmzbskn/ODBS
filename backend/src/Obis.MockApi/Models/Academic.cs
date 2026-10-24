namespace Obis.MockApi.Models;

/// <summary>Ders kataloğundaki bir ders (mock OBİS perspektifinden).</summary>
public record CourseInfo(
    string CourseCode,
    string CourseName,
    int Credit,
    int Ects,
    int Term,
    bool IsMandatory,
    List<string> Prerequisites);

/// <summary>Bölümün mezuniyet koşulları.</summary>
public record CurriculumInfo(
    string Department,
    List<string> MandatoryCourseCodes,
    int ElectiveCreditRequirement,
    int TotalEctsRequirement,
    int InternshipDaysRequirement);
