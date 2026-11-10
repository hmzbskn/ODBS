using Advisor.Domain.Enums;

namespace Advisor.Domain.Entities;

/// <summary>Danışmanın oluşturduğu ihtiyaç/sosyoekonomik durum anketi.</summary>
public class Survey
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public required string CreatedByAdvisorId { get; set; }

    /// <summary>Hedef öğrenciler — basit filtre: null ise danışmanın tüm öğrencileri.</summary>
    public int? TargetClassYear { get; set; }

    public List<SurveyQuestion> Questions { get; set; } = new();
    public List<SurveyResponse> Responses { get; set; } = new();
}

public class SurveyQuestion
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Survey? Survey { get; set; }
    public required string Text { get; set; }
    public QuestionType Type { get; set; }
    public int Order { get; set; }

    /// <summary>Soru ağırlığı (çarpan). Seçim sorularında seçilen seçeneklerin puanı, sayı sorularında
    /// girilen değer bu çarpanla çarpılır. Negatif olabilir (ör. gelir arttıkça ihtiyaç azalır).</summary>
    public double Weight { get; set; } = 1;

    public bool Required { get; set; } = true;

    public List<SurveyQuestionOption> Options { get; set; } = new();
}

/// <summary>Seçim tipi sorularda her seçeneğin danışman tarafından tanımlanan puanı
/// (ör. "Hane geliri 0-5000 TL" seçeneği 10 puan). Number/Text tipi sorularda kullanılmaz.</summary>
public class SurveyQuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public SurveyQuestion? Question { get; set; }
    public required string Text { get; set; }
    public double Score { get; set; }
}

/// <summary>Bir öğrencinin bir ankete verdiği tüm cevapların zarfı. KVKK açık rıza
/// onayı, cevaplar kaydedilmeden önce alınmış olmalı (bkz. Application katmanındaki
/// SurveyService kuralı).</summary>
public class SurveyResponse
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Survey? Survey { get; set; }
    public required string StudentNo { get; set; }
    public bool ConsentAccepted { get; set; }
    public DateTime? ConsentAcceptedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Dışarıdan (CSV/Excel) içe aktarılan sonuçlar için true.</summary>
    public bool ImportedExternally { get; set; }

    public List<SurveyAnswer> Answers { get; set; } = new();
}

public class SurveyAnswer
{
    public Guid Id { get; set; }
    public Guid ResponseId { get; set; }
    public SurveyResponse? Response { get; set; }
    public Guid QuestionId { get; set; }

    /// <summary>Seçim tipi: seçilen seçenek id'leri virgülle ayrılmış (basit, SQL'de
    /// ayrı bir ilişki tablosu yerine; sorgu hacmi küçük olduğu için kabul edilebilir).</summary>
    public string? SelectedOptionIds { get; set; }

    public double? NumberValue { get; set; }
    public string? TextValue { get; set; }
}

/// <summary>Bir anketin bir öğrenci için danışmanın verdiği destek kararı. Puanın kendisi her
/// seferinde cevaplardan canlı hesaplanır; burada atama anındaki puan denetim izi olarak saklanır.</summary>
public class NeedScoreResult
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public required string StudentNo { get; set; }
    public double TotalScore { get; set; }
    public DateTime ComputedAt { get; set; }
    public string? SupportAssigned { get; set; }
}
