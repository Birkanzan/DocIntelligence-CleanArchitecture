namespace DocIntelligence.Domain.Interfaces;

/// <summary>
/// Message broker soyutlaması.
/// RabbitMQ veya Azure Service Bus ile implemente edilebilir.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// Kuyruğa yeni bir mesaj gönderir.
    /// </summary>
    Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Kuyruğu dinler ve mesaj gelince handler'ı çağırır.
    /// </summary>
    Task SubscribeAsync<T>(
        string queueName,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where T : class;
}

/// <summary>
/// OCR servisi soyutlaması.
/// Tesseract veya Azure AI Vision ile implemente edilebilir.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Görsel veya PDF dosyasından metin çıkarır.
    /// </summary>
    Task<OcrResult> ExtractTextAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// OCR sonucunu taşıyan değer nesnesi.
/// </summary>
public record OcrResult(
    string Text,
    double Confidence,
    IReadOnlyList<OcrPage> Pages);

public record OcrPage(
    int PageNumber,
    string Text,
    double Confidence);

/// <summary>
/// Belge optimizasyon servisi soyutlaması.
/// </summary>
public interface IDocumentOptimizer
{
    /// <summary>
    /// Görsel dosyayı optimize eder (boyutlandırma, kalite düşürme).
    /// </summary>
    Task<OptimizationResult> OptimizeImageAsync(
        Stream imageStream,
        string contentType,
        OptimizationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF dosyasını optimize eder.
    /// </summary>
    Task<OptimizationResult> OptimizePdfAsync(
        Stream pdfStream,
        OptimizationOptions options,
        CancellationToken cancellationToken = default);
}

public record OptimizationResult(
    Stream OptimizedStream,
    long OriginalSizeBytes,
    long OptimizedSizeBytes,
    double CompressionRatio);

public record OptimizationOptions(
    int MaxWidthPx = 1920,
    int MaxHeightPx = 1080,
    int JpegQuality = 65,
    bool GrayscaleIfPossible = true);

/// <summary>
/// ML.NET ile belge sınıflandırma sözleşmesi.
/// </summary>
public interface IDocumentClassifier
{
    Task<ClassificationResult> ClassifyAsync(
        string extractedText,
        CancellationToken cancellationToken = default);
}

public record ClassificationResult(
    Domain.Enums.DocumentCategory Category,
    double Confidence,
    IDictionary<string, float> AllScores);
