# DocIntelligence — Calistirma Kilavuzu

Projenin tum bileskenlerini sifirdan ayaga kaldirmak icin bu adimlari sirayla uygula.

---

## Mimari Ozet

Calismas gereken bileskenler (sirayla):
1. SQL Server LocalDB - veritabani
2. RabbitMQ - mesaj kuyrugu (Docker ile)
3. WebAPI - REST API sunucusu
4. Worker - arka plan islem servisi
5. Web UI - tarayici arayuzu

---

## On Kosullar

- .NET 9 SDK   -> dotnet --version
- SQL LocalDB  -> sqllocaldb info
- Docker       -> docker --version

---

## 1) SQL Server LocalDB'yi Basalt

LocalDB genellikle arka planda hazirdir; yoksa:

  sqllocaldb start MSSQLLocalDB

Veritabanini MANUEL OLUSTURMANA GEREK YOK.
WebAPI ilk acilista EF Core Migration'lari otomatik uygular.

Baglanti dizgisi (appsettings.json'da zaten ayarli):
  Server=(localdb)\mssqllocaldb;Database=DocIntelligenceDb;Trusted_Connection=True

---

## 2) RabbitMQ'yu Docker ile Basalt

Ilk kez calistirma (once docker pull yapar, birkaç dk surebilir):

  docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

Dogrulama:
  docker ps    -> rabbitmq satiri "Up" durumunda olmali

RabbitMQ yonetim paneli:
  http://localhost:15672
  Kullanici adi: guest
  Sifre:        guest

Sonraki acilislarda sadece:
  docker start rabbitmq

---

## 3) WebAPI'yi Basalt

Yeni bir PowerShell penceresi ac:

  cd "C:\Users\birka\OneDrive\Desktop\NETProje"
  dotnet run --project src\DocIntelligence.WebAPI\DocIntelligence.WebAPI.csproj

Ciktida su gorulmeli:
  Now listening on: http://localhost:5000

Swagger arayuzu: http://localhost:5000

---

## 4) Worker'i Basalt

Ayri bir PowerShell penceresi ac:

  cd "C:\Users\birka\OneDrive\Desktop\NETProje"
  dotnet run --project src\DocIntelligence.Worker\DocIntelligence.Worker.csproj

Ciktida su gorulmeli:
  DocumentProcessingWorker baslatildi. Kuyruk dinleniyor: document.processing

---

## 5) Web UI'yi Ac

Dosya gezgininde su dosyayi cift tikla veya tarayiciya surukle:
  src\DocIntelligence.WebUI\index.html

Sidebar'da "API cevrimici" (yesil nokta) gorunuyorsa her sey hazir.

---

## Hizli Baslat (Her Seferinde)

  docker start rabbitmq
  # Yeni pencere -> WebAPI
  dotnet run --project src\DocIntelligence.WebAPI\DocIntelligence.WebAPI.csproj
  # Yeni pencere -> Worker
  dotnet run --project src\DocIntelligence.Worker\DocIntelligence.Worker.csproj
  # index.html'i tarayicida ac

---

## Kapalis Sirasi

  # Her iki pencerede Ctrl+C
  docker stop rabbitmq

---

## Sik Karsilasilan Hatalar

### RabbitMQ baglanti hatasi
  RabbitMQ.Client.Exceptions.BrokerUnreachableException
  Cozum: docker start rabbitmq

### LocalDB baglanti hatasi
  Cannot open database "DocIntelligenceDb"
  Cozum: sqllocaldb start MSSQLLocalDB

### Port 5000 kullanimda
  Failed to bind to address http://0.0.0.0:5000
  Cozum:
    netstat -ano | findstr :5000
    taskkill /PID <PID> /F

### tessdata bulunamadi (OCR hatasi)
  TesseractException: Failed to initialise tesseract engine
  Cozum: Su dosyalarin var oldugundan emin ol:
    src\DocIntelligence.Worker\tessdata\tur.traineddata
    src\DocIntelligence.Worker\tessdata\eng.traineddata
  Indir: https://github.com/tesseract-ocr/tessdata

### Migration hatasi
  dotnet tool install --global dotnet-ef
  dotnet ef migrations add InitialCreate --project src\DocIntelligence.Infrastructure --startup-project src\DocIntelligence.WebAPI
  dotnet ef database update --project src\DocIntelligence.Infrastructure --startup-project src\DocIntelligence.WebAPI

---

## Depolama Yolu

Yuklenen belgeler su dizine kaydedilir:
  C:\DocIntelligence\Storage\

appsettings.json'dan degistirilebilir:
  "Storage": { "Local": { "BasePath": "C:\\DocIntelligence\\Storage" } }
