# EDITT Integration Adapter

Bu proje yalnızca EDITT'e ait veritabanı, entity ve storage işlemlerini bilir.
Platform çekirdeği bu projeye referans vermez.

İlk görevler:

- Mevcut `Dokuman` kaydından güvenli `SourceReference` üretme
- `ConversionRequested` yayınlama
- `ConversionCompleted` ve `ConversionFailed` olaylarını tüketme
- Sonucu mevcut gösterim kopyası kaydetme akışına bağlama
- Feature flag ile eski/yeni converter seçimi
