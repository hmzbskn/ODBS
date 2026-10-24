using Advisor.Application.Common;

namespace Advisor.Application.Communication;

/// <summary>Talep eki doğrulaması: izinli uzantılar, boyut sınırı ve dosya içeriği imzası (sihirli baytlar).
/// Uzantı tek başına yeterli sayılmaz: ".pdf" adlı bir çalıştırılabilir dosya reddedilir.</summary>
public static class AttachmentValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", new[] { "%PDF-"u8.ToArray() }),
        [".png"] = ("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),
        [".jpg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
        [".jpeg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
        // .docx bir ZIP kapsayıcısıdır.
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }),
    };

    public static string AllowedExtensionsText => string.Join(", ", Allowed.Keys);

    /// <summary>Doğrulanmış (ad, tür, uzantı) döndürür; geçersizse <see cref="AppException"/> fırlatır.
    /// Akışın başına geri sarılır.</summary>
    public static (string SafeName, string ContentType, string Extension) Validate(string? fileName, Stream content, long length)
    {
        if (length <= 0) throw AppException.BadRequest("Yüklenen dosya boş.");
        if (length > MaxBytes) throw AppException.BadRequest("Dosya çok büyük (en fazla 5 MB).");

        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        var extension = Path.GetExtension(name);
        if (name.Length == 0 || !Allowed.TryGetValue(extension, out var rule))
            throw AppException.BadRequest($"Bu dosya türü kabul edilmiyor. İzinli türler: {AllowedExtensionsText}.");

        var header = new byte[8];
        var read = ReadFully(content, header);
        content.Seek(0, SeekOrigin.Begin);
        if (!rule.Signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig)))
            throw AppException.BadRequest("Dosya içeriği uzantısıyla uyuşmuyor; geçerli bir belge yükleyin.");

        return (SafeFileName(name), rule.ContentType, extension.ToLowerInvariant());
    }

    /// <summary>Özgün adı yol ve denetim karakterlerinden arındırır, uzunluğunu sınırlar.</summary>
    public static string SafeFileName(string name)
    {
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c is not ('/' or '\\' or '"' or ':' or '*' or '?' or '<' or '>' or '|')).ToArray()).Trim();
        if (cleaned.Length > 100)
        {
            var ext = Path.GetExtension(cleaned);
            cleaned = cleaned[..(100 - ext.Length)] + ext;
        }

        return cleaned.Length == 0 ? "ek" : cleaned;
    }

    private static int ReadFully(Stream s, byte[] buffer)
    {
        var total = 0;
        int n;
        while (total < buffer.Length && (n = s.Read(buffer, total, buffer.Length - total)) > 0) total += n;
        return total;
    }
}
