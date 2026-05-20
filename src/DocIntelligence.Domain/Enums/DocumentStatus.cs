namespace DocIntelligence.Domain.Enums;

/// <summary>
/// Bir belgenin yaşam döngüsü boyunca geçebileceği durumları tanımlar.
/// </summary>
public enum DocumentStatus
{
    /// <summary>Yüklendi, işlem kuyruğa alındı.</summary>
    Pending = 0,

    /// <summary>Optimizasyon (boyut küçültme) işlemi devam ediyor.</summary>
    Optimizing = 1,

    /// <summary>OCR metin çıkarma işlemi devam ediyor.</summary>
    ExtractingText = 2,

    /// <summary>ML.NET ile sınıflandırma yapılıyor.</summary>
    Classifying = 3,

    /// <summary>Tüm işlemler başarıyla tamamlandı.</summary>
    Completed = 4,

    /// <summary>İşlem sırasında hata oluştu.</summary>
    Failed = 5
}

/// <summary>
/// ML.NET tarafından tahmin edilen belge kategorisi.
/// </summary>
public enum DocumentCategory
{
    Unknown = 0,
    Invoice = 1,
    Identity = 2,
    Contract = 3,
    Receipt = 4,
    StudyNote = 5,
    Report = 6,
    Other = 7
}

/// <summary>
/// Dosyanın türü.
/// </summary>
public enum FileType
{
    Unknown = 0,
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
    Tiff = 4,
    Bmp = 5
}
