using DocIntelligence.Domain.Enums;

namespace DocIntelligence.Domain.Entities;

/// <summary>
/// Sisteme yüklenen bir belgeyi temsil eden ana entity.
/// Dosyanın kendisi depolanmaz; sadece path ve metadata tutulur.
/// </summary>
public class Document : BaseEntity
{
    // --- Temel Bilgiler ---
    public string OriginalFileName { get; private set; } = default!;
    public string StoragePath { get; private set; } = default!;
    public string? OptimizedStoragePath { get; private set; }
    public long FileSizeBytes { get; private set; }
    public long? OptimizedSizeBytes { get; private set; }
    public FileType FileType { get; private set; }
    public string ContentType { get; private set; } = default!;

    // --- İşlem Durumu ---
    public DocumentStatus Status { get; private set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; private set; }

    // --- OCR Sonuçları ---
    public string? ExtractedText { get; private set; }
    public double OcrConfidence { get; private set; }

    // --- ML Sınıflandırma ---
    public DocumentCategory Category { get; private set; } = DocumentCategory.Unknown;
    public double CategoryConfidence { get; private set; }

    // --- Metadata ---
    public string? UploadedByUserId { get; private set; }
    public string? Tags { get; private set; }  // JSON olarak saklanır

    // --- İlişkiler ---
    public ICollection<ProcessingJob> ProcessingJobs { get; private set; } = new List<ProcessingJob>();

    // EF Core için parametresiz constructor
    private Document() { }

    public Document(
        string originalFileName,
        string storagePath,
        long fileSizeBytes,
        FileType fileType,
        string contentType,
        string? uploadedByUserId = null)
    {
        OriginalFileName = originalFileName;
        StoragePath = storagePath;
        FileSizeBytes = fileSizeBytes;
        FileType = fileType;
        ContentType = contentType;
        UploadedByUserId = uploadedByUserId;
    }

    // --- Domain Metotları ---

    public void SetStatus(DocumentStatus status)
    {
        Status = status;
        SetUpdatedAt();
    }

    public void SetOptimizedPath(string path, long newSizeBytes)
    {
        OptimizedStoragePath = path;
        OptimizedSizeBytes = newSizeBytes;
        SetUpdatedAt();
    }

    public void SetExtractedText(string text, double confidence)
    {
        ExtractedText = text;
        OcrConfidence = confidence;
        SetUpdatedAt();
    }

    public void SetCategory(DocumentCategory category, double confidence)
    {
        Category = category;
        CategoryConfidence = confidence;
        SetUpdatedAt();
    }

    public void SetError(string errorMessage)
    {
        Status = DocumentStatus.Failed;
        ErrorMessage = errorMessage;
        SetUpdatedAt();
    }

    public void SetTags(string tagsJson)
    {
        Tags = tagsJson;
        SetUpdatedAt();
    }
}
