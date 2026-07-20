# İlk Çalıştırma Kontrolü

```powershell
cd document-processing-platform
Copy-Item .env.example .env
docker compose --env-file .env -f deploy/docker-compose.yml up -d
New-Item -ItemType Directory -Force C:\DocumentProcessing\Incoming
New-Item -ItemType Directory -Force C:\DocumentProcessing\Artifacts
Copy-Item C:\Temp\sample.pdf C:\DocumentProcessing\Incoming\sample.pdf
dotnet restore
dotnet build -c Release
dotnet run --project src/DocumentProcessing.Worker
```

Başka terminal:

```powershell
dotnet run --project samples/DocumentProcessing.Sample.Publisher -- C:\DocumentProcessing\Incoming\sample.pdf
```

Beklenen çıktı:

```text
C:\DocumentProcessing\Artifacts\{job-id}\display.pdf
```

RabbitMQ yönetim ekranı:

```text
http://localhost:15672
```
