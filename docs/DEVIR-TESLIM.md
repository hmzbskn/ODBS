# ODBS: Devir teslim (yapay zeka ve yeni geliştirici için)

Bu dosya, projeye sonradan giren geliştiricinin ve onun yapay zekasının ilk okuyacağı yerdir. Kod ve ayrıntı için asıl kaynaklar
`README.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/PROGRESS.md`. Burada o dosyalarda **bulunmayan bağlam** ve
**yanlış anlaşılırsa zarar verecek noktalar** var. Güncel durumu her zaman koddan ve testten doğrula; bu dosya tarih atılmış bir fotoğraftır
(2026-11-10).

## 1. Proje nedir

Bitirme projesi: **Öğrenci–Danışman Otomasyonu (ODBS)**. Selçuk Üniversitesi Bilgisayar Mühendisliği bağlamında, danışman hocanın
öğrencilerinin dönem başı ders seçimlerini, risk durumunu ve mezuniyet ilerlemesini tek ekrandan izlediği; öğrencinin de danışmanıyla
randevu, duyuru ve dilekçe işlerini yürüttüğü web uygulaması. Öğrenci ve ders verisi OBİS'ten **yalnızca okunur**. OBİS'te olmayan veri
(danışman kararı, randevu, anket, atama, duyuru, talep) ODBS'nin kendi veritabanındadır.

Repo: `https://github.com/hmzbskn/ODBS` (branch `master`). Sahibi: Hamza Başkan.

## 2. EN ÖNEMLİ UYARI: iki farklı "durum" var

- **Kodun gerçek durumu:** 21 aşama bitti, uygulama çalışıyor, 369 test yeşil (bkz. 5).
- **Hocaya söylenen durum:** hoca projenin **yapıldığını bilmiyor**. Resmi durum "planlama aşaması". Hocaya giden her belge
  (`docs/HOCA-RAPORU.md`, PDF, şemalar, mail) plan diliyle yazıldı: "yapmak istiyorum / planlıyorum". Bu belgelerde şunlar **yok** ve eklenmemeli:
  "yaptık / tamamlandı / X test geçiyor / şu an mock kullanıyoruz / demo", controller adları, kodu ele veren ifadeler.
- Hamza'nın hoca belgeleri için koyduğu ek kurallar:
  - "Kural motoru", "sistem kural koyar / engeller" deme. Ders kaydı kontrolü hocanın **elle yaptığı** çakışma / AKTS / alttan ders
    kontrolünü otomatikleştirir. Sistem bulduğunu hocanın önüne koyar, **karar hocadadır** (çakışma var ama hoca sorun etmiyorsa kaydı onaylar).
    Mekanizmayı değil amacı yaz.
  - Rapordan bilerek çıkarılanlar: "kural motoru", tekrar alma kuralı (dilekçeyle yürüyor), öğretim türü maddesi. Hocaya giden belgede kalan kontrol maddeleri: çakışma, AKTS, alttan ders.
  - Rapor birinci tekil, Hamza'nın ağzından yazılı ("planlıyorum", "hocam"). Aynı tonu koru.
  - Giriş: kullanıcılar mevcut **OBİS hesabıyla** girer (öğrenci okul no + OBİS şifresi). Sicil no giriş bilgisi değil, giriş sonrası arka planda
    "danışmanın öğrencileri" sorgusunun anahtarıdır. Kullanıcıya sicil no yazdırma.
  - "OBİS'te olmayan veriyi kendi DB'mizde tutarız" bir **amaç** değil, teknik nottur; amaç cümlelerinde geçmemeli.
- Bu kural **yalnızca hocaya giden belgeler** için. `README`, `DECISIONS`, `ARCHITECTURE` iç teknik dokümandır, gerçeği yazar. Ama bunlar hocaya
  **gönderilmemeli** (özellikle README'deki demo ve test sayıları).
- `Desktop/ODBS Ara Rapor.pdf` eski, "yapıldı" dilli sürümdür; hocaya gönderilmemeli. Güncel olan `Desktop/ODBS Proje Planı.pdf` (8 sayfa).
  Her ikisi Hamza'nın bilgisayarında, repoda değil. Plan PDF'i `docs/HOCA-RAPORU.md`'den üretilir (Markdown → HTML → headless Edge/Chromium PDF,
  `.er` sınıflı bloklar yatay sayfa). Üretim betiği repoda yok; gerekirse yeniden yaz.

## 3. Teknoloji

