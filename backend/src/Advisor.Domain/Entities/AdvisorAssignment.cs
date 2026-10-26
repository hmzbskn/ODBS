namespace Advisor.Domain.Entities;

/// <summary>Bölüm başkanı / dekan / yönetici tarafından yapılan danışman ataması. OBİS'teki
/// danışman bilgisini ezer; kayıt silinirse öğrenci OBİS'teki danışmanına döner.
/// Öğrenci başına en fazla bir kayıt vardır.</summary>
public class AdvisorAssignment
{
    public Guid Id { get; set; }
    public required string StudentNo { get; set; }
    public required string AdvisorId { get; set; }

    /// <summary>Atama yapılmadan önce geçerli olan (OBİS ya da önceki atamadan gelen) danışman.</summary>
    public string? PreviousAdvisorId { get; set; }

    public Guid AssignedByUserId { get; set; }
    public required string AssignedByName { get; set; }
    public DateTime AssignedAt { get; set; }
}
