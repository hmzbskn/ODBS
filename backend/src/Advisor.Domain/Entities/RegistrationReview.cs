using Advisor.Domain.Enums;

namespace Advisor.Domain.Entities;

/// <summary>Danışmanın bir öğrencinin bir dönemdeki ders kaydı için verdiği karar.
/// OBİS'in kendi "onay durumu" alanından bağımsız — bu, ANA UYGULAMANIN kendi
/// danışmanlık sürecinin kaydı (prompt: "bu karar ana uygulamanın kendi
/// veritabanında tutulsun").</summary>
public class RegistrationReview
{
    public Guid Id { get; set; }
    public required string StudentNo { get; set; }
    public int Term { get; set; }
    public RegistrationDecision Decision { get; set; }
    public string? Note { get; set; }
    public required string ReviewedByAdvisorId { get; set; }
    public DateTime ReviewedAt { get; set; }
}
