# Öğrenci–Danışman Otomasyonu

Akademik danışmanların işini kolaylaştıran bir web uygulaması. Üniversitenin öğrenci
bilgi sistemi (OBİS) verilerini (mock bir OBİS API üzerinden) çeker; ders kaydı
doğrulama, ihtiyaç puanlama, erken uyarı/mezuniyet analizi ve danışman-öğrenci
iletişimi modülleriyle danışmana karar desteği sağlar.

> Kurulum adımları, demo hesaplar ve ekran açıklamaları proje bitince bu dosyaya
> eklenecek (bkz. `docs/PROGRESS.md`).

## Mimari

Detaylar için `docs/ARCHITECTURE.md`. Kısaca: .NET 8 katmanlı backend
(Domain/Application/Infrastructure/Api) + ayrı bir mock OBİS API projesi + React/TS
frontend.

## Geliştirme günlüğü

- `docs/PROGRESS.md` — aşama aşama ne yapıldı
- `docs/DECISIONS.md` — belirsiz noktalarda alınan kararlar ve gerekçeleri
