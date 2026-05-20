using DocIntelligence.Domain.Enums;

namespace DocIntelligence.Application.DTOs;

/// <summary>
/// Belge bilgilerini API'ye dönerken kullanılan DTO.
/// Domain entity'si yerine bu nesne aktarılır.
/// </summary>
public record DocumentDto
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = default!;
    public long FileSizeBytes { get; init; }
    public long? OptimizedSizeBytes { get; init; }
    public string? OptimizedStoragePath { get; init; }
    public string FileType { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string Category { get; init; } = default!;
    public double ClassificationConfidence { get; init; }
    public string? ExtractedText { get; init; }
    public double OcrConfidence { get; init; }
    public DateTime UploadedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? Tags { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Belge yükleme isteği için kullanılan model.
/// </summary>
public record UploadDocumentRequest(
    string FileName,
    string ContentType,
    long FileSizeBytes
);

/// <summary>
/// Mesaj kuyruğuna gönderilen "yeni belge geldi" mesajı.
/// </summary>
public record DocumentUploadedMessage(
    Guid DocumentId,
    string StoragePath,
    string ContentType,
    string FileType
);

/// <summary>
/// Arama filtresi.
/// </summary>
public record DocumentSearchFilter(
    string? UserId = null,
    DocumentStatus? Status = null,
    DocumentCategory? Category = null,
    string? TextQuery = null,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// Sayfalı sonuç wrapper'ı.
/// </summary>
public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