| Parça | Seçim |
| --- | --- |
| Arka yüz | ASP.NET Core, C#, **.NET 8**, katmanlı: `Advisor.Domain`, `Advisor.Application`, `Advisor.Infrastructure`, `Advisor.Api` |
| Veritabanı | **MSSQL** (yerel `localhost`, `AdvisorDb`, Windows kimlik doğrulama), EF Core. 2026-11-08'de SQLite'tan geçildi; tek migration `InitialSqlServer`, 16 tablo, açılışta otomatik uygulanır |
| Ön yüz | React + TypeScript + Vite (`frontend/`), rol bazlı ekranlar |
| OBİS | `IObisClient` adapter deseni. Geliştirmede **bağımsız Mock OBİS API** (`Obis.MockApi`, `localhost:5280`); gerçek OBİS bağlanınca yalnızca bu arayüzün gerçek uygulaması yazılır |
| Kimlik | JWT (120 dk), 5 rol: `Ogrenci`, `Danisman`, `BolumBaskani`, `Dekan`, `Admin` |
| Test | xUnit. Test projesi `net10.0` (makinede yalnızca .NET 10 çalışma zamanı vardı; yalnızca .NET 8 varsa csproj'da `8.0.*` yap) |

Her istekte iki denetim: uç noktanın izin verdiği **rol**, sonra kullanıcının o öğrenciyi görme **kapsamı** (öğrenci kendini, danışman kendi
öğrencilerini, bölüm başkanı bölümünü, dekan fakültesini, admin herkesi).

## 4. Çalıştırma

Windows, repo kökünde:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-all.ps1   # Mock OBİS + API + frontend
powershell -ExecutionPolicy Bypass -File .\stop-all.ps1
```

| Ne | Adres |
| --- | --- |
| Uygulama | http://localhost:5173 |
| API + Swagger | http://localhost:5154/swagger |
| Mock OBİS + Swagger | http://localhost:5280/swagger |

Günlükler `.run/` içine gider. Önkoşul: .NET SDK, Node 20+, yerel SQL Server. Betik bu kabukta `-ExecutionPolicy Bypass` ister.
Demo hesaplar (şifre hepsinde `Demo123!`): `admin`, `danisman1`-`danisman4`, `bolumbaskani1`, `dekan1`, `ogrenci1`-`ogrenci4`.
`danisman1` ve `ogrenci2` birbirine bağlı ve uç durumları taşıyor, test ederken ilk bunlara bak. Tam liste README'de.

## 5. Test ve doğrulama

```bash
cd backend && dotnet test                       # 369 test
cd frontend && npm run lint && npm run build    # oxlint 0 uyarı, tsc, vite build
```

- Çoğu test EF InMemory ile koşar (hızlı ama indeks/kısıt/tür eşlemesini sınamaz). `SqlServerIntegrationTests` (8 test) gerçek SQL Server'a karşı
  koşar, her seferinde kendi geçici `AdvisorDb_Test_*` veritabanını kurup siler, canlı `AdvisorDb`'ye dokunmaz; SQL Server yoksa atlanır.
  Başka sunucu için `ODBS_TEST_SQLSERVER` ortam değişkeni.
- Her endpoint için otomatik yetki taraması var (anonim 401, rol dışı 403, onaylı istisna listesi). **Yeni endpoint ekleyince bu taramayı da güncelle.**
- Bir değişiklik "bitti" sayılmadan önce gerçekten çalıştırıp doğrula (test + canlı sistemde ilgili akış). Bu projede doğrulama, yazılan koddan daha çok hata buldu.
- Canlı veriyi test için silme: `AdvisorDb` ve yüklenen ekler gerçek kullanımdadır; test için ayrı veritabanı/kopya kullan.

## 6. Veri ve iş kuralları (Hamza'nın verdiği kararlar)

Aşama aşama gerekçeleri `docs/DECISIONS.md`'de. Kod yazarken bozulmaması gerekenler:

- **Mock OBİS seed'i:** 63 öğrenci, 4 danışman, Selçuk Bilgisayar Mühendisliği'nin **118 gerçek dersi** (`CourseCatalog.cs`, kodlar 3301xxx), 2026-2027 güz
  gerçek ders programı (`FallSchedule.cs`). Kişi verisi kurgusal. Müfredat sayfasında önkoşul ve kredi yoktu: önkoşullar (yalnızca zorunlu zincirler) ve kredi
  (AKTS'den türetilmiş) **varsayımdır**. Müfredat: 240 AKTS, 30 seçmeli kredi, 40 gün staj.
- **Sınıf başına bir danışman:** 1. sınıf Mehmet Arslan (ADV2), 2. Zeynep Kara (ADV3), 3. Elif Yıldız (ADV1), 4. Prof. Dr. Ali Çelik (ADV4).
  Yönetici kuralı aşabilir (istediği öğrenciyi başka danışmana atar), arayüzde uyarı yok.
- **İkinci öğretim (İÖ) yalnızca 4. sınıfta.** NÖ saatleri 08:30-16:55, İÖ 17:00-21:55. "(NÖ/İÖ)" şubesi iki öğretimin birlikte ders yaptığı anlamına gelmez,
  1-3. sınıf dersinin tek gündüz şubesidir. Bu etiketi kendi varsayımınla yorumlama; belirsizse Hamza'ya sor.
- **Ders kaydı kontrolleri** (`IRegistrationRule`, DI'a otomatik kaydolur, yeni kural = yeni sınıf). Şu an 5 kural var (`TimeConflictRule`, `SectionProgramRule`, `RetakeRule`, `CreditLimitRule`, `LowerSemesterPriorityRule`); bulguların çoğu **Hata**, alttan ders önceliği **Uyarı** seviyesinde:
  - Saat çakışması: OBİS ders programı + öğrencinin seçtikleri karşılaştırılır. Alttan ders temel alınır, üst dersin başka şubesi önerilir. İki dersten **en az birinde devam şartı
    yoksa çakışma sayılmaz** (`ObisRegistration.AttendanceRequired`; gerçek OBİS'teki kaynağı belirsiz, mock'ta bayrak).
  - Öğretim türü: NÖ öğrencisi NÖ şubesinden, İÖ öğrencisi İÖ şubesinden ders alır; NÖ/İÖ ortak şube serbest.
  - Tekrar alma: **BB ve üstüyle** (BB, BA, AA) geçilen ders tekrar alınamaz (`RetakeHighGrade`, varsayılan BB). CB ve altı (CB, CC, DC, DD) serbest. Hamza'nın "BC"si sistemde **CB**'dir
    (Selçuk ölçeği: AA, BA, BB, CB, CC, DC, DD, FD, FF).
  - Dönem AKTS limiti (GANO'ya bağlı: <2.00: 24, 2.00-2.99: 31, ≥3.00: 37; ayarlanabilir). Alttan ders önceliği uyarısı yalnızca o yarıyılda açılan dersleri sayar.
  - **Önkoşul ve sınav çakışması yok**, bilerek kaldırıldı (Hamza kararı, Aşama 15).
- **Haftalık program renkleri:** yeşil (çakışma yok), turuncu (devam şartı yok), kırmızı (çakışma), gri (alınmadı). Izgara gerçek ilk/son saate kırpılır.
- **Erken uyarı:** devamsızlık %20 uyarı, %30 sınır; GANO, eksik zorunlu ders, AKTS, seçmeli kredi, staj günü. Eşikler `RegistrationRules` / `RiskRules` ayarlarında.
- **Danışman e-postası** OBİS'te yok; `ad+soyad` ASCII + `@selcuk.edu.tr` (hoca) / `@ogr.selcuk.edu.tr` (öğrenci) olarak türetiliyor, gerçek adresle uyuşmayabilir.
  E-posta uygulamadan **gönderilmez**; buton Zimbra'yı (`https://ogrposta.selcuk.edu.tr/`) yeni sekmede açar ve adresi panoya kopyalar.
- **Bölüm Başkanı / Dekan** izleme ve atama rolüdür: ders kaydı onaylamaz, anket/duyuru açmaz, randevu ve talep modülünü kullanmaz. Randevu listesi/iptali ve talep listesi
  `Ogrenci, Danisman, Admin` ile sınırlıdır (Aşama 20'de kapatılan gerçek bir yetki açığıydı; geri açma).
- Anket yanıtlarını yalnızca anketin sahibi danışman ve admin görür; KVKK rızası olmadan anket gönderilemez; yüklenen dosyalara virüs taraması yok.

## 7. Planlanan ama kodda olmayanlar (hocaya "yapılacak" diye anlatıldı)

Bunlar rapordaki vaatlerdir; kod henüz yapmıyor, yeni geliştirmenin doğal sırası budur:

1. **Danışman sicil no'su ile öğrenci listesi:** OBİS sözleşmesine `GetStudentsByAdvisorAsync(sicilNo)` (`GET /advisors/{sicilNo}/students`). Giriş yapan danışmanın sicil no'su hesapta tutulacak, kullanıcı yazmayacak.
2. **Danışmana bildirim:** `AdvisorNotifications` tablosu. Kayıt dönemi kapanınca ya da ders kaydı değişince, alması gereken dersi almayan öğrenci için danışmana bildirim (tipler: eksik zorunlu ders,
   alttan ders alınmadı, başarısız ders yeniden alınmadı). Danışman listesine "alması gereken ama almadığı ders" sütunu (müfredat − transkript − bu dönem seçimi). İsteğe bağlı e-posta (Zimbra/SMTP).
3. **Birim ağacı:** `IOrgUnitClient` + yerel `OrgUnits` tablosu (`Id, ParentId, Ad, Tür, Kod`), günlük/haftalık senkron. Bölüm Başkanı/Dekan kapsamı `ParentId` zincirinden hesaplanır;
   öğrenci ve danışman bölüm bilgisi serbest metin yerine `OrgUnits.Id`'ye bağlanır. Neden senkron: ağaç nadir değişir, servis kesilse de uygulama çalışır.
4. **OBİS hesabıyla gerçek giriş** (SSO/LDAP ya da OBİS giriş doğrulama servisi); şu an yerel `Users` tablosu + demo hesaplar.
5. **Gerçek OBİS bağlantısı:** 10 okuma servisi + yukarıdaki sicil no servisi = 11 çağrı tipi (`docs/OBIS-VERI-ENVANTERI.md`). Yalnızca `IObisClient`'ın gerçek uygulaması yazılır; kural motoru, servisler, controller'lar ve ön yüz değişmez.
   Bilgi İşlem'e açık sorular: erişim yöntemi (REST/SOAP/DB görünümü), test ortamı, devam şartı alanının kaynağı, alttan ders işareti, e-posta adresleri, çağrı limiti ve KVKK.
6. Fikirler: danışmana haftalık özet, çok çakışan derste bölüm başkanına şube açma önerisi, anket sonuçları için .xlsx içe/dışa aktarma.

Bilinen eksikler: .xlsx yok (yalnızca CSV), refresh token ve token iptali yok, bildirimler yalnızca uygulama içi, KVKK metni örnek, ARCHITECTURE'daki Mermaid ER diyagramı eski
(`AdvisorAssignments` yok, roller eksik; güncel şema `docs/img/veritabani-semasi.*`), `docs/ARCHITECTURE.md` bazı yerlerde hâlâ "kural motoru" der (iç dokümanda sorun değil).

## 8. Çalışma biçimi (Hamza'nın tercihleri)

- **Dil:** Türkçe. Arayüz metinleri, commit mesajları ve dokümantasyon Türkçe. Kısa, direkt, dolgusuz yaz.
- **Belirsizlikte** durup sorma; makul varsayımla ilerle, seçtiğin yolu tek cümleyle söyle, kararı `docs/DECISIONS.md`'ye yeni "Aşama N" başlığıyla yaz. Hamza yanlışsa döndürür.
  İstisna: Hamza "araştır / planla / yapabilir miyiz" diyorsa yalnızca cevap ver, o izin olmadan kod yazma.
- **Bir şeyi yorumlamadan önce** verilen belgeyi/PDF'i satır satır incele; etiketleri kendi varsayımınla anlamlandırma.
- Dosyayı değiştirmeden önce mevcut halini oku. Aşama bitince `docs/PROGRESS.md` ve `docs/DECISIONS.md`'yi güncelle; test sayısını koddan doğrula, hafızadan yazma.
- Hamza'dan gelen bir düzeltme geçici istek değil, kalıcı kuraldır; yukarıdaki listeye ekle.
- Commit: küçük ve anlamlı, `feat(...)` / `fix(...)` biçiminde.
## 9. Başlarken kontrol listesi

1. `docs/DECISIONS.md`'yi baştan sona oku (kararların gerekçeleri burada, kod bunları göstermez).
2. `start-all.ps1` ile sistemi ayağa kaldır, `danisman1` ve `ogrenci2` ile gez.
3. `dotnet test` çalıştır, 369 yeşil olmalı. Değilse önce nedenini bul.
4. Hocaya giden bir belge yazıyorsan 2. bölümü yeniden oku.
5. Yeni endpoint = yetki taraması + test. Yeni kural = `IRegistrationRule` sınıfı + test + seed uç durumu. Yeni karar = `DECISIONS.md`.
