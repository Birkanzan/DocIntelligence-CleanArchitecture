namespace DocIntelligence.Domain.Interfaces;

/// <summary>
/// Dosya depolama soyutlaması.
/// Lokal disk, Azure Blob veya S3 ile implemente edilebilir.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Dosyayı depolar ve erişim yolunu döner.
    /// </summary>
    Task<string> SaveAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Depolanan dosyayı stream olarak okur.
    /// </summary>
    Task<Stream> GetAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dosyayı depolar.
    /// </summary>
    Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dosyanın var olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
