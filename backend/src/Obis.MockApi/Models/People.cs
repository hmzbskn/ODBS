namespace Obis.MockApi.Models;

/// <summary>Akademik danışman (mock OBİS perspektifinden; sadece referans bilgisi).</summary>
public record AdvisorInfo(
    string AdvisorId,
    string FirstName,
    string LastName,
    string Title);

/// <summary>Öğrenci özet bilgisi.</summary>
public record StudentInfo(
    string StudentNo,
    string FirstName,
    string LastName,
    int ClassYear,
    string Department,
    string AdvisorId,
    double Gpa,
    string Status);
