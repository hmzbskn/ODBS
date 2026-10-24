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

## Aşama 10 — İletişim ve talepler

- Durum: tamamlandı
- **Randevu:** danışman haftalık görüşme saatleri tanımlar (hafta içi, 08:00-20:00, 10-120 dk, çakışma yok);
  öğrenci yalnızca KENDİ danışmanının saatlerinden önümüzdeki 28 gün için randevu alır (en az 60 dk önceden,
  en fazla 2 aktif randevu). Dolu saat tekrar alınamaz: kod kontrolüne ek olarak veritabanında kısmi tekil indeks
  (`SlotId, Date` — yalnızca planlı randevular) eşzamanlı çift rezervasyonu engeller. İki taraf da iptal edebilir
  (başlamış/geçmiş randevu iptal edilemez); danışman iptalinde öğrenciye gerekçeli bildirim gider. Danışman,
  gerçekleşmiş randevuyu "tamamlandı" işaretler. Kullanılan saat kuralı Türkiye saatidir (`TrTime`).
- **Görüşme notları ve öğrenci dosyası:** danışman her öğrenci için not ekler/siler; zaman çizelgesi notları,
  randevuları, talepleri ve alınan duyuruları birleştirip yeniden eskiye sıralar. Notlar öğrenciye KAPALIDIR
  (öğrencinin erişebildiği hiçbir uçta görünmez, testle doğrulandı). Yönetici okur ama not ekleyemez.
- **Filtreli duyuru:** sınıf, GANO aralığı, risk seviyesi ve staj durumuyla hedef kitle; gönderimden önce
  "hedef kitleyi göster" (sayı + öğrenci listesi) zorunludur, filtre değişince önizleme geçersiz olur. Her öğrenci için
  ayrı alıcı kaydı tutulur; danışman kimin okuyup okumadığını görür. Boş kitleye gönderim reddedilir.
- **Gelen kutusu (öğrenci):** duyurular ve sistem bildirimleri (randevu iptali, talep durumu) tek listede, okundu
  durumuyla; menüde okunmamış sayacı; "tümünü okundu say". Başkasının duyurusu/bildirimi okundu yapılamaz (404).
- **Talep/dilekçe:** tür (ders ekle-bırak, kayıt dondurma, muafiyet, diğer), açıklama (10-2000), isteğe bağlı ek.
  Durum akışı Beklemede → İnceleniyor → Onaylandı/Reddedildi; sonuçlanan talep değişmez (409); reddederken gerekçe
  zorunlu; her durum değişikliği öğrenciye bildirim gönderir. En fazla 5 açık talep.
- **Ek dosya güvenliği:** izinli uzantılar (pdf, png, jpg, jpeg, docx), en fazla 5 MB, dosya imzası (sihirli bayt)
  uzantıyla uyuşmalı ("belge.pdf" adlı çalıştırılabilir reddedilir), özgün ad yol/denetim karakterlerinden
  arındırılır, diskte rastgele adla saklanır (yol gezintisi mümkün değil), indirme her zaman `attachment` +
  `nosniff`, erişim yalnızca sahibi/danışmanı/yönetici.
- **Arayüz:** `/randevular` (danışman: saat yönetimi + yaklaşan/geçmiş randevular; öğrenci: boş saatler + randevularım),
  `/ogrenci/:no` (not + zaman çizelgesi), `/duyurular` (danışman: oluşturma + okundu durumu; öğrenci: gelen kutusu),
  `/talepler` (öğrenci: form + takip; danışman: inceleme ve karar). Artık hiçbir modül "Yakında" değildir; yer tutucu
  mekanizması kaldırıldı.
- Migration'lar: `Faz10Appointments` (kısmi tekil indeks), `Faz10Communications` (AnnouncementRecipient, filtre ve ek alanları).
- Testler: toplam 299 (bu aşamada +100): randevu kuralları ve yarış koşulu koruması, not gizliliği, süzgeç ve okundu
  durumu, ek doğrulayıcı (imza, uzantı, boyut, ad temizleme), durum akışı ve yetki matrisi.
