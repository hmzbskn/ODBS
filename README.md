# Öğrenci–Danışman Otomasyonu

Akademik danışmanların işini kolaylaştıran bir web uygulaması. Üniversitenin öğrenci
bilgi sistemi (OBİS) verilerini (mock bir OBİS API üzerinden) çeker; ders kaydı
doğrulama, ihtiyaç puanlama, erken uyarı/mezuniyet analizi ve danışman-öğrenci
iletişimi modülleriyle danışmana karar desteği sağlar.

> Kurulum adımları, demo hesaplar ve ekran açıklamaları proje bitince bu dosyaya
> eklenecek (bkz. `docs/PROGRESS.md`).

## Demo hesaplar

Tüm hesapların şifresi: `Demo123!` (yalnızca geliştirme amaçlı). Hesaplar API ilk açıldığında oluşur.

| Kullanıcı adı | Rol | Bağlı kayıt |
| --- | --- | --- |
| `admin` | Admin | - |
| `danisman1` | Danışman | ADV1 (Elif Yıldız) |
| `danisman2` | Danışman | ADV2 (Mehmet Arslan) |
| `danisman3` | Danışman | ADV3 (Zeynep Kara) |
| `ogrenci1` | Öğrenci | 20261001 Can Şahin |
| `ogrenci2` | Öğrenci | 20241011 Buse Ateş (saat çakışması uç durumu) |
| `ogrenci3` | Öğrenci | 20251006 Selin Kılıç (önkoşul ihlali uç durumu) |
| `ogrenci4` | Öğrenci | 20231016 Caner Polat (sınav çakışması uç durumu) |

Giriş: `POST /api/auth/login`, dönen token Swagger'daki **Authorize** düğmesine yapıştırılır.

## Mimari

Detaylar için `docs/ARCHITECTURE.md`. Kısaca: .NET 8 katmanlı backend
(Domain/Application/Infrastructure/Api) + ayrı bir mock OBİS API projesi + React/TS
frontend.

## Geliştirme günlüğü

- `docs/PROGRESS.md` — aşama aşama ne yapıldı
- `docs/DECISIONS.md` — belirsiz noktalarda alınan kararlar ve gerekçeleri
