using Advisor.Application.Communication;
using Microsoft.Extensions.Options;

namespace Advisor.Infrastructure.Communication;

public class UploadOptions
{
    public const string SectionName = "Uploads";

    /// <summary>Yükleme klasörü. Göreli yol çalışma dizinine göre çözülür; üretimde mutlak yol verilmelidir.</summary>
    public string Path { get; set; } = "App_Data/uploads";
}

/// <summary>Dosyaları yerel diske rastgele adlarla yazar. Saklama adı kullanıcı girdisi içermez, bu yüzden
/// yol gezintisi (path traversal) mümkün değildir; yine de okurken ad doğrulanır.</summary>
public class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(IOptions<UploadOptions> options)
    {
        _root = System.IO.Path.GetFullPath(options.Value.Path);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var stored = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var target = File.Create(System.IO.Path.Combine(_root, stored));
        await content.CopyToAsync(target, ct);
        return stored;
    }

    public Task<Stream?> OpenAsync(string storedName, CancellationToken ct = default)
    {
        // Yalnızca bu deponun ürettiği biçimde adlar kabul edilir: 32 onaltılık karakter + uzantı.
        if (storedName != System.IO.Path.GetFileName(storedName) || storedName.Length < 33 || storedName.Contains(".."))
            return Task.FromResult<Stream?>(null);

        var path = System.IO.Path.Combine(_root, storedName);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }
}
