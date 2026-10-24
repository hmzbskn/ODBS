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

## Aşama 5 — Ders kaydı doğrulama motoru

- Durum: tamamlandı
- `IRegistrationRule` (Application/Registration): her kural `Evaluate(RegistrationContext)` ile
  `RegistrationIssue` (seviye Hata/Uyarı/Bilgi, mesaj, ilgili dersler) döner. Kurallar saf; OBİS'e
  gitmez, yalnızca bağlamı okur. Yeni kural = yeni sınıf; `AddRegistrationRules` derlemeyi tarayıp
  hepsini DI'a kaydeder.
- 6 kural: `TIME_CONFLICT` (Hata), `PREREQUISITE` (Hata), `LOWER_SEMESTER_PRIORITY` (Uyarı, kalınmış ve
  hiç alınmamış ayrı mesaj), `CREDIT_LIMIT` (Hata, GANO'ya göre AKTS sınırı), `EXAM_CONFLICT` (Hata),
  `RETAKE` (Bilgi, BB ve üstü ile geçilmiş ders).
- Eşikler `appsettings.json > RegistrationRules` bölümünden ayarlanır (GANO bantları, AKTS sınırları,
  tekrar alma not eşiği).
- Bağlam, doğrulanan dönemden ÖNCEKİ transkripti kullanır; geçmiş bir dönem doğrulanırken sonraki
  dönemlerin sonuçları önkoşul/GANO hesabına sızmaz. GANO transkriptten hesaplanır (kredi ağırlıklı,
  tekrarda son deneme geçerli) ve OBİS'in `Gpa` alanıyla 63 öğrencide birebir uyuşur.
- API: `GET /api/registrations/students?term=` (danışman: yalnızca kendi öğrencileri, admin: hepsi;
  uyarı rozet sayıları + son karar), `GET /api/registrations/{no}?term=` (dersler, çakışanlar işaretli
  haftalık program, bulgular, karar geçmişi), `POST /api/registrations/{no}/review` (onayla / düzeltme
  iste; düzeltmede not zorunlu; karar geçmişi tutulur, en son kayıt geçerli).
- Enum'lar API'de metin olarak döner (`JsonStringEnumConverter`).
- Testler: 72 test yeşil. Her kural için sınır/olumsuz durumlar (bitişik saat, farklı gün, başka şube,
  aynı dönemde alınan önkoşul, gelecek dönemde geçilen önkoşul, GANO bantları, eşik ayarı...), 6 uç durum
  öğrencisinin gerçek seed verisiyle yakalanması ve API yetki/sahiplik/karar testleri.
- Canlı duman testi yapıldı (mock OBİS + API gerçek HTTP ile): giriş, JWT, 20241011 için
  CREDIT_LIMIT + TIME_CONFLICT + LOWER_SEMESTER_PRIORITY, 401, Swagger 200.

### Aşama 5'te bulunup düzeltilen seed/altyapı hataları

1. **Ortak şube bozulması (Aşama 2 hatası):** EDGE-01 ve EDGE-05 için MAT101/A ve BLM203/A'nın
   saatleri TÜM öğrenciler için değiştiriliyordu; ilk sınıf öğrencilerinin hepsinde yapay saat
   çakışması çıkıyordu. Artık uç durum öğrencisine özel yeni şube ("C") eklenir, ortak şubeler
   değişmez.
2. **B şubesi çakışmaları:** B şubeleri dönemdeki başka bir dersin A saatine denk gelebiliyordu.
   B şubeleri artık dönemde kullanılmayan (gün, saat) kombinasyonlarına yerleşir.
3. **Seed tekrarlanabilir değildi:** seçmeli ders seçimi `string.GetHashCode()` kullanıyordu (.NET'te
   süreç başına rastgele). Sabit bir değerle değiştirildi; `--generate-seed` iki kez çalıştırılıp
   çıktının birebir aynı olduğu doğrulandı.
4. **`HttpObisClient` enum çözemiyordu:** OBİS gün ve sınav türünü metin gönderiyor ("Monday",
   "Midterm"); istemci sayı bekliyordu. `JsonStringEnumConverter` eklendi (canlı testte yakalandı).

**Bilinen eksik / not:** Baseline öğrenciler kaldıkları dersi sonraki dönemde tekrar almadan ilerliyor
(%92 geçme oranlı rastgele üretim). Bu yüzden uç durum olmayan birçok öğrencide gerçek bir
`LOWER_SEMESTER_PRIORITY` uyarısı ya da önkoşul hatası çıkıyor. Kural doğru çalışıyor; bu, veri
üretiminin gerçekçilik sınırı. Aşama 11'de uç durum raporu hazırlanırken bu fazlalık ayrıca ele alınacak.

## Aşama 6 — Frontend iskeleti, giriş, panel düzeni

- Durum: tamamlandı
- `frontend/`: Vite + React 19 + TypeScript. Tek ek bağımlılık `react-router-dom`; stil saf CSS
  (token'lı, koyu tema desteği, Tailwind/UI kiti yok).
- `api/client.ts`: tek fetch sarmalayıcı. JWT'yi ekler, sunucunun Türkçe `message` alanını gösterir,
  durum koduna göre Türkçe yedek mesajlar (401/403/404/502), ağ hatasında "Sunucuya ulaşılamıyor".
  Süresi dolan oturumda (401) otomatik çıkış.
- `auth/AuthContext.tsx`: giriş/çıkış, oturum `localStorage`'da, süresi dolmuş oturum yüklenmez.
- `RequireAuth`: giriş zorunluluğu + rol kısıtı (yetkisiz rol → ana sayfa).
- `Layout`: kenar menü (role göre süzülür), üst çubukta kullanıcı + rol rozeti + çıkış; 800 px altında
  menü çekmeceye dönüşür (mobilde bozulmaz).
- Ortak durum bileşenleri: yükleniyor / hata (tekrar dene) / boş. `useAsync` kancası bunları sürer.
- Sayfalar: giriş (demo hesaplar tek tıkla), genel bakış (danışman/yönetici: öğrenci, hatalı kayıt,
  karar bekleyen sayıları + önce bakılacaklar; öğrenci: dönem, GANO, AKTS, danışman kararı — gerçek API
  verisi), henüz yapılmamış modüller için "Yakında" yer tutucuları (hangi aşamada geleceğini söyler), 404.
- Geliştirmede `/api` istekleri Vite proxy ile `http://localhost:5154`'e yönlenir (CORS gerekmez);
  adres `VITE_API_TARGET` ile değişir.
- `tsc -b`, `oxlint` (0 uyarı) ve `vite build` temiz. Canlı doğrulama: mock OBİS + API + Vite birlikte
  çalıştırıldı; proxy üzerinden giriş (200), yanlış şifre (401, Türkçe mesaj), SPA yönlendirmesi (200).

**Bilinen eksik:** Arayüz bir tarayıcıda gözle kontrol edilmedi (bu ortamda tarayıcı aracı yoktu);
düzen/renk doğrulaması Aşama 7'de ders kaydı ekranıyla birlikte yapılacak.
