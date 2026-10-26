# Öğrenci–Danışman Otomasyonu

Akademik danışmanların işini kolaylaştıran bir web uygulaması. Üniversitenin öğrenci bilgi sistemi (OBİS)
verilerini (şimdilik bir **mock OBİS API** üzerinden) çeker; danışmana **ders kaydı doğrulama**, **ihtiyaç
puanlama (anket)**, **erken uyarı ve mezuniyet analizi** ve **iletişim/talep** araçlarıyla karar desteği sağlar.
Öğrenciler kendi ders kaydını, mezuniyet durumunu, randevu, duyuru ve taleplerini görür.

> Tüm veriler kurgusaldır; gerçek kişisel veri içermez. Bitirme projesi olarak geliştirilmiştir.

![Danışman paneli](docs/screenshots/02-danisman-panel.png)

## İçindekiler

1. [Hızlı başlangıç](#hızlı-başlangıç)
2. [Demo hesaplar](#demo-hesaplar)
3. [Modüller ve ekranlar](#modüller-ve-ekranlar)
4. [Seed uç durumları](#seed-uç-durumları)
5. [Mimari özeti](#mimari-özeti)
6. [Elle çalıştırma](#elle-çalıştırma)
7. [Testler](#testler)
8. [Yapılandırma](#yapılandırma)
9. [Üretime alma notları](#üretime-alma-notları)
10. [Proje yapısı](#proje-yapısı)
11. [Bilinen sınırlamalar](#bilinen-sınırlamalar)

## Hızlı başlangıç

**Gereksinimler:** .NET SDK 8 ya da üstü, Node.js 20 ya da üstü (npm ile).

Tek komutla Mock OBİS + backend API + frontend'i başlatır (ilk çalıştırmada `npm install` ve derleme de yapılır):

```powershell
# Windows (PowerShell)
.\start-all.ps1
# durdurmak için
.\stop-all.ps1
```

```bash
# Linux / macOS / Git Bash
./start-all.sh
./stop-all.sh
```

Hazır olunca:

| Ne | Adres |
| --- | --- |
| Uygulama (ön yüz) | http://localhost:5173 |
| Backend API + Swagger | http://localhost:5154/swagger |
| Mock OBİS + Swagger | http://localhost:5280/swagger |

Günlükler `.run/` klasörüne yazılır. Veritabanı (SQLite) `backend/src/Advisor.Api/advisor.db` dosyasında oluşur;
silinirse bir sonraki açılışta yeniden kurulur ve demo hesaplar tekrar eklenir. Yüklenen talep ekleri
`backend/src/Advisor.Api/App_Data/uploads/` altında tutulur.

## Demo hesaplar

Tüm hesapların şifresi: **`Demo123!`** (yalnızca geliştirme amaçlı).

| Kullanıcı adı | Rol | Bağlı kayıt |
| --- | --- | --- |
| `admin` | Yönetici | Tüm öğrencileri ve tüm danışmanların anketlerini görür |
| `danisman1` | Danışman | ADV1 — Dr. Öğr. Üyesi Elif Yıldız (21 öğrenci; tüm uç durum öğrencileri bu danışmanda) |
| `danisman2` | Danışman | ADV2 — Doç. Dr. Mehmet Arslan |
| `danisman3` | Danışman | ADV3 — Dr. Öğr. Üyesi Zeynep Kara |
| `bolumbaskani1` | Bölüm Başkanı | Bilgisayar Mühendisliği — kendi bölümünün öğrencilerini danışmanlara atar, her danışmanın analizlerini görür |
| `dekan1` | Dekan | Tüm bölümler — atama yapar, tüm danışmanların analizlerini görür |
| `ogrenci1` | Öğrenci | 20261001 Can Şahin (1. sınıf, henüz notlandırılmış dersi yok) |
| `ogrenci2` | Öğrenci | 20241011 Buse Ateş (3. sınıf — **saat çakışması** uç durumu) |
| `ogrenci3` | Öğrenci | 20251006 Selin Kılıç (2. sınıf — **önkoşul ihlali** uç durumu) |
| `ogrenci4` | Öğrenci | 20231016 Caner Polat (4. sınıf — **sınav çakışması** uç durumu) |

Giriş ekranında üç hesap için tek tıkla doldurma düğmeleri vardır. Mock OBİS'teki diğer öğrenciler (63 kişi)
danışman ve yönetici ekranlarında görünür; hepsi için hesap açılmamıştır.

## Modüller ve ekranlar

### 1. Ders kaydı doğrulama motoru (projenin çekirdeği)

Danışman, onaylamadan önce öğrencinin ders seçimini 6 kurala karşı otomatik kontrol ettirir. Kurallar `IRegistrationRule`
arayüzüyle yazılır; **yeni kural eklemek yalnızca yeni bir sınıf yazmaktır** (DI'a otomatik kaydolur).

| Kural | Seviye | Ne yakalar |
| --- | --- | --- |
| Ders saati çakışması | Hata | Seçilen şubelerin aynı gün ve kesişen saatte olması |
| Önkoşul | Hata | Önkoşulu geçilmemiş ders (aynı dönemde birlikte alınan geçerli değildir) |
| Alttan ders önceliği | Uyarı | Kalınmış/hiç alınmamış alt dönem dersi varken üst dönem dersi seçmek |
| AKTS limiti | Hata | GANO'ya bağlı üst sınır (<2.00: 24, 2.00-2.99: 31, ≥3.00: 37; ayarlanabilir) |
| Sınav çakışması | Hata | Aynı tarih ve saatte iki sınav |
| Tekrar alma | Bilgi | Daha önce BB ve üstüyle geçilmiş dersi yeniden seçmek |

Danışman ekranı: dönem seçimi → öğrenci listesi (hata/uyarı/bilgi rozetleri) → öğrenci detayında haftalık program
(çakışanlar **kırmızı**), kural bulguları ve **Onayla / Düzeltme iste (not zorunlu)** kararı. Karar ana uygulamanın kendi
veritabanında geçmişiyle birlikte tutulur. Öğrenci kendi kaydını ve danışman kararını `Kayıt Durumum`dan görür.

| Danışman: öğrenci listesi | Danışman: doğrulama sonucu |
| --- | --- |
| ![Liste](docs/screenshots/03-ders-kayitlari-liste.png) | ![Doğrulama](docs/screenshots/04-ders-kaydi-dogrulama.png) |

### 2. Anket ve ihtiyaç puanlama

Danışman anket oluşturur (tek seçim, çoklu seçim, sayı, metin; tarih aralığı; hedef sınıf). Öğrenci **KVKK aydınlatma
metnini okuyup açık rıza vermeden** anketi gönderemez. Dış kaynaklı sonuçlar **CSV ile içe aktarılabilir** (öğrenci
numarasına göre eşleşir, hatalı satırlar raporlanır). Puan = (seçilen seçeneklerin puanı ya da girilen sayı) × soru ağırlığı;
ağırlık/puanlar sonradan da değiştirilebilir ve mevcut yanıtlar anında yeniden puanlanır. Sonuçlar puana göre sıralanır, her
puanın **hangi cevaptan geldiği** gösterilir; "ilk N öğrenci" ya da "puanı X ve üstü" seçilip **destek türü** (yemek kartı, burs…)
atanır ve **CSV olarak dışa aktarılır**. Yanıtları yalnızca anketin sahibi danışman ve yönetici görür; istatistik ekranı kimlik
içermez.

| Anket oluşturma | Sonuçlar, kırılım ve destek ataması | Öğrenci formu (KVKK) |
| --- | --- | --- |
| ![Oluşturma](docs/screenshots/05-anket-olusturma.png) | ![Sonuç](docs/screenshots/06-anket-sonuc-siralama.png) | ![Form](docs/screenshots/15-ogrenci-anket-formu-kvkk.png) |

### 3. Erken uyarı ve mezuniyet analizi

* **Risk puanı (0-100, kurala dayalı):** düşük/sınırda GANO, son iki dönem ortalamasında düşüş, aynı dersten tekrar kalma ve
  art arda dönemlerde kalma, devamsızlık sınırına yaklaşma/aşma, normal ve azami öğrenim süresi. Her gösterge ayrı ve
  **açıklamalıdır**; eşikler `appsettings.json`'dan değişir.
* **Mezuniyet analizi:** eksik zorunlu dersler (Kaldı / Alınmadı / Bu dönem alıyor), seçmeli kredi, toplam AKTS ilerlemesi, staj
  günü, GANO ve **"bu dönem aldığı dersleri geçerse mezun olur mu?"** projeksiyonu. Öğrenci kendi analizini görür.
* **Danışman paneli (Recharts):** öğrenci sayısı, ortalama GANO, risk dağılımı, dönem bazlı GANO eğilimi, başarı oranı en düşük dersler.

| Risk listesi | Risk gerekçesi | Mezuniyet analizi |
| --- | --- | --- |
| ![Risk](docs/screenshots/07-risk-listesi.png) | ![Detay](docs/screenshots/08-risk-detay.png) | ![Mezuniyet](docs/screenshots/09-mezuniyet-analizi.png) |

### 4. İletişim ve talepler

* **Randevu:** danışman haftalık görüşme saatlerini tanımlar; öğrenci kendi danışmanının boş saatinden randevu alır (en az 1 saat önceden,
  28 gün ufuk, en fazla 2 aktif randevu); iki taraf da iptal edebilir, danışman iptalinde öğrenciye gerekçeli bildirim gider.
* **Görüşme notları ve öğrenci dosyası:** notlar ve randevu/talep/duyuru geçmişi tek zaman çizelgesinde; **notlar öğrenciye kapalıdır**.
* **Filtreli duyuru:** sınıf, GANO aralığı, risk seviyesi, staj durumuyla hedef kitle; göndermeden önce kimlere gideceği görülür;
  öğrenci panelinde bildirim kutusu ve okundu durumu, danışmanda "kaç kişi okudu".
* **Talep/dilekçe:** ders ekle-bırak, kayıt dondurma, muafiyet, diğer; açıklama + dosya eki (PDF/PNG/JPG/DOCX, 5 MB); durum akışı
  Beklemede → İnceleniyor → Onaylandı/Reddedildi (reddederken gerekçe zorunlu); her değişiklik öğrenciye bildirim gönderir.

| Randevular (danışman) | Öğrenci dosyası | Duyuru hedef kitlesi | Talepler (danışman) |
| --- | --- | --- | --- |
| ![Randevu](docs/screenshots/10-randevular-danisman.png) | ![Dosya](docs/screenshots/11-ogrenci-dosyasi.png) | ![Duyuru](docs/screenshots/12-duyuru-hedef-kitle.png) | ![Talep](docs/screenshots/17-talepler-danisman.png) |

### Öğrenci ekranları ve mobil

| Ders kaydım | Mezuniyet analizim | Randevu | Gelen kutusu | Talepler |
| --- | --- | --- | --- | --- |
| ![](docs/screenshots/14-ogrenci-ders-kaydi.png) | ![](docs/screenshots/13-ogrenci-mezuniyet.png) | ![](docs/screenshots/16-ogrenci-randevu.png) | ![](docs/screenshots/18-ogrenci-gelen-kutusu.png) | ![](docs/screenshots/19-ogrenci-talepler.png) |

Arayüz masaüstü öncelikli ama mobilde bozulmaz (menü çekmeceye dönüşür, tablolar yatay kayar):

| Mobil sayfa | Mobil menü |
| --- | --- |
| ![](docs/screenshots/20-mobil-ogrenci-ders-kaydi.png) | ![](docs/screenshots/21-mobil-menu.png) |

## Seed uç durumları

Mock OBİS'in deterministik seed verisi (63 öğrenci, 118 derslik gerçek 8 dönemlik Bilgisayar Mühendisliği müfredatı) 10 bilinçli uç durum içerir; her biri ilgili modülde
doğru yakalanır ve testlerle doğrulanmıştır. Hepsi `danisman1`'in öğrencisidir.

| Uç durum | Öğrenci | Nerede görünür |
| --- | --- | --- |
| EDGE-01 Saat çakışması | 20241011 Buse Ateş | Ders Kayıtları → detay (Matematik-1/C ile Mikroişlemciler kırmızı) |
| EDGE-02 Önkoşul ihlali | 20251006 Selin Kılıç | Ders Kayıtları (Programlama Dili-1 kalmış, Veri Yapıları seçilmiş) |
| EDGE-03 Alttan ders önceliği | 20241012 Duygu Şahin | Ders Kayıtları (Fizik-2 kalmış) |
| EDGE-04 AKTS limiti + yüksek risk | 20251007 Sude Kaya | Ders Kayıtları (44 AKTS) ve Risk (puan 85: düşük GANO, art arda kalma, devamsızlık) |
| EDGE-05 Sınav çakışması | 20231016 Caner Polat | Ders Kayıtları (Bilgisayar Mimarisi ve Mantık Devreleri-1 finalleri) |
| EDGE-06 Tekrar alma | 20231017 Kaan Aksoy | Ders Kayıtları (Veri Yapıları, AA ile geçilmiş) |
| EDGE-07 GANO düşüşü | 20241013 Volkan Kurt | Risk (dönem ortalaması AA → DD) |
| EDGE-08 Azami süreye yaklaşma | 20231018 Yusuf Karaca | Risk (9. dönem) |
| EDGE-09 Mezuniyete az kalan | 20231019 Oğuz Erdoğan | Mezuniyet analizi (bu dönemi geçerse mezun) |
| EDGE-10 Devamsızlık sınırı | 20251008 Hakan Yılmaz | Risk (%29 devamsızlık) |

Seed'i yeniden üretmek için (çıktı birebir aynıdır): `cd backend/src/Obis.MockApi && dotnet run -- --generate-seed`.

## Mimari özeti

Ayrıntı, Mermaid diyagramları ve gerekçeler için: **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

* **Katmanlı backend (.NET):** `Domain` ← `Application` ← `Infrastructure` ← `Api`. Bağımlılıklar içe doğrudur.
* **Adapter deseni:** ana uygulama OBİS'e yalnızca `IObisClient` arayüzüyle bağlanır. Gerçek OBİS geldiğinde yapılacak iş yeni bir
  implementasyon yazıp DI kaydını değiştirmektir. OBİS verisi ana veritabanına **kopyalanmaz**; her zaman canlı (kısa önbellekli) çekilir.
* **Kural motoru:** saf ve bağlam tabanlı `IRegistrationRule` kuralları; ağsız birim testlenebilir.
* **Güvenlik:** JWT, varsayılan-reddet yetkilendirme, rol + sahiplik kontrolü (`IStudentAccessGuard`), giriş hız sınırı.
* **Frontend:** React 19 + TypeScript + Vite; saf CSS (koyu tema destekli); Recharts yalnızca danışman panelinde, tembel yüklenir.

## Elle çalıştırma

Betik kullanmak istemezseniz üç ayrı terminalde:

```bash
# 1) Mock OBİS  (http://localhost:5280)
cd backend/src/Obis.MockApi
ASPNETCORE_URLS=http://localhost:5280 dotnet run

# 2) Backend API (http://localhost:5154, Obis:BaseUrl varsayılanı 5280)
cd backend/src/Advisor.Api
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5154 dotnet run

# 3) Frontend (http://localhost:5173, /api isteklerini 5154'e yönlendirir)
cd frontend
npm install
npm run dev
```

(PowerShell'de ortam değişkenleri için `$env:ASPNETCORE_URLS = 'http://localhost:5280'` biçimini kullanın.)

## Testler

```bash
cd backend && dotnet test          # 333 test: birim, API, tam yığın uçtan uca, güvenlik taraması
cd frontend && npm run lint && npm run build   # oxlint (0 uyarı), tsc, vite build
```

Backend testleri şunları kapsar: her doğrulama kuralının sınır durumları; 10 seed uç durumunun hem hizmet hem **API üzerinden**
yakalanması; **gerçek mock OBİS HTTP sözleşmesiyle** bellek içi uçtan uca testler; her endpoint'in otomatik yetki taraması (anonim → 401,
rol dışı → 403, onaylı istisna listesi); Swagger üretimi; üretim sertleştirmeleri (zayıf JWT anahtarı, demo hesap kapısı, hız sınırı).

> **Not:** Test projesi `net10.0` hedefler, çünkü geliştirme makinesinde yalnızca .NET 10 çalışma zamanı vardı (bkz.
> [DECISIONS.md](docs/DECISIONS.md)). Yalnızca .NET 8 SDK'nız varsa `tests/Advisor.Tests/Advisor.Tests.csproj` içinde
> `TargetFramework` ve `Microsoft.AspNetCore.Mvc.Testing` sürümünü `8.0.*` yapın.

## Yapılandırma

Ana ayarlar `backend/src/Advisor.Api/appsettings.json` içindedir; ortam değişkeniyle ezilebilir (`Bolum__Anahtar` biçimi).

| Anahtar | Varsayılan | Anlamı |
| --- | --- | --- |
| `ConnectionStrings:Default` | `Data Source=advisor.db` | SQLite dosyası |
| `Obis:BaseUrl` / `Obis:CacheSeconds` | `http://localhost:5280` / `30` | OBİS adresi ve kısa önbellek süresi |
| `Jwt:SigningKey` / `Jwt:AccessTokenMinutes` | geliştirme anahtarı / `120` | Token imzası ve süresi |
| `RegistrationRules:*` | bkz. dosya | GANO bantları, AKTS sınırları, tekrar alma not eşiği |
| `RiskRules:*` | bkz. dosya | Risk göstergeleri, puanlar, seviye sınırları, mezuniyet asgari GANO'su |
| `Uploads:Path` | `App_Data/uploads` | Talep eklerinin klasörü |
| `Auth:LoginAttemptsPerMinute` | `10` | İstemci IP'si başına giriş denemesi sınırı |
| `Demo:SeedAccounts` | Development'ta açık, Production'da kapalı | Demo hesapların oluşturulması |

## Üretime alma notları

* `Jwt__SigningKey` ortam değişkeniyle **en az 32 karakterlik gizli** bir değer verin; geliştirme anahtarıyla uygulama üretimde başlamaz.
* Demo hesaplar üretimde oluşmaz (`Demo:SeedAccounts=true` ile açıkça istenmedikçe). Yönetici hesabını ayrıca oluşturun.
* Uygulama HTTPS arkasında çalıştırılmalı; ters proxy kullanılıyorsa `UseForwardedHeaders` eklenmeli (hız sınırı istemci IP'sine göre çalışır).
* Frontend için `npm run build` çıktısı (`frontend/dist`) API ile **aynı alan adından** (ters proxy) sunulmalıdır; API CORS izni vermez.
* `Uploads:Path` mutlak, yedeklenen ve web kökünün dışındaki bir klasör olmalı.
* Gerçek OBİS'e geçiş: [docs/ARCHITECTURE.md → Gerçek OBİS'e geçiş](docs/ARCHITECTURE.md#gerçek-obise-geçiş).

## Proje yapısı

```text
OgrenciDanismanOtomasyonu/
├─ backend/
│  ├─ OgrenciDanismanOtomasyonu.slnx
│  ├─ src/
│  │  ├─ Advisor.Domain/           Entity'ler, enum'lar (bağımlılığı yok)
│  │  ├─ Advisor.Application/      Arayüzler, DTO'lar, saf iş mantığı (kural motoru, puanlama, risk, mezuniyet, CSV)
│  │  ├─ Advisor.Infrastructure/   EF Core + SQLite, HttpObisClient, servisler (anket, randevu, duyuru, talep...)
│  │  ├─ Advisor.Api/              Controller'lar, JWT, Swagger, middleware
│  │  └─ Obis.MockApi/             Bağımsız mock OBİS (Minimal API + JSON seed)
│  └─ tests/Advisor.Tests/         xUnit
├─ frontend/                       React + TypeScript + Vite
├─ docs/
│  ├─ ARCHITECTURE.md              Katmanlar, diyagramlar, gerekçeler
│  ├─ DECISIONS.md                 Belirsiz noktalarda alınan kararlar
│  ├─ PROGRESS.md                  Aşama aşama ilerleme + final özet
│  └─ screenshots/                 Bu belgedeki ekran görüntüleri
├─ start-all.ps1 / start-all.sh    Tek komutla başlatma
└─ stop-all.ps1  / stop-all.sh     Durdurma
```

## Bilinen sınırlamalar

* Excel (.xlsx) içe aktarma yok, yalnızca CSV ("CSV olarak kaydet"); ek paket eklememek için (bkz. DECISIONS).
* Token iptali (logout sonrası geçersiz kılma) ve refresh token yok; token 120 dk geçerlidir.
* Yüklenen dosyalara virüs taraması yapılmaz (yalnızca tür, imza ve boyut denetimi).
* KVKK metni örnek/kurgusaldır; gerçek kullanımda hukuk birimi onaylamalıdır.
* Mock OBİS'teki baseline öğrencilerin bir kısmı kaldıkları dersleri tekrar almadan ilerler; bu yüzden uç durum olmayan bazı
  öğrencilerde de (doğru) "alttan ders" uyarısı çıkar. Seed üretiminin gerçekçilik sınırıdır, kural motorunun hatası değildir.
* Bildirimler uygulama içidir (e-posta/SMS yok).
