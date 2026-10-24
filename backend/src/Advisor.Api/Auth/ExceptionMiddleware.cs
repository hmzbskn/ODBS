namespace Advisor.Api.Auth;

/// <summary>Yakalanmamış hataları Türkçe, tek biçimli bir JSON gövdeye çevirir; iç hata
/// ayrıntıları istemciye sızdırılmaz, sunucu günlüğüne yazılır.</summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OBİS bağlantı hatası");
            await Write(context, StatusCodes.Status502BadGateway,
                "Öğrenci bilgi sistemine (OBİS) şu anda ulaşılamıyor. Lütfen daha sonra tekrar deneyin.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // İstemci vazgeçti; yanıt yazılmaz.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmeyen hata");
            await Write(context, StatusCodes.Status500InternalServerError,
                "Beklenmeyen bir hata oluştu. Sorun devam ederse yöneticiye bildirin.");
        }
    }

    private static Task Write(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { message });
    }
}
