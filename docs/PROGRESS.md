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

## Aşama 7 — Ders kaydı doğrulama arayüzü

- Durum: tamamlandı
- Danışman/yönetici: `/kayitlar` — dönem seçimi (varsayılan: her öğrencinin güncel dönemi), ad/numara
  arama, "yalnızca bulgusu olanlar" süzgeci; her öğrencinin yanında hata/uyarı/bilgi rozetleri, AKTS,
  GANO ve danışman kararı. Satırdan `/kayitlar/:studentNo` detayına geçilir.
- Detay: özet kartları (dönem, GANO, AKTS/üst sınır, bulgu sayıları), seviyeye göre renkli kural bulguları,
  **haftalık ders programı** (gün x saat ızgarası; çakışan dersler kırmızı + "Çakışma" etiketi), seçilen
  dersler tablosu (çakışanlar kırmızı), danışman karar paneli, karar geçmişi.
- Karar paneli: "Onayla" ya da "Düzeltme iste" (not yazılmadan gönderilemez, sunucu da doğrular). Hatalı
  kayıt onaylanırken bilgilendirme çıkar ama engellenmez. Kayıttan sonra ekran yenilenir.
- Öğrenci: `/kayitlarim` — aynı ekranın salt okunur hali; danışmanın son kararı ve notu en üstte görünür.
- `formatGpa`: notlandırılmış dersi olmayan (ilk dönem) öğrencide GANO "0.00" yerine "-" gösterilir.
- Doğrulama: Playwright (geçici, projeye eklenmedi) ile gerçek tarayıcıda uçtan uca gezildi: giriş →
  genel bakış → liste → detay → not zorunluluğu (not boşken buton kapalı) → düzeltme isteme → öğrenci
  hesabında kararın görünmesi → 390 px mobil görünüm (yatay taşma yok, menü çekmecesi açılıyor).
  Konsol hatası yok. `tsc -b`, `oxlint` (0 uyarı) ve `vite build` temiz; backend 72/72 test yeşil.

## Aşama 8 — Anket ve ihtiyaç puanlama (backend + frontend + testler)

- Durum: tamamlandı
- **Anket oluşturma:** başlık, açıklama, başlangıç/bitiş tarihi, hedef (tüm öğrencilerim ya da tek sınıf),
  4 soru tipi (tek seçim, çoklu seçim, sayı, metin), zorunluluk, soru sırası. Doğrulamalar sunucuda
  (en az bir soru, seçim sorusunda 2-20 seçenek, tekrar eden soru/seçenek metni yok, tarih sırası...).
- **KVKK:** öğrenci formunda aydınlatma metni + açık rıza kutusu gösterilir; rıza işaretlenmeden
  gönder düğmesi kapalıdır ve sunucu da rızasız gönderimi reddeder (400). Rıza zamanı kaydedilir.
- **Puanlama:** soru puanı = (seçilen seçeneklerin puanı toplamı ya da girilen sayı) x soru ağırlığı;
  ağırlık/puan negatif olabilir. Puan her seferinde cevaplardan canlı hesaplanır; "Puanlama" sekmesinde
  ağırlık ve puanlar sonradan değiştirilince mevcut yanıtlar hemen yeniden puanlanır. Öğrenciye puanlar ve
  ağırlıklar hiçbir yanıtta gönderilmez (testle doğrulandı).
- **Sonuçlar:** puana göre sıralı liste (eşitlikte aynı sıra), her öğrencinin puanının soru soru kırılımı
  (toplam = kırılım toplamı), "ilk N öğrenciyi seç" ve "puanı X ve üstündekileri seç", destek türü atama /
  kaldırma (Yemek kartı, Burs...), CSV dışa aktarma (UTF-8 BOM, Türkçe Excel uyumlu, CSV enjeksiyon koruması).
- **CSV içe aktarma:** şablon indirme, `Öğrenci No` + soru sütunları, çoklu seçimde `|` ayırıcı, satır
  bazlı hata raporu (hedef kitlede olmayan öğrenci, tekrar, geçersiz seçenek/sayı, zorunlu boş). Geçerli
  satırlar alınır, hatalılar atlanıp raporlanır. KVKK onay kutusu zorunludur; içe aktarılan yanıtlar
  "CSV" etiketiyle işaretlenir.
- **Gizlilik:** yanıtları yalnızca anketin sahibi danışman ve yönetici görür (başka danışman 403, hedefte
  olmayan öğrenci 404). İstatistik ekranı anonimdir: kimlik ve serbest metin içermez.
- Yanıt almış anket silinemez (409). Aynı öğrenci aynı ankete iki kez yanıt veremez (409; veritabanında
  `(SurveyId, StudentNo)` tekil indeksi eşzamanlı çift gönderimi de engeller).
- Migration `Faz8SurveyScoring`: soru ağırlığı/zorunluluk, seçenek puanı ve toplam puan `double`,
  tekil indeksler.
- Testler: toplam 135 (bu aşamada +63): puanlama ve CSV birim testleri, API testleri (oluşturma doğrulamaları,
  başka danışmanın erişimi, hedef kitle, kapalı/planlanmış anket, KVKK rızası, çift gönderim, geçersiz
  cevaplar, sıralama ve kırılım, yeniden puanlama, destek ataması, dışa aktarma, anonim istatistik, içe aktarma
  hata raporu, silme kuralı).
- Canlı doğrulama (gerçek tarayıcı, gerçek SQLite + migration): danışman anketi oluşturur → iki öğrenci
  (390 px mobil görünümde) rıza vererek yanıtlar → sıralama ve kırılım → ilk 1 öğrenciye "Yemek kartı"
  atanır → CSV iner (BOM, 3 satır) → puanlama değişince 16 yerine 56 görünür → şablon indirilir, CSV içe
  aktarılır (1 satır alındı, 2 hata raporlandı) → istatistik → öğrenci `/anketler` yönetim sayfasına
  giremez. Konsol hatası yok.

