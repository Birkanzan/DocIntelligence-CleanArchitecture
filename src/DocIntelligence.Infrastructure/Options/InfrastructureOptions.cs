namespace DocIntelligence.Infrastructure.Options;

/// <summary>
/// appsettings.json'dan okunacak seçenek sınıfları.
/// Her servis kendi bağımsız seçenek nesnesini alır.
/// </summary>

public class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";
    public string BasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "uploads");
}

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

public class TesseractOptions
{
    public const string SectionName = "Tesseract";
    public string DataPath { get; set; } = "tessdata";
    public string Language { get; set; } = "tur+eng";
}

public class MlOptions
{
    public const string SectionName = "ML";
    public string? ModelPath { get; set; }
    public string TrainingDataPath { get; set; } = "ml_training_data.csv";
}
