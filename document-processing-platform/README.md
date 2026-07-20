# Document Processing Platform

DB bağımsız, RabbitMQ tabanlı, genişletilebilir belge dönüştürme ve gösterim kopyası üretme platformu.

## İlk hedef

`ConversionRequested` mesajını RabbitMQ üzerinden alan Windows Worker Service'in kaynak dosyayı çözmesi, dönüşümü gerçekleştirmesi, çıktıyı doğrulaması ve `ConversionCompleted` veya `ConversionFailed` olayı yayınlaması.

## Gereksinimler

- .NET SDK 10
- Docker Desktop veya RabbitMQ 4.x
- Windows üzerinde servis kurulumu için yönetici yetkisi

## Başlangıç

```powershell
Copy-Item .env.example .env
docker compose -f deploy/docker-compose.yml up -d
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Worker:

```powershell
dotnet run --project src/DocumentProcessing.Worker
```

Örnek publisher:

```powershell
dotnet run --project samples/DocumentProcessing.Sample.Publisher -- "C:\Docs\sample.pdf"
```

## İlk milestone kabul kriteri

1. Publisher `ConversionRequested` mesajı yayınlar.
2. Worker mesajı manual-ack ile alır.
3. Kaynak dosyayı izin verilen storage alias üzerinden çözer.
4. İlk vertical slice için PDF dosyasını doğrular ve artifact alanına kopyalar.
5. `ConversionCompleted` yayınlandıktan ve publisher confirm alındıktan sonra request ack edilir.
6. Kalıcı hata durumunda `ConversionFailed` yayınlanır ve mesaj DLQ'ya yönlendirilir.

## Solution yapısı

- `Contracts`: Taşıma ve domain-bağımsız mesaj sözleşmeleri
- `Core`: Use-case arayüzleri, sonuç modelleri ve hata kodları
- `Conversion`: Orchestrator, format detector ve provider seçimi
- `Messaging.RabbitMq`: RabbitMQ topolojisi, publisher ve consumer
- `Storage`: Alias tabanlı güvenli file reference çözümleme ve artifact store
- `Worker`: Windows Service host
- `Client`: Uygulamalara verilecek sade istemci sözleşmesi
- `Integration.Editt`: EDITT'e özel adapter; çekirdek bu projeye bağımlı değildir

## Branch yaklaşımı

- `main`: her zaman derlenebilir
- `develop`: ilk ay boyunca entegrasyon dalı
- `feature/<issue>-<kisa-ad>`

Her PR tek sorumluluk taşımalı, test içermeli ve ilgili issue'yu kapatmalıdır.
