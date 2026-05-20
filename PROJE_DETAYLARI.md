# DocIntelligence - Akıllı Belge İşleme Sistemi

Bu proje, sisteme yüklenen dokümanların (Görsel ve PDF'ler) **optimize edilmesi (boyutunun küçültülmesi)**, **Optik Karakter Tanıma (OCR)** ile içindeki metinlerin çıkarılması ve son olarak **Makine Öğrenimi (ML.NET)** kullanılarak türlerine (Fatura, Kimlik vb.) göre sınıflandırılması amacıyla geliştirilmiştir.

Proje tasarımı **Clean Architecture (Temiz Mimari)** prensiplerine ve asenkron **Event-Driven (Olay Güdümlü)** mimariye göre kurgulanmıştır.

---

## 🏛️ 1. Mimari Tasarım (Neden Clean Architecture?)

Geleneksel projelerde tüm kodlar bir aradadır (Spagetti kod). Clean Architecture ise projeyi "Soğan" (Onion) gibi katmanlara ayırır.
**En büyük kural şudur:** İçteki katman (Domain) asla dıştaki katmanları (Veritabanı, WebAPI) bilemez. Bağımlılıklar daima dıştan içe doğrudur. Bu bize ne sağlar?
* Yarın **SQL Server** yerine **PostgreSQL**'e geçmek isterseniz sadece *Infrastructure* katmanını değiştirirsiniz; projenin geri kalanının haberi bile olmaz.
* Yarın **Tesseract OCR** yerine **Azure Vision API**'ye geçmek isterseniz, iş mantığı (*Application*) hiç değişmez.

---

## 📂 2. Katmanlar ve Klasörlerin Görevleri

### 📦 1. `DocIntelligence.Domain` (Çekirdek Katman)
Projenin kalbidir. Hiçbir dış kütüphaneye (NuGet paketine) bağlı değildir.
* **`Entities/`**: Veritabanı tablolarının C# nesnesi karşılıkları. 
  * `Document.cs`: Belgenin temel bilgilerini (durum, dosya boyutu, OCR metni) tutan varlık.
  * `ProcessingJob.cs`: İşlem adımlarını loglamak için kullanılır.
* **`Enums/`**: Sabit durumlar. 
  * `DocumentStatus.cs`: *Pending, Optimizing, Classifying, Completed* gibi belge durumları.
* **`Interfaces/`**: Sözleşmeler. Örneğin `IOcrService` veya `IDocumentRepository` burada tanımlanır ama içleri boştur! *"Nasıl"* yapılacağı dış katmana bırakılır, *"Ne"* yapılacağı burada söylenir.

### ⚙️ 2. `DocIntelligence.Application` (İş Akışı Katmanı)
Kullanıcının sisteme ne yaptırmak istediğini barındırır. CQRS (Command/Query Responsibility Segregation) deseni için **MediatR** kütüphanesi kullanılmıştır.
* **`DTOs/`**: (Data Transfer Object) Veritabanı Entity'lerini doğrudan API'den dışarı açmak güvenlik açığıdır. Bu yüzden sadece istenilen veriler DTO'lara dönüştürülüp dışarı verilir.
* **`UseCases/Documents/`**: 
  * `UploadDocumentCommandHandler.cs`: Kullanıcı dosya yüklediğinde çalışır. (Kayıt, Storage'a aktarım, RabbitMQ'ya mesaj atma)
* **`Validators/`**: **FluentValidation** ile dosya boyutu (max 50mb) veya dosya uzantılarının (sadece pdf, jpg, png vb.) kurallarını denetler.

### 🔌 3. `DocIntelligence.Infrastructure` (Dış Dünya Katmanı)
Dış kütüphanelerin, veritabanının ve dosya sisteminin gerçekte çalıştığı yerdir. Domain'deki Interface'leri uygular.
* **`Persistence/`**: **EF Core** DbContext ve tablo ayarlarının (`Configurations`) olduğu yer.
* **`Repositories/`**: Veritabanı CRUD (Ekle, Sil, Güncelle, Getir) işlemleri.
* **`Storage/`**: Dosyaları klasörlere kaydeden mekanizma.
* **`Services/`**:
  * `TesseractOcrService.cs`: Tesseract ile metin okuma işlemi.
  * `MlDocumentClassifier.cs`: ML.NET kullanarak metnin konusunu anlama işlemi.
  * `DocumentOptimizer.cs`: ImageSharp/iText7 ile dosya boyutunu düşürme işlemleri.
* **`Messaging/`**: **RabbitMQ** ile mesaj gönderim ve alım servisi.

### 🌍 4. `DocIntelligence.WebAPI` (Sunum Katmanı)
Kullanıcıların (veya bir Frontend uygulamasının) HTTP üzerinden istek attığı kapıdır. 
* Sadece **Controller**'ları (Swagger) barındırır.
* Asla veritabanına direkt bağlanmaz. İsteği alır, **Application (MediatR)** katmanına iletir ve cevabı döner.

### 👷 5. `DocIntelligence.Worker` (Arka Plan Hizmeti)
Kullanıcı dosya yüklediğinde API *"Başarıyla alındı, işlem sırasına koyuldu"* (HTTP 202) döner. Peki asıl zor işi (OCR, Küçültme, ML) kim yapar?
* API kullanıcısını bu ağır işlemlerle bekletip timeout aldırtmamak için bu Worker servisi yazılmıştır.
* 7/24 arkada çalışıp RabbitMQ'yu dinler. "Yeni dosya geldi" mesajını duyar duymaz sessiz sedasız arkada tüm optimizasyon ve OCR süreçlerini işletir.

---

## 🔑 3. Projedeki En Önemli Kodların Anlamları

### Neden API ile Worker Ayrı? Neden RabbitMQ?
`UploadDocumentCommandHandler.cs` içerisindeki şu kod blokları bu projenin en hayati kısımlarıdır:
```csharp
// 1. Veritabanına belgenin sadece META bilgilerini ekliyoruz.
await _repository.AddAsync(document, cancellationToken);

// 2. Mesaj kuyruğuna "işle" mesajı at (API KULLANICIYI BEKLETMEZ!)
var message = new DocumentUploadedMessage(document.Id, storagePath, ...);
await _broker.PublishAsync("document.processing", message, cancellationToken);
```
**Neden Önemli?** Bir PDF'ten OCR ile metin çıkarmak dosyanın sayfasına göre 30 saniye ile 2 dakika arasında sürebilir. Eğer bu işlemi API içinde yapsaydık, kullanıcının ekranında "Yükleniyor..." ibaresi takılı kalır, sistem kitlenir veya hata (Timeout) atardı. RabbitMQ sayesinde *"Dosyayı aldım sen gidebilirsin"* diyoruz, ağır işi arka plana (Worker) yıkıyoruz.

### Worker'ın Pipeline Mantığı
`DocumentProcessingWorker.cs` içerisindeki `ProcessDocumentAsync` metodu bir bant (pipeline) sistemidir:
```csharp
// ADIM 1: OPTİMİZASYON (Boyut küçültme)
var optimizationResult = await optimizer.OptimizePdfAsync(...);

// ADIM 2: OCR (Metni okuma)
var ocrResult = await ocr.ExtractTextAsync(...);

// ADIM 3: SINIFLANDIRMA (Yapay Zeka ile Karar verme)
var classResult = await classifier.ClassifyAsync(ocrResult.Text);

// İŞLEM TAMAM: Veritabanını güncelle
document.SetStatus(DocumentStatus.Completed);
```
**Neden Önemli?** İşlem adımları birbirinden izoledir. Adımları veritabanında "Classifying", "ExtractingText" diyerek tek tek güncelliyoruz. İleride sistemde ön yüze (React/Vue.js) "İşlem %50'de, OCR Yapılıyor..." gibi canlı bildirim göndermek istersek bu statüleri kullanabiliriz.

### TesseractOcrService - Akıllı Tercih Yöntemi
```csharp
public async Task<OcrResult> ExtractTextAsync(...)
{
    if (contentType == "application/pdf")
        return await ExtractFromPdfAsync(fileStream, ...); // iText7 ile direkt metni çek
    
    return await ExtractFromImageAsync(fileStream, ...); // Resimse Tesseract ile analiz et
}
```
**Neden Önemli?** PDF dosyalarının içinde genellikle resim değil, kodlanmış metinler olur. Boş yere PDF'i Tesseract'a verip görsel işleme yapıp sistemi yormak yerine `iText7` ile saniyeler içinde direkt gerçek metni %100 doğrulukla çekiyoruz. Sadece resimler için (JPEG/PNG) OCR'ı çalıştırarak sistemi mükemmel şekilde optimize ettik.

### ML Sınıflandırma ve Kural Tabanlı Fallback
`MlDocumentClassifier.cs` içindeki yapı:
Eğer ML modeliniz yoksa sistemin çökmemesi için yazılan yedek mekanizma:
```csharp
var rules = new List<(DocumentCategory Category, string[] Keywords)>
{
    (DocumentCategory.Invoice,   ["fatura", "invoice", "kdv", "vat", "vergi"]),
    (DocumentCategory.Identity,  ["kimlik", "tc kimlik", "nüfus", "passport"]),
};
```
**Neden Önemli?** Gerçek dünya projelerinde Yapay Zeka modeli ilk günden elde hazır olmaz. Model hazır olana kadar veya ML.NET çuvalladığında kelime eşleştirme üzerinden bir mantık yürütebilmesi "Graceful Degradation" denilen, projenin tamamen durması yerine kısıtlı kapasiteyle hayatta kalması stratejisidir.

---

## 🛠️ 4. Kullanılan Kütüphaneler ve Seçim Nedenleri

* **Entity Framework Core (EF Core):** SQL sorguları yazmak yerine (Select, Insert vb.) C# kodları ile veritabanını yönetmek için. (ORM).
* **MediatR:** CQRS pattern için kullanıldı. Controller'ları çok hafif hale getirip, "Yükle" veya "Sil" gibi emirlerin tek bir sorumluluk altında kodlanmasını sağlar.
* **AutoMapper (v16.1.1):** Birbirine benzeyen `Document` entity'si ile `DocumentDto` sınıflarındaki 15 değişkeni manuel olarak `x.Name = y.Name` yazmaktan bizi kurtaran otomatik nesne kopyalama kütüphanesi.
* **RabbitMQ.Client:** Asenkron mesajlaşma broker'ı. Uygulama kapatılsa veya çökse bile içindeki mesajları (işleri) kaybetmediği (Durable) için tercih edildi.
* **Tesseract OCR:** Google tarafından desteklenen, ücretsiz ve en yetenekli karakter tanıma kütüphanesi olduğu için seçildi.
* **SixLabors.ImageSharp:** Resimleri kırpmak, yeniden boyutlandırmak ve kalitesini (compress) düşürmek için C# dünyasındaki en performanslı kütüphanedir.
* **iText7:** PDF'ler üzerinde doğrudan metin ayıklamak ve PDF sıkıştırması yapmak için kullanıldı.
* **Microsoft.ML:** Makine öğrenimi süreçlerini Python'a gerek kalmadan, doğrudan .NET ekosisteminde gerçekleştirebilmek için eklendi.

Bu mimari; binlerce doküman aynı anda gelse dahi, web sunucusunu kilitlemeden tüm ağır yükü RabbitMQ aracılığıyla Worker sunuculara dağıtabilen, sektör standartlarına (*Enterprise level*) uygun güçlü bir tasarımdır.
