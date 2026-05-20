using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Options;

namespace DocIntelligence.Infrastructure.Storage;

/// <summary>
/// Lokal dosya sistemi tabanlı depolama servisi.
/// Production'da Azure Blob Storage implementasyonu ile değiştirilebilir.
/// IStorageService interface'i sayesinde üst katmanlar değişmez.
/// </summary>
public class LocalStorageService : IStorageService
{
    private readonly LocalStorageOptions _options;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(
        IOptions<LocalStorageOptions> options,
        ILogger<LocalStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Klasörü oluştur (yoksa)
        Directory.CreateDirectory(_options.BasePath);
    }

    public async Task<string> SaveAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // Unique dosya adı oluştur (orijinal ad + GUID prefix)
        var safeFileName = Path.GetFileNameWithoutExtension(fileName)
            .Replace(" ", "_")
            .Replace("..", "");
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}_{safeFileName}{extension}";
        var fullPath = Path.Combine(_options.BasePath, uniqueName);

        // Dizin yoksa oluştur
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileOutput = File.Create(fullPath);
        await fileStream.CopyToAsync(fileOutput, cancellationToken);

        _logger.LogInformation("Dosya kaydedildi: {Path}", fullPath);
        return uniqueName; // Sadece relative path sakla
    }

    public async Task<Stream> GetAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_options.BasePath, storagePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Dosya bulunamadı: {storagePath}", storagePath);

        // Belleğe al - file handle'ı serbest bırak
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return new MemoryStream(bytes);
    }

    public Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_options.BasePath, storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Dosya silindi: {Path}", fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_options.BasePath, storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }
}
