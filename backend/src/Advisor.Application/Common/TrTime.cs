namespace Advisor.Application.Common;

/// <summary>Türkiye saati (UTC+3, yaz saati uygulaması yok) yardımcıları. Randevu ve duyuru gibi
/// takvim işlemleri kullanıcının duvar saatine göre değerlendirilir; <see cref="TimeProvider"/> ile test edilebilir.</summary>
public static class TrTime
{
    public static DateTime Now(TimeProvider time) => time.GetUtcNow().UtcDateTime.AddHours(3);

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time));

    public static DateTime At(DateOnly date, TimeOnly time) => date.ToDateTime(time);
}
