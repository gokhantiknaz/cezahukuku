namespace CezaHukuku.Infrastructure.Files;

/// <summary>
/// Yüklenen dosyaların deposu. Geliştirmede yerel disk; üretimde S3 uyumlu depo (Hetzner, MinIO, R2) aynı arayüzle
/// eklenecek. Anahtarlar uygulamanın ürettiği göreli yollardır ("ekler/2026/10/{id}.pdf"); kullanıcı girdisi
/// içermez.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct = default);

    /// <summary>Dosya yoksa null.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Yerel deponun klasörü; göreliyse uygulama klasörüne göre. Varsayılan "App_Data/files".</summary>
    public string Root { get; set; } = "App_Data/files";
}

public sealed class LocalFileStorage(string root) : IFileStorage
{
    private readonly string root = Path.GetFullPath(root);

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    // Anahtar uygulamada üretilir; yine de depo klasörünün dışına çıkan yol reddedilir.
    private string PathOf(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Geçersiz dosya anahtarı.", nameof(key));
        return path;
    }
}
