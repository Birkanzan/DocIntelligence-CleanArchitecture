using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Options;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace DocIntelligence.Infrastructure.Services;

/// <summary>
/// Tesseract OCR kütüphanesini kullanarak görsel/PDF'den metin çıkarır.
/// Azure AI Vision kullanmak istersen sadece bu sınıfı değiştir.
/// </summary>
public class TesseractOcrService : IOcrService
{
    private readonly TesseractOptions _options;
    private readonly ILogger<TesseractOcrService> _logger;

    public TesseractOcrService(
        IOptions<TesseractOptions> options,
        ILogger<TesseractOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OcrResult> ExtractTextAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // PDF'ler için iText7 ile metin çıkar (OCR'a gerek yok)
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return await ExtractFromPdfAsync(fileStream, cancellationToken);

        // Görseller için Tesseract kullan
        return await ExtractFromImageAsync(fileStream, cancellationToken);
    }

    private async Task<OcrResult> ExtractFromImageAsync(
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        using var engine = new TesseractEngine(
            _options.DataPath,
            _options.Language,
            EngineMode.Default);

        imageStream.Seek(0, SeekOrigin.Begin);
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();

        using var img = Pix.LoadFromMemory(imageBytes);
        using var page = engine.Process(img);

        var text = page.GetText();
        var confidence = page.GetMeanConfidence();

        _logger.LogInformation("OCR tamamlandı: Güven={Confidence:P0}, Karakter={Chars}",
            confidence, text.Length);

        var ocrPage = new OcrPage(1, text, confidence);
        return new OcrResult(text, confidence, new[] { ocrPage });
    }

    private Task<OcrResult> ExtractFromPdfAsync(
        Stream pdfStream,
        CancellationToken cancellationToken)
    {
        pdfStream.Seek(0, SeekOrigin.Begin);
        var pages = new List<OcrPage>();
        var fullText = new System.Text.StringBuilder();

        try
        {
            using var reader = new PdfReader(pdfStream);
            using var pdfDoc = new PdfDocument(reader);

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var strategy = new LocationTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                pages.Add(new OcrPage(i, pageText, 1.0)); // PDF metin çıkarma = tam güven
                fullText.AppendLine(pageText);
            }

            var result = new OcrResult(fullText.ToString(), 1.0, pages);
            _logger.LogInformation("PDF metin çıkarma tamamlandı: {Pages} sayfa", pages.Count);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PDF metin çıkarma başarısız oldu (muhtemelen uyumsuz/bozuk PDF): {Msg}", ex.Message);
            return Task.FromResult(new OcrResult("", 0.0, Array.Empty<OcrPage>()));
        }
    }
}
