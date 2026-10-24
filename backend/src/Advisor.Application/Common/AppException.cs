namespace Advisor.Application.Common;

/// <summary>İş kuralı ihlali. Global exception middleware bunu verilen HTTP durum koduyla
/// ve (Türkçe) mesajla istemciye döndürür; sunucu hatası olarak günlüğe yazılmaz.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public static AppException BadRequest(string message) => new(400, message);
    public static AppException Forbidden(string message = "Bu işlem için yetkiniz yok.") => new(403, message);
    public static AppException NotFound(string message) => new(404, message);
    public static AppException Conflict(string message) => new(409, message);
}
