using DocIntelligence.Domain.Enums;

namespace DocIntelligence.Domain.Entities;

/// <summary>
/// Bir belge için arka planda çalıştırılan bir işi (job) temsil eder.
/// Her belge birden fazla iş kaydına sahip olabilir (optimize, OCR, classify).
/// </summary>
public class ProcessingJob : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public Document Document { get; private set; } = default!;

    public string JobType { get; private set; } = default!;  // "Optimize", "OCR", "Classify"
    public DocumentStatus Status { get; private set; } = DocumentStatus.Pending;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ResultMetadata { get; private set; }  // JSON

    private ProcessingJob() { }

    public ProcessingJob(Guid documentId, string jobType)
    {
        DocumentId = documentId;
        JobType = jobType;
    }

    public void Start()
    {
        Status = DocumentStatus.Optimizing;
        StartedAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void Complete(string? resultMetadata = null)
    {
        Status = DocumentStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        ResultMetadata = resultMetadata;
        SetUpdatedAt();
    }

    public void Fail(string errorMessage)
    {
        Status = DocumentStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt();
    }
}
