namespace Advisor.Infrastructure.Obis;

/// <summary>appsettings.json → "Obis" bölümünden okunur. Gerçek OBİS'e geçişte
/// sadece BaseUrl (ve varsa kimlik bilgileri) değişir.</summary>
public class ObisOptions
{
    public const string SectionName = "Obis";

    public string BaseUrl { get; set; } = "http://localhost:5280";

    /// <summary>OBİS yanıtlarının bellekte kaç saniye önbelleğe alınacağı.</summary>
    public int CacheSeconds { get; set; } = 30;
}
