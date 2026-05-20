using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using DocIntelligence.Domain.Interfaces;
using iText.Kernel.Pdf;
using iText.IO.Image;
using iText.Kernel.Pdf.Xobject;

namespace DocIntelligence.Infrastructure.Services;

/// <summary>
/// ImageSharp ve iText7 kullanarak belge optimizasyonu yapar.
/// Görselleri agresif biçimde sıkıştırır, EXIF verisini temizler,
/// PDF gömülü görsellerini ve nesne akışlarını optimize eder.
/// </summary>
public class DocumentOptimizer : IDocumentOptimizer
{
    private readonly ILogger<DocumentOptimizer> _logger;

    public DocumentOptimizer(ILogger<DocumentOptimizer> logger)
    {
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────
    // GÖRSEL OPTİMİZASYONU
    // ─────────────────────────────────────────────────────────────────────

    public async Task<OptimizationResult> OptimizeImageAsync(
        Stream imageStream,
        string contentType,
        OptimizationOptions options,
        CancellationToken cancellationToken = default)
    {
        var originalSize = imageStream.Length;
        imageStream.Seek(0, SeekOrigin.Begin);

        using var image = await Image.LoadAsync(imageStream, cancellationToken);

        // 1) EXIF / ICC / Metadata'yı temizle (boyutu büyük etkiler)
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        // 2) Gri tonlamaya çevir (OCR için yeterli, dosya boyutunu ~%30 düşürür)
        if (options.GrayscaleIfPossible)
            image.Mutate(x => x.Grayscale());

        // 3) Yeniden boyutlandır (orantılı, büyütme yapmaz)
        if (image.Width > options.MaxWidthPx || image.Height > options.MaxHeightPx)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(options.MaxWidthPx, options.MaxHeightPx),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        // 4) En iyi çıkışı seç: JPEG ve WebP'yi dene, hangisi küçükse onu kullan
        var jpegStream = await EncodeJpegAsync(image, options.JpegQuality, cancellationToken);
        var webpStream = await EncodeWebpAsync(image, options.JpegQuality, cancellationToken);

        Stream bestStream;
        string bestMime;

        if (webpStream.Length < jpegStream.Length)
        {
            bestStream = webpStream;
            bestMime = "image/webp";
            await jpegStream.DisposeAsync();
        }
        else
        {
            bestStream = jpegStream;
            bestMime = "image/jpeg";
            await webpStream.DisposeAsync();
        }

        bestStream.Seek(0, SeekOrigin.Begin);
        var optimizedSize = bestStream.Length;
        var ratio = originalSize > 0 ? (double)optimizedSize / originalSize : 1.0;

        _logger.LogInformation(
            "Görsel optimize edildi [{Format}]: {Original:N0} → {Optimized:N0} bytes (tasarruf: %{Saving:F0})",
            bestMime, originalSize, optimizedSize, (1 - ratio) * 100);

        return new OptimizationResult(bestStream, originalSize, optimizedSize, ratio);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PDF OPTİMİZASYONU
    // ─────────────────────────────────────────────────────────────────────

    public async Task<OptimizationResult> OptimizePdfAsync(
        Stream pdfStream,
        OptimizationOptions options,
        CancellationToken cancellationToken = default)
    {
        var originalSize = pdfStream.Length;
        pdfStream.Seek(0, SeekOrigin.Begin);

        Stream? pass1 = null;
        try
        {
            pass1 = await CompressPdfImagesAsync(pdfStream, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PDF Pass 1 (Görsel Sıkıştırma) atlandı: {Msg}", ex.Message);
            // Pass 1 başarısız olursa orijinal akışı bir kopyaya alıp devam edelim
            var fallbackMs = new MemoryStream();
            pdfStream.Seek(0, SeekOrigin.Begin);
            await pdfStream.CopyToAsync(fallbackMs, cancellationToken);
            pass1 = fallbackMs;
        }

        pass1.Seek(0, SeekOrigin.Begin);

        // Geçiş 2: iText7 full-compression
        var outputStream = new MemoryStream();
        bool pass2Success = false;
        try
        {
            using (var reader = new PdfReader(pass1))
            {
                if (reader.IsEncrypted()) throw new Exception("PDF şifreli.");

                using var writer = new PdfWriter(outputStream, new WriterProperties()
                    .SetFullCompressionMode(true)
                    .SetCompressionLevel(CompressionConstants.BEST_COMPRESSION));

                using var pdfDoc = new PdfDocument(reader, writer);
                var info = pdfDoc.GetDocumentInfo();
                info.SetCreator(null);
                info.SetProducer(null);
                pdfDoc.Close();
                pass2Success = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PDF Pass 2 (Full Compression) atlandı: {Msg}", ex.Message);
            pass2Success = false;
        }

        if (!pass2Success)
        {
            pass1.Seek(0, SeekOrigin.Begin);
            var resultRatio = (double)pass1.Length / originalSize;
            return new OptimizationResult(pass1, originalSize, pass1.Length, resultRatio);
        }

        await pass1.DisposeAsync();

        var resultBytes = outputStream.ToArray();
        var resultStream = new MemoryStream(resultBytes);
        var ratio = originalSize > 0 ? (double)resultBytes.Length / originalSize : 1.0;

        _logger.LogInformation(
            "PDF optimize edildi: {Original:N0} → {Optimized:N0} bytes (tasarruf: %{Saving:F0})",
            originalSize, resultBytes.Length, (1 - ratio) * 100);

        return new OptimizationResult(resultStream, originalSize, resultBytes.Length, ratio);
    }

    // ─────────────────────────────────────────────────────────────────────
    // YARDIMCI: PDF içindeki gömülü görselleri JPEG ile sıkıştır
    // ─────────────────────────────────────────────────────────────────────

    private async Task<MemoryStream> CompressPdfImagesAsync(
        Stream inputPdf,
        OptimizationOptions options,
        CancellationToken cancellationToken)
    {
        var outStream = new MemoryStream();
        inputPdf.Seek(0, SeekOrigin.Begin);

        using var reader = new PdfReader(inputPdf);
        if (reader.IsEncrypted()) throw new Exception("PDF şifreli, görseller işlenemiyor.");

        using var writer = new PdfWriter(outStream,
            new WriterProperties().SetFullCompressionMode(true));
        using var pdfDoc = new PdfDocument(reader, writer);

        int totalPages = pdfDoc.GetNumberOfPages();
        int recompressed = 0;

        for (int pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var page = pdfDoc.GetPage(pageNum);
            var resources = page.GetResources();
            if (resources == null) continue;

            var xObjects = resources.GetResource(PdfName.XObject);
            if (xObjects == null) continue;

            foreach (var entry in xObjects.EntrySet())
            {
                var xobj = xObjects.GetAsStream(entry.Key);
                if (xobj == null) continue;

                var subtype = xobj.GetAsName(PdfName.Subtype);
                if (!PdfName.Image.Equals(subtype)) continue;

                var filterObj = xobj.Get(PdfName.Filter);
                if (filterObj != null)
                {
                    var filterStr = filterObj.ToString() ?? string.Empty;
                    if (filterStr.Contains("JBIG2") || filterStr.Contains("JPXDecode"))
                        continue;
                }

                try
                {
                    var imgBytes = xobj.GetBytes(true);
                    if (imgBytes == null || imgBytes.Length < 4096) continue;

                    using var imgMs = new MemoryStream(imgBytes);
                    using var img = await Image.LoadAsync(imgMs, cancellationToken);

                    if (img.Width > options.MaxWidthPx || img.Height > options.MaxHeightPx)
                    {
                        img.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(options.MaxWidthPx, options.MaxHeightPx),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        }));
                    }

                    if (options.GrayscaleIfPossible)
                        img.Mutate(x => x.Grayscale());

                    using var compressedMs = new MemoryStream();
                    await img.SaveAsJpegAsync(compressedMs,
                        new JpegEncoder { Quality = options.JpegQuality }, cancellationToken);

                    if (compressedMs.Length >= imgBytes.Length) continue;

                    var newBytes = compressedMs.ToArray();
                    xobj.SetData(newBytes);
                    xobj.Put(PdfName.Filter, PdfName.DCTDecode);
                    xobj.Put(PdfName.Width, new PdfNumber(img.Width));
                    xobj.Put(PdfName.Height, new PdfNumber(img.Height));
                    xobj.Put(PdfName.BitsPerComponent, new PdfNumber(8));
                    xobj.Put(PdfName.ColorSpace, PdfName.DeviceRGB);
                    xobj.Remove(PdfName.DecodeParms);
                    xobj.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);

                    recompressed++;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("PDF görsel sıkıştırma atlandı: {Msg}", ex.Message);
                }
            }
        }

        _logger.LogInformation("PDF içi görsel sıkıştırma: {Count} görsel yeniden kodlandı", recompressed);
        pdfDoc.Close();
        var resultBytes = outStream.ToArray();
        return new MemoryStream(resultBytes);
    }

    // ─────────────────────────────────────────────────────────────────────
    // YARDIMCI: JPEG / WebP kodlama
    // ─────────────────────────────────────────────────────────────────────

    private static async Task<MemoryStream> EncodeJpegAsync(
        Image image, int quality, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder
        {
            Quality = quality
        }, ct);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    private static async Task<MemoryStream> EncodeWebpAsync(
        Image image, int quality, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await image.SaveAsWebpAsync(ms, new WebpEncoder
        {
            Quality = quality,
            Method = WebpEncodingMethod.BestQuality,
            FileFormat = WebpFileFormatType.Lossy
        }, ct);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