### Aşama 8'de bulunup düzeltilenler

1. `useAsync` yenilemede içeriği "Yükleniyor"a çevirip sayfa durumunu (başarı mesajı, kaydırma) siliyordu.
   Artık yalnızca girdiler değişince yükleniyor gösterilir; `retry` ile yenilemede mevcut veri kalır
   (Aşama 7 karar panelindeki sıçrama da bununla düzeldi).
2. Rıza ifadesi ayrı bir uç nokta yerine form yanıtında gelir; metin ön yüzde kopyalanmaz.

## Aşama 9 — Erken uyarı, mezuniyet analizi ve danışman paneli grafikleri

- Durum: tamamlandı
- **Risk puanı (kurala dayalı, 0-100):** göstergeler ayrı ve açıklamalıdır; toplam puan 100 ile sınırlanır,
  seviye Düşük (<30) / Orta (30-59) / Yüksek (>=60). Göstergeler: düşük GANO (<2.00: 35) ve sınırda GANO
  (<2.30: 15); son iki dönem ortalaması arasında düşüş (>=0.5: 20, >=1.0: 30); aynı dersten tekrar tekrar
  kalma (ders başına 15, en çok 30) ve art arda iki dönem ders kalma (10); mevcut dönem devamsızlığı
  (%20 üstü: 20, %30 ve üstü: 40); normal süreyi (8 dönem) aşma (10'dan başlar, dönem başına +5) ve azami
  süreye (14 dönem) yaklaşma (40). Tüm eşikler `appsettings.json > RiskRules` ile değişir.
- **Mezuniyet analizi:** zorunlu dersler (tamamlanmayanlar Kaldı / Alınmadı / Bu dönem alıyor olarak ayrılır),
  seçmeli kredi, toplam AKTS ilerlemesi (%), staj günü (OBİS'ten), asgari GANO, eksik koşul listesi ve tahmini
  kalan dönem. **Projeksiyon:** "bu dönem aldığı dersleri geçerse mezuniyet koşulları tamamlanır mı?"
  (`ProjectedCanGraduate`) ve geçse bile kalacak eksikler.
- **Danışman paneli (Recharts):** öğrenci sayısı, ortalama GANO, yüksek riskli sayısı, mezuniyete en çok 1
  dönem kalan sayısı; risk dağılımı (halka), dönem bazlı ortalama GANO (çizgi), başarı oranı en düşük 12 ders
  (çubuk, yüzde etiketli) ve "önce bakılması gerekenler". Grafikler CSS değişkenleriyle renklendiği için koyu
  temada da çalışır; Recharts tembel yüklenir (yalnızca danışman paneli indirir).
- **Sayfalar:** `/risk` (puana göre sıralı liste, seviye ve ad/numara süzgeci, göstergeler özet), `/risk/:no`
  (puan çubuğu, gerekçeli göstergeler + mezuniyet analizi), `/mezuniyet` (öğrencinin kendi analizi).
  Risk yalnızca danışman/yönetici içindir; öğrenci yalnızca kendi mezuniyet analizini görebilir.
- **API:** `GET /api/analytics/dashboard`, `/risk`, `/students/{no}/risk`, `/students/{no}/graduation`
  (danışman yalnızca kendi öğrencileri; kapsam sunucuda belirlenir).
- **Mock OBİS:** `GET /api/students/{no}/internship` (tamamlanan staj günü) eklendi; `IObisClient.GetInternshipAsync`.
- Testler: toplam 199 (bu aşamada +64): her risk göstergesi için sınır ve ayar testleri, mezuniyet koşulları ve
  projeksiyon, seed uç durumları (EDGE-04 yüksek risk, EDGE-07 sert düşüş, EDGE-08 süre aşımı, EDGE-09 az kalan,
  EDGE-10 devamsızlık), panel tutarlılığı, API yetkileri (öğrenci risk göremez, başka danışman 403, 404 mesajı).
- Canlı doğrulama (gerçek tarayıcı): panel + 3 grafik, risk listesi ve seviye süzgeci, risk detayı, EDGE-09 mezuniyet
  projeksiyonu, öğrenci kendi mezuniyet analizi, öğrencinin `/risk`'e girememesi, 390 px mobil (yatay taşma yok).
  Konsol hatası yok.

### Aşama 9'da bulunup düzeltilen seed sorunları

1. **Müfredat sayıları tutarsızdı:** `TotalEctsRequirement=240` iken zorunlu dersler 192 AKTS, tüm seçmeliler 50
   AKTS (en çok 242) idi; mezuniyet pratikte erişilemezdi. Müfredat artık 212 AKTS (192 + dönem başına bir seçmeli
   x 4) ve 12 seçmeli kredi (4 x 3).
2. **EDGE-09 "az kalan" değildi:** önceki dönemlerden kalınmış 3 ders tekrar alınmadığı için eksik çok görünüyordu.
   Bu öğrencinin kalınmış dersleri geçilmiş sayıldı; artık yalnızca bu dönemin dersleri kalıyor.
3. **EDGE-04 yeterince riskli değildi:** GANO 2.24 idi. Artık ilk iki dönemde ders kalmış (GANO < 2.0), art arda iki
   dönem başarısız ve bir derste devamsızlık sınırı aşılmış (demoda tek "Yüksek" riskli öğrenci).
4. **Staj verisi yoktu:** OBİS'e staj endpoint'i ve seed verisi eklendi (rastgele akışın sonunda üretildi; mevcut hiçbir
   veri değişmedi, yalnızca yukarıdaki iki öğrencinin ve müfredatın değişikliği var).
