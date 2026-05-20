# DocIntelligence - Akıllı Belge İşleme Sistemi

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Message%20Broker-orange.svg)](https://www.rabbitmq.com/)
[![Clean Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-brightgreen.svg)]()

DocIntelligence; sisteme yüklenen dokümanların (Görsel ve PDF'ler) **optimize edilmesi (boyutunun küçültülmesi)**, **Optik Karakter Tanıma (OCR)** ile içindeki metinlerin çıkarılması ve son olarak **Makine Öğrenimi (ML.NET)** kullanılarak türlerine (Fatura, Kimlik vb.) göre otomatik sınıflandırılması amacıyla geliştirilmiş dağıtık bir sistemdir.

## 🚀 Özellikler

* **Gelişmiş Mimari:** Clean Architecture (Temiz Mimari) ve CQRS (MediatR) prensiplerine uygun tasarlanmıştır.
* **Asenkron İşleme:** Kullanıcıyı bekletmemek adına işlemler **RabbitMQ** üzerinden arka plan (Worker) servislerine iletilir.
* **Akıllı OCR:** PDF dosyaları için `iText7` ile doğrudan metin ayıklama yapılırken, görsel dosyalar için `Tesseract OCR` kullanılır.
* **Makine Öğrenimi Sınıflandırması:** `ML.NET` ile eğitilmiş model kullanılarak belgeler içeriklerine göre kategorize edilir (Makine öğrenimi devrede olmadığı durumlarda kural tabanlı fallback mekanizması çalışır).
* **Boyut Optimizasyonu:** `ImageSharp` ve `iText7` entegrasyonu ile saklama alanından tasarruf edilir.

## 🛠️ Teknolojiler

* **Framework:** .NET 9
* **Veritabanı:** Entity Framework Core (SQL Server LocalDB)
* **Message Broker:** RabbitMQ
* **Örüntü ve Mimari:** Clean Architecture, CQRS (MediatR), Event-Driven
* **Kütüphaneler:** AutoMapper, FluentValidation, Tesseract OCR, ImageSharp, iText7, Microsoft.ML

## 📂 Mimari Tasarım
Proje, bağımlılıkların dıştan içe doğru olduğu katmanlı bir yapıda tasarlanmıştır:
1. `DocIntelligence.Domain`: Core katman (Entity, Enum, Interface).
2. `DocIntelligence.Application`: İş kuralları, DTO'lar, MediatR Use-Case'leri.
3. `DocIntelligence.Infrastructure`: DB, RabbitMQ, OCR ve ML servis implementasyonları.
4. `DocIntelligence.WebAPI`: Kullanıcının HTTP üzerinden iletişim kurduğu REST API.
5. `DocIntelligence.Worker`: RabbitMQ'yu dinleyen arka plan işleyicisi.

---

## 💻 Kurulum ve Çalıştırma

### Ön Koşullar
* [.NET 9 SDK](https://dotnet.microsoft.com/download)
* SQL LocalDB (`sqllocaldb info` komutuyla kontrol edebilirsiniz)
* [Docker Desktop](https://www.docker.com/products/docker-desktop) (RabbitMQ için)

### 1. RabbitMQ'yu Başlatın
Docker yüklü sisteminizde aşağıdaki komutu terminalde çalıştırın:
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```
*(Sonraki açılışlarda sadece `docker start rabbitmq` demeniz yeterlidir.)*

### 2. Web API'yi Ayağa Kaldırın
Proje dizininde terminali açıp aşağıdaki komutu girin (Veritabanı migration'ları ilk açılışta otomatik yapılacaktır):
```bash
dotnet run --project src\DocIntelligence.WebAPI\DocIntelligence.WebAPI.csproj
```
_API http://localhost:5000 adresinde ayağa kalkacaktır (Swagger)._

### 3. Worker Servisini Ayağa Kaldırın
Ağır yükü üstlenen Worker servisini çalıştırmak için **yeni bir terminal** penceresinde:
```bash
dotnet run --project src\DocIntelligence.Worker\DocIntelligence.Worker.csproj
```

### 4. Web Arayüzünü Açın
`src\DocIntelligence.WebUI\index.html` dosyasını tarayıcınızda açarak sistemi hemen test etmeye başlayabilirsiniz.

## 📝 Notlar
* Belgeler varsayılan olarak `C:\DocIntelligence\Storage\` dizininde saklanır (Bu yol `appsettings.json` içerisinden değiştirilebilir).
* Tesseract OCR için gerekli olan `tur.traineddata` ve `eng.traineddata` dil dosyalarının `src\DocIntelligence.Worker\tessdata\` dizininde bulunduğundan emin olun.

---
_Bu proje dağıtık sistemler, asenkron iletişim ve temiz kodlama (Clean Code) standartları gözetilerek geliştirilmiştir._
