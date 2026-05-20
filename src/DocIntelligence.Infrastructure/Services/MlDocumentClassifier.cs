using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using DocIntelligence.Domain.Enums;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Options;

namespace DocIntelligence.Infrastructure.Services;

/// <summary>
/// ML.NET tabanlı belge sınıflandırıcı.
/// Önceden eğitilmiş model varsa yükler; yoksa basit kural tabanlı sınıflandırma yapar.
/// </summary>
public class MlDocumentClassifier : IDocumentClassifier
{
    private readonly MlOptions _options;
    private readonly ILogger<MlDocumentClassifier> _logger;
    private readonly MLContext _mlContext;
    private PredictionEngine<DocumentTextInput, DocumentCategoryPrediction>? _predictionEngine;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MlDocumentClassifier(
        IOptions<MlOptions> options,
        ILogger<MlDocumentClassifier> logger)
    {
        _options = options.Value;
        _logger = logger;
        _mlContext = new MLContext(seed: 42);
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string extractedText,
        CancellationToken cancellationToken = default)
    {
        // Model varsa ML.NET ile sınıflandır
        if (_options.ModelPath is not null && File.Exists(_options.ModelPath))
        {
            await EnsureModelLoadedAsync(cancellationToken);
            return PredictWithModel(extractedText);
        }

        // Model yoksa kural tabanlı fallback
        return RuleBasedClassify(extractedText);
    }

    private async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
    {
        if (_predictionEngine is not null) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_predictionEngine is not null) return;

            var model = _mlContext.Model.Load(_options.ModelPath!, out _);
            _predictionEngine = _mlContext.Model
                .CreatePredictionEngine<DocumentTextInput, DocumentCategoryPrediction>(model);

            _logger.LogInformation("ML.NET modeli yüklendi: {Path}", _options.ModelPath);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private ClassificationResult PredictWithModel(string text)
    {
        var input = new DocumentTextInput { Text = text };
        var prediction = _predictionEngine!.Predict(input);

        var category = Enum.TryParse<DocumentCategory>(prediction.PredictedLabel, out var cat)
            ? cat
            : DocumentCategory.Unknown;

        var scores = prediction.Score is not null
            ? Enum.GetNames<DocumentCategory>()
                .Zip(prediction.Score, (name, score) => (name, score))
                .ToDictionary(x => x.name, x => x.score)
            : new Dictionary<string, float>();

        return new ClassificationResult(category, prediction.Score?.Max() ?? 0.0, scores);
    }

    /// <summary>
    /// Model yokken basit anahtar kelime tabanlı sınıflandırma.
    /// Gerçek ML modeli eğitilene kadar kullanılabilir.
    /// </summary>
    private static ClassificationResult RuleBasedClassify(string text)
    {
        var lower = text.ToLowerInvariant();

        var rules = new List<(DocumentCategory Category, string[] Keywords)>
        {
            (DocumentCategory.Invoice,   ["fatura", "invoice", "kdv", "vat", "toplam", "total", "vergi"]),
            (DocumentCategory.Identity,  ["kimlik", "tc kimlik", "nüfus", "passport", "identity", "doğum"]),
            (DocumentCategory.Contract,  ["sözleşme", "contract", "taraf", "madde", "imza", "agreement"]),
            (DocumentCategory.Receipt,   ["fiş", "receipt", "ödeme", "payment", "kasa", "cashier"]),
            (DocumentCategory.StudyNote, ["ders", "ödev", "not", "note", "chapter", "bölüm", "tanım"]),
            (DocumentCategory.Report,    ["rapor", "report", "analiz", "analysis", "sonuç", "result"]),
        };

        var scores = new Dictionary<string, float>();
        foreach (var (category, keywords) in rules)
        {
            var matchCount = keywords.Count(kw => lower.Contains(kw));
            scores[category.ToString()] = matchCount / (float)keywords.Length;
        }

        var best = scores.OrderByDescending(kv => kv.Value).First();
        var bestCategory = Enum.Parse<DocumentCategory>(best.Key);
        return new ClassificationResult(
            (int)bestCategory == 0 ? DocumentCategory.Other : bestCategory,
            best.Value,
            scores);
    }
}

// ML.NET veri modelleri

public class DocumentTextInput
{
    [ColumnName("Text")]
    public string Text { get; set; } = string.Empty;
}

public class DocumentCategoryPrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedLabel { get; set; } = string.Empty;

    [ColumnName("Score")]
    public float[]? Score { get; set; }
}
