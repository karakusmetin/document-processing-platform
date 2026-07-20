# İlk Sprint Görev Dağılımı

## Sen — Omurga ve ürün sınırları

1. GitHub repository, branch protection ve CI doğrulaması
2. RabbitMQ topology ve publisher confirm davranışı
3. Worker consumer, manual ack/nack akışı
4. Conversion orchestrator ve hata sözleşmeleri
5. EDITT adapter sınırlarının belirlenmesi

## Buddy — Storage ve ilk conversion provider'ları

1. `AliasFileReferenceResolver` testleri
2. `LocalArtifactStore` atomik yazma ve hash testleri
3. PDF pass-through output validation
4. Golden-file test altyapısı
5. Image/TIFF → PDF için lisans uygun provider araştırması ve PoC

## Ortak entegrasyon noktası

İkinizin işleri şu interface'lerde birleşir:

- `IDocumentSource`
- `IArtifactStore`
- `IFileFormatDetector`
- `IConversionProvider`
- `IConversionOrchestrator`

## İlk PR'lar

- PR-01: repository baseline ve CI
- PR-02: contracts + error codes
- PR-03: RabbitMQ topology + publisher
- PR-04: storage aliases + artifact store
- PR-05: worker consumer + PDF vertical slice
- PR-06: golden-file test harness