- Canlı doğrulama (gerçek tarayıcı + SQLite + disk deposu): duyuru önizleme zorunluluğu, öğrencide menü sayacı ve okundu
  olunca sıfırlanması, `.exe` reddi (Türkçe hata), PDF ekli talep, danışmanın eki indirmesi (içerik birebir),
  not olmadan "Reddet" kapalı, İncelemeye al → Onayla, öğrencide yanıt notu ve 2 bildirim, danışmanda "1 / 5 okudu",
  öğrencinin personel sayfalarına girememesi, mobil.

## Aşama 11 — Uçtan uca doğrulama ve sertleştirme

- Durum: tamamlandı
- **Tam yığın testi (bellek içi, ağsız):** Mock OBİS gerçek Minimal API + JSON seed ile `WebApplicationFactory` içinde ayağa
  kalkar, ana uygulama GERÇEK `HttpObisClient` ile ona bağlanır (sahte istemci yok). Böylece HTTP sözleşmesi (metin enum'lar,
  saat/tarih biçimleri) otomatik doğrulanır. Doğrulananlar: OBİS adaptörünün her endpoint'i; 10 uç durumdan ders kaydı ve
  risk/mezuniyet ile ilgili olanların API üzerinden yakalanması (saat/sınav çakışması, önkoşul, alttan ders, AKTS limiti, tekrar
  alma, GANO düşüşü, süre aşımı, devamsızlık, yüksek risk, mezuniyete az kalan); **63 öğrencinin tamamı için** ders kaydı, risk,
  mezuniyet ve öğrenci dosyası uçlarının 200 dönmesi; tüm demo öğrenci hesaplarının kendi ekranlarını kullanabilmesi.
- **Otomatik yetki taraması:** Uygulamadaki her endpoint (49) çalışma zamanında listelenir ve taranır: (1) anonim erişime açık
  olan yalnızca `POST api/auth/login`; (2) diğer tüm endpoint'ler anonim istekte 401 verir; (3) rol kısıtlı her endpoint (40),
  izin verilmeyen her rolden 403 alır; (4) rol kısıtı OLMAYAN endpoint'ler bilinçli onaylanmış bir listeyle birebir eşleşmek
  zorundadır (yetki servis/guard katmanında). Yeni bir endpoint eklenip yetki düşünülmezse test kırılır.
