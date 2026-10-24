# İlerleme Günlüğü

Bu dosya, çalışma sırasındaki her aşamanın sonunda güncellenir: ne yapıldı, derleme/test
durumu, bilinen eksikler.

## Aşama 1 — Solution ve proje iskeleti

- Durum: tamamlandı
- `OgrenciDanismanOtomasyonu.sln`: Advisor.Domain, Advisor.Application,
  Advisor.Infrastructure, Advisor.Api (controller tabanlı), Obis.MockApi (Minimal API),
  Advisor.Tests (xUnit). Katman referansları kuruldu (bkz. docs/ARCHITECTURE.md).
- Build+test yeşil.

## Aşama 2 — Mock OBİS API ve seed verisi

- Durum: tamamlandı
- 52 derslik 8 dönemlik Bilgisayar Mühendisliği müfredatı (`CourseCatalog.cs`),
  önkoşul zinciri: BLM101→BLM103→BLM201→BLM204→BLM304→BLM401 vb.
- 3 danışman, 63 öğrenci (sınıf1:15, sınıf2:15, sınıf3:15, sınıf4:18), deterministik
  isim üretimi (`NamePools`, seed=1337).
- `SeedGenerator`: geçmiş dönem transkriptleri (~%92 geçme oranı), güncel dönem kaydı
  (Beklemede), devam kaydı, haftalık ders programı (dönem içi çakışmasız), sınav
  takvimi (dönem içi çakışmasız, dönemler arası kasıtlı çakıştırılabilir).
- **10 bilinçli uç durum öğrencisi** üretildi ve doğrulandı (liste: docs/DECISIONS.md).
  EDGE-01 (saat çakışması) ve EDGE-05 (sınav çakışması) üretilen JSON üzerinden elle
  doğrulandı (MAT101/A ve BLM301/A aynı gün/saat; BLM203 ve BLM401 final'leri aynı
  tarih/saat).
- 8 endpoint (`ObisEndpoints.cs`): `/api/students`, `/api/students/{no}`,
  `/students/{no}/transcript`, `/registrations?term=`, `/attendance?term=`,
  `/api/courses`, `/api/schedule?term=`, `/api/exams?term=`, `/api/curriculum`.
- `dotnet run -- --generate-seed` seed'i üretir/yeniden üretir; normal çalışma zamanı
  sadece `SeedData/json/*.json`'ı okur.
- Build+test yeşil (1/1 — henüz asıl iş mantığı testi yok, Aşama 5'te gelecek).

**Bilinen eksik:** `/api/*` endpoint'leri henüz canlı `dotnet run` ile elle
duman testinden geçirilmedi (CLI izin katmanı bu oturumda `dotnet run` gibi ağ
dinleyen komutları engelliyordu — bkz. docs/DECISIONS.md). Program.cs derleniyor ve
mantık JSON üzerinden statik olarak doğrulandı; entegrasyon testleri (Aşama 5/11)
bunu `WebApplicationFactory` ile in-process doğrulayacak.

## Aşama 3 — Domain modelleri, veritabanı, IObisClient adapter'ı

- Durum: tamamlandı (commit `99fb938`)
- Domain entity'leri (kullanıcı, kayıt onayı, anket, randevu/görüşme/duyuru/talep),
  `AdvisorDbContext` (SQLite) + `InitialCreate` migration, `IObisClient` / `HttpObisClient`
  (önbellekli HTTP istemcisi). OBİS verisi ana veritabanında tutulmaz.

## Aşama 4 — Kimlik doğrulama, roller, demo hesaplar

- Durum: tamamlandı
- JWT (HS256) giriş: `POST /api/auth/login`, `GET /api/auth/me`. Şifreler BCrypt ile saklanır.
- Roller: Admin, Danisman, Ogrenci. Varsayılan yetkilendirme politikası **her endpoint için
  giriş ister**; açık olanlar `[AllowAnonymous]` ile işaretlenir (şimdilik yalnızca login).
- `ICurrentUser` (JWT claim'lerinden) ve `IStudentAccessGuard`: admin hepsini, öğrenci yalnızca
  kendini, danışman yalnızca OBİS'te kendisine bağlı öğrencileri görür. Sonraki aşamalardaki
  tüm öğrenci endpoint'leri bu guard'ı kullanacak.
- Global exception middleware: Türkçe, tek biçimli JSON hata gövdesi; OBİS erişim hatası 502.
- Demo hesaplar uygulama açılışında (kullanıcı tablosu boşsa) oluşturulur; bkz. README.
- Swagger'da Bearer yetkilendirme butonu var.
- 10 yeni test (giriş, 401/403, rol ayrımı, danışman/öğrenci sahiplik sınırı, seeder idempotent).
  Toplam build+test yeşil (11/11).
