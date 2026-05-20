using DocIntelligence.Application.DTOs;
using DocIntelligence.Domain.Enums;
using DocIntelligence.Domain.Interfaces;

namespace DocIntelligence.Worker;

/// <summary>
/// Belge işleme pipeline'ını yöneten arka plan worker'ı.
/// RabbitMQ'dan "document.processing" kuyruğunu dinler ve
/// Optimize → OCR → Classify adımlarını sırayla çalıştırır.
/// </summary>
public class DocumentProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageBroker _broker;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    private const string QueueName = "document.processing";

    public DocumentProcessingWorker(
        IServiceScopeFactory scopeFactory,
        IMessageBroker broker,
        ILogger<DocumentProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _broker = broker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentProcessingWorker başlatıldı. Kuyruk dinleniyor: {Queue}", QueueName);

        await _broker.SubscribeAsync<DocumentUploadedMessage>(
            QueueName,
            ProcessDocumentAsync,
            stoppingToken);

        // Worker çalışmaya devam eder
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessDocumentAsync(
        DocumentUploadedMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Belge işleniyor: {DocumentId}", message.DocumentId);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var optimizer = scope.ServiceProvider.GetRequiredService<IDocumentOptimizer>();
        var ocr = scope.ServiceProvider.GetRequiredService<IOcrService>();
        var classifier = scope.ServiceProvider.GetRequiredService<IDocumentClassifier>();

        var document = await repository.GetByIdAsync(message.DocumentId, cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Belge bulunamadı: {DocumentId}", message.DocumentId);
            return;
        }

        try
        {
            // ── ADIM 1: OPTİMİZASYON ─────────────────────────────────
            _logger.LogInformation("[{Id}] Adım 1: Optimizasyon başlıyor", document.Id);
            document.SetStatus(DocumentStatus.Optimizing);
            await repository.UpdateAsync(document, cancellationToken);

            var fileStream = await storage.GetAsync(document.StoragePath, cancellationToken);
            var options = new OptimizationOptions(
                MaxWidthPx: 1280,        // Agresif: 1920 -> 1280
                MaxHeightPx: 720,
                JpegQuality: 50,         // Agresif: 65 -> 50
                GrayscaleIfPossible: true 
            );

            OptimizationResult optimizationResult;
            if (message.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                optimizationResult = await optimizer.OptimizePdfAsync(fileStream, options, cancellationToken);
            else
                optimizationResult = await optimizer.OptimizeImageAsync(fileStream, message.ContentType, options, cancellationToken);

            // Optimize edilmiş dosyayı kaydet
            var optimizedPath = await storage.SaveAsync(
                optimizationResult.OptimizedStream,
                $"opt_{Path.GetFileName(document.StoragePath)}",
                message.ContentType,
                cancellationToken);

            document.SetOptimizedPath(optimizedPath, optimizationResult.OptimizedSizeBytes);

            _logger.LogInformation("[{Id}] Optimizasyon tamamlandı: %{Ratio:F0} tasarruf",
                document.Id, (1 - optimizationResult.CompressionRatio) * 100);

            // ── ADIM 2: OCR ──────────────────────────────────────────
            _logger.LogInformation("[{Id}] Adım 2: OCR başlıyor", document.Id);
            document.SetStatus(DocumentStatus.ExtractingText);
            await repository.UpdateAsync(document, cancellationToken);

            var optimizedStream = await storage.GetAsync(optimizedPath, cancellationToken);
            var ocrResult = await ocr.ExtractTextAsync(optimizedStream, message.ContentType, cancellationToken);

            document.SetExtractedText(ocrResult.Text, ocrResult.Confidence);
            _logger.LogInformation("[{Id}] OCR tamamlandı: {Chars} karakter, güven {Confidence:P0}",
                document.Id, ocrResult.Text.Length, ocrResult.Confidence);

            // ── ADIM 3: SINIFLANDIRMA ────────────────────────────────
            _logger.LogInformation("[{Id}] Adım 3: ML sınıflandırma başlıyor", document.Id);
            document.SetStatus(DocumentStatus.Classifying);
            await repository.UpdateAsync(document, cancellationToken);

            var classResult = await classifier.ClassifyAsync(ocrResult.Text, cancellationToken);
            document.SetCategory(classResult.Category, classResult.Confidence);

            _logger.LogInformation("[{Id}] Sınıflandırma: {Category} (güven: {Confidence:P0})",
                document.Id, classResult.Category, classResult.Confidence);

            // ── TAMAMLANDI ───────────────────────────────────────────
            document.SetStatus(DocumentStatus.Completed);
            await repository.UpdateAsync(document, cancellationToken);

            _logger.LogInformation("[{Id}] Tüm işlemler başarıyla tamamlandı.", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Id}] İşlem sırasında hata oluştu", document.Id);
            document.SetError(ex.Message);
            await repository.UpdateAsync(document, cancellationToken);
            throw; // RabbitMQ BasicNack tetiklensin
        }
    }
}