- **Swagger testi:** `/swagger/v1/swagger.json` Development'ta üretilir ve tüm controller'ları belgeler (44 yol); Production'da açık değildir.
- **Sertleştirme:**
  - Üretimde zayıf ya da geliştirme `Jwt:SigningKey` ile uygulama BAŞLAMAZ (anahtar repoda göründüğü için token sahteciliği mümkün olurdu).
  - Demo hesaplar (bilinen şifreli) yalnızca Development/Testing'de kendiliğinden oluşur; üretimde `Demo:SeedAccounts=true` ile
    açıkça istenmelidir.
  - Giriş uç noktasında istemci IP'si başına dakikada 10 deneme sınırı (`Auth:LoginAttemptsPerMinute`); aşılınca 429 + Türkçe
    mesaj, doğru şifre de sınırı aşamaz.
  - Güvenlik başlıkları (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`), üretimde HSTS.
  - İmzası bozulmuş ya da rolü değiştirilmiş (içerik değiştirilmiş) JWT'nin reddedildiği testle doğrulandı.
- Testler: toplam 333 (bu aşamada +34).

**Bilinen sınırlamalar (rapora not):**
- Giriş hız sınırı IP'ye göre; ters proxy arkasında `X-Forwarded-For` işlenmeli (`UseForwardedHeaders`) yoksa tüm istemciler tek IP görünür.
- Token'ı geri çağırma (logout sonrası geçersiz kılma) ve refresh token yoktur; token süresi 120 dk.
- Yüklenen dosyalar virüs taramasından geçmez; yalnızca tür/imza/boyut denetimi vardır.
- Frontend üretim derlemesi (`frontend/dist`) API tarafından sunulmaz; ters proxy ya da statik barındırma gerekir.

## Aşama 12 — Dokümantasyon ve tek komutla çalıştırma

- Durum: tamamlandı
- **Tek komutla çalıştırma:** `start-all.ps1` (Windows) ve `start-all.sh` (Linux/macOS/Git Bash): gereksinimleri kontrol eder, ilk çalıştırmada
  `npm install` yapar, backend'i derler, Mock OBİS + API + frontend'i arka planda başlatır, hazır olana kadar bekler, adresleri ve demo
  hesapları yazdırır. `stop-all.*` süreçleri alt süreçleriyle durdurur. Her iki betik de gerçekten çalıştırılarak doğrulandı (soğuk başlangıç
  yaklaşık 5 sn, giriş isteği Vite proxy üzerinden 200; durdurma sonrası dinleyen port yok). Test sırasında yakalanan hata: PowerShell'de
  `$Args` otomatik değişken olduğu için parametre adı olarak kullanılamaz (`$ArgList` yapıldı).
- **README.md:** hızlı başlangıç, demo hesaplar, modül ve ekran açıklamaları (21 gerçek ekran görüntüsü), uç durum → ekran haritası, elle
  çalıştırma, testler, yapılandırma anahtarları, üretime alma notları, proje yapısı, bilinen sınırlamalar.
- **docs/ARCHITECTURE.md:** 9 Mermaid diyagramı (sistem, katmanlar, adapter, kural motoru sınıf + sıra diyagramı, yetkilendirme akışı,
  ER, test stratejisi, dağıtım), modül ve veri akışı tablosu, güvenlik modeli, tasarım gerekçeleri ve **"Gerçek OBİS'e geçiş"** kontrol listesi.
  Diyagramların söz dizimi gerçek bir Mermaid motoruyla doğrulandı; bu doğrulama iki bozuk diyagramı yakaladı (sıralı diyagramda `;` ifade
  ayırıcıdır, ER diyagramında ASCII dışı etiketler tırnaklanmalıdır) ve düzeltildi. Artık 9/9 diyagram geçerli ve çiziliyor.
- **Ekran görüntüleri** (`docs/screenshots/`, 1.7 MB): başlat betiğiyle ayağa kaldırılan gerçek uygulamadan, API ile üretilen demo veriyle
  (anket + CSV içe aktarma, randevu, not, duyuru, talep) tarayıcıdan alındı.

---

# Final özet

## Neler tamamlandı

12 aşamanın tamamı bitti; her aşama derlenip test edilerek ayrı commit'lendi.

* **Altyapı:** katmanlı .NET 8 backend (Domain / Application / Infrastructure / Api), bağımsız Mock OBİS API'si (63 öğrenci, 52 derslik müfredat,
  10 bilinçli uç durum, deterministik seed), `IObisClient` adapter'ı, SQLite + EF Core migration'ları, JWT kimlik doğrulama, 3 rol, demo hesaplar,
  global exception middleware (Türkçe hata mesajları), Swagger.
* **Modül 1 — Ders kaydı doğrulama:** 6 kurallı, genişletilebilir motor (saat çakışması, önkoşul, alttan ders önceliği, AKTS/GANO limiti, sınav
  çakışması, tekrar alma); danışman ekranı (liste, haftalık program, çakışanlar kırmızı, onayla / düzeltme iste); karar geçmişi.
* **Modül 2 — Anket ve ihtiyaç puanlama:** 4 soru tipi, KVKK açık rıza zorunluluğu, canlı puanlama (soru ağırlığı × seçenek puanı), şeffaf kırılım,
  ilk N / eşik seçimi, destek ataması, CSV içe/dışa aktarma (satır hata raporu), anonim istatistik, gizlilik.
* **Modül 3 — Erken uyarı ve mezuniyet:** açıklamalı kurala dayalı risk puanı, mezuniyet analizi + "bu dönem geçerse" projeksiyonu, danışman paneli
  (Recharts: risk dağılımı, dönem GANO eğilimi, en zor dersler).
* **Modül 4 — İletişim ve talepler:** randevu (yarış koşuluna dayanıklı), görüşme notları (öğrenciye kapalı) ve zaman çizelgesi, filtreli duyuru
  (önizleme, okundu durumu), öğrenci bildirim kutusu, talep/dilekçe akışı ve güvenli dosya eki.
* **Kalite:** 333 backend testi (birim, seed uç durumları, API, gerçek OBİS HTTP sözleşmesiyle tam yığın, otomatik yetki taraması, Swagger, sertleştirme);
  frontend `tsc`, `oxlint` (0 uyarı), `vite build` temiz; her arayüz aşaması gerçek tarayıcıda (masaüstü + 390 px mobil) uçtan uca gezildi.
* **Güvenlik:** varsayılan-reddet politikası + rol + sahiplik, giriş hız sınırı, üretimde zayıf anahtar/demo hesap kapıları, dosya imzası doğrulaması,
  CSV enjeksiyonu koruması, güvenlik başlıkları.

## Süreçte bulunup düzeltilen önemli hatalar

* Mock OBİS seed'i: uç durum kodu ortak şubelerin saatini tüm öğrenciler için değiştiriyordu; `string.GetHashCode()` yüzünden seed tekrarlanabilir değildi;
  müfredat sayıları tutarsızdı (mezuniyet erişilemezdi); EDGE-04/EDGE-09 gerçekten uç durum değildi; staj verisi yoktu.
* `HttpObisClient` metin olarak gelen enum'ları çözemiyordu (yalnızca canlı HTTP testinde görülebilen hata; şimdi otomatik teste bağlı).
* `useAsync` yenilemede ekranı silip başarı mesajlarını yok ediyordu.
* Mermaid diyagramları, başlatma betiği ve çeşitli test varsayımları (sabit saat, paylaşılan fixture durumu) doğrulama sırasında yakalanıp düzeltildi.

## Bilinen eksikler ve sınırlamalar

* Excel (.xlsx) içe aktarma yok, yalnızca CSV (ek paket eklememek için).
* Token iptali ve refresh token yok; token 120 dk geçerli. Hesap yönetimi (şifre değiştirme, kullanıcı ekleme) arayüzü yok; hesaplar seed'den gelir.
* Yüklenen dosyalara virüs taraması yok; dosya deposu yerel disk.
* Bildirimler yalnızca uygulama içi (e-posta/SMS yok). KVKK metni örnek/kurgusaldır.
* Mock OBİS baseline öğrencilerinin bir kısmı kaldıkları dersi tekrar almadan ilerler: uç durum olmayan öğrencilerde de doğru "alttan ders" uyarısı çıkar.
* Aynı anda çok sayıda öğrenciyi toplu hesaplayan ekranlar (panel, risk listesi, duyuru önizleme) her öğrenci için OBİS çağrısı yapar; binlerce öğrencide
  ön hesaplama ya da toplu OBİS uç noktaları gerekir.
* Ters proxy arkasında `UseForwardedHeaders` eklenmeden giriş hız sınırı tüm istemcileri tek IP görür.

## Gerçek OBİS API'sine geçişte değişmesi gerekenler

Ayrıntılı kontrol listesi: [ARCHITECTURE.md → Gerçek OBİS'e geçiş](ARCHITECTURE.md#gerçek-obise-geçiş). Özet:

1. `IObisClient` için gerçek OBİS protokolüne/kimliğine uygun yeni bir implementasyon (alan eşlemeleriyle) ve DI kaydının değiştirilmesi;
   `Application`, `Domain`, controller'lar, servisler ve frontend **değişmez**.
2. `Obis:BaseUrl` ve kimlik bilgileri (gizli yapılandırma), önbellek süresi.
3. Staj günü, devamsızlık ve şube/saat verisinin gerçek OBİS'teki karşılıklarının netleştirilmesi (mock'ta basitleştirilmiştir).
4. Eşiklerin (GANO bantları, AKTS limitleri, azami süre, devamsızlık sınırı, müfredat sayıları) üniversite yönetmeliğine göre ayarlanması.
5. Hesapların kurumsal kimlik sağlayıcıdan (SSO/LDAP) gelmesi ve `LinkedStudentNo`/`LinkedAdvisorId` eşlemesi.
6. Ölçek için toplu uç noktalar ya da arka plan ön hesaplaması.
