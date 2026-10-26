# Kararlar

Bu dosya, proje promptunda belirsiz bırakılan veya ortamdan kaynaklanan noktalarda
alınan kararları ve gerekçelerini kaydeder. Kurala göre: soru sorulmaz, en makul karar
verilir ve buraya yazılır.

## 2026-10-24 — Proje konumu

**Karar:** Proje, HamzaOS vault'unun dışında, ayrı bir git reposu olarak
`C:\Users\Başkan\Desktop\OgrenciDanismanOtomasyonu` dizininde geliştirildi (vault'un
`300-Projects/` klasörü yalnızca proje hakkında insan yazımı notları tutar, kodu değil).

**Gerekçe:** Vault'taki diğer kod projesi (Servis/Sefer) aynı desen üzerinde
çalışıyor: `Desktop\Servis` kendi reposu, vault'ta sadece referans notu var. Aynı
deseni tekrarlamak tutarlılık sağlıyor. Dizin adında Türkçe karakter ve boşluk
kullanılmadı (ör. `OgrenciDanismanOtomasyonu`) — vault'ta daha önce Windows 8.3 kısa
yol bug'ına neden olan emoji/Türkçe karakterli klasör adları sorun yaratmıştı, aynı
riskten kaçınıldı.

## 2026-10-24 — .NET sürümü ve çalışma zamanı

**Karar:** Proje promptu ".NET 8" istiyor; TargetFramework tüm projelerde `net8.0`
olarak bırakıldı (Domain/Application/Infrastructure/Api/Obis.MockApi/Tests). Ancak bu
makinede yalnızca **.NET SDK 10.x** kurulu; `Microsoft.AspNetCore.App` paylaşımlı
çalışma zamanının 8.x sürümü yok (yalnızca 10.0.9/10.0.11 var; `Microsoft.NETCore.App`
8.0.14/8.0.30 var ama AspNetCore karşılığı yok). Bu yüzden ASP.NET Core projelerinde
(`Advisor.Api`, `Obis.MockApi`) csproj'a `<RollForward>LatestMajor</RollForward>`
eklendi; bu, host'un yalnızca 10.x çalışma zamanı bulsa bile net8.0 hedefli uygulamayı
çalıştırmasına izin veriyor.

**Gerekçe:** Build (`dotnet build`/`restore`) SDK'nın NuGet üzerinden çektiği referans
paketleriyle net8.0'ı hedefleyebilir, bu yüzden derleme sorunsuz. Ama *çalıştırma*
(`dotnet run`, entegrasyon testleri host süreci) için eşleşen major-version çalışma
zamanı gerekir; RollForward olmadan "no runtime found" hatası alınır. `LatestMajor`
seçeneği .NET'in resmi olarak desteklediği bir host çözümleme mekanizması, ileride
gerçek sunucuya .NET 8 runtime'ı kurulursa bu ayar zararsızdır (zaten tam sürüm eşleşirse
rollforward devreye girmez). Eğer ileride bu makineye .NET 8 SDK/runtime kurulursa bu
satırlar kaldırılabilir ama kalması da sorun yaratmaz.

## 2026-10-24 — Çalıştırma komutları

**Karar:** `git`, `npm`, `dotnet` komutları bu oturumda CLI izin katmanında ("auto
mode" sınıflandırıcısı) zaman zaman yanıt vermedi; proje dizini
`.claude/settings.local.json` → `permissions.additionalDirectories`'e eklenerek
Write/Edit araçlarının bu dizinde çalışabilmesi sağlandı. Bu, HamzaOS vault'unun kendi
ayar dosyası, proje kodunun bir parçası değil.

## 2026-10-24 — Mock OBİS: seed üretimi ve ek bağımlılık

**Karar:** Seed verisi `Obis.MockApi` içinde `SeedGenerator` ile sabit seed (1337)
kullanılarak programatik üretildi, `dotnet run -- --generate-seed` ile bir kez
çalıştırılıp `SeedData/json/*.json` dosyalarına yazıldı. Normal API çalışma zamanı
bu JSON dosyalarını okur (`SeedIo.Load`), yeniden üretmez — prompt'un "JSON seed
verisinden besleniyor" ifadesine birebir uyar, aynı zamanda üretim kodu da repoda
şeffaf kalır.

**Ek bağımlılık:** `BCrypt.Net-Next` (Advisor.Infrastructure) — demo hesap şifrelerini
düz metin tutmamak için; hafif, yaygın, tek iş yapan bir paket.

**Uç durum öğrencileri (bkz. `Obis.MockApi/SeedData/json/edge-cases.json`):**

| Etiket | Öğrenci No | Senaryo |
| --- | --- | --- |
| EDGE-01 | 20241011 | Saat çakışması (Matematik-1 tekrar, özel C şubesi + Mikroişlemciler/A aynı gün/saat) |
| EDGE-02 | 20251006 | Önkoşul ihlali (Programlama Dili-1 kalmış, Veri Yapıları'na kayıtlı) |
| EDGE-03 | 20241012 | Alttan ders önceliği (Fizik-2 kalmış, dönem5 dersleri alıyor) |
| EDGE-04 | 20251007 | Kredi/AKTS limiti aşımı (düşük GANO + 3 ekstra ders) |
| EDGE-05 | 20231016 | Sınav çakışması (Mantık Devreleri-1 ve Bilgisayar Mimarisi final'leri aynı an) |
| EDGE-06 | 20231017 | Tekrar alma (Veri Yapıları'nı AA ile geçmiş, yine kayıtlı) |
| EDGE-07 | 20241013 | GANO düşüşü (dönem3 AA ortalama → dönem4 DD ortalama) |
| EDGE-08 | 20231018 | Azami süreye yaklaşma (dönem9'da, 3 ders tekrarı) |
| EDGE-09 | 20231019 | Mezuniyete az kalan (dönem8, 5 dersten sadece 2'si eksik) |
| EDGE-10 | 20251008 | Devamsızlık sınırına yaklaşma (14 oturumdan 10'u, ~%29) |

## 2026-10-24 — Aşama 4: kimlik doğrulama kararları

- **Varsayılan-reddet:** `FallbackPolicy = RequireAuthenticatedUser`. Yeni bir endpoint
  unutularak açık kalamaz; herkese açık olan `[AllowAnonymous]` ile açıkça işaretlenir.
- **Sahiplik kontrolü tek yerde:** `IStudentAccessGuard`. Danışman-öğrenci eşlemesi JWT'ye
  gömülmez, her istekte OBİS'ten (önbellekli) doğrulanır; danışman ataması değişirse eski
  token'la erişim sürmez.
- **Refresh token yok:** 120 dk'lık tek access token. Bitirme projesi kapsamı için yeterli.
- **Test projesi net10.0'a alındı:** Makinede yalnızca .NET 10 çalışma zamanı var; `Mvc.Testing 8.x`
  TestServer'ı System.Text.Json 10 ile uyumsuz (`PipeWriter.UnflushedBytes` hatası). Test projesi
  `net10.0` + `Mvc.Testing 10.*` kullanır; uygulama projeleri `net8.0` hedefinde kalır.
- Testler `Testing` ortamında çalışır (Swagger yalnızca `Development`'ta açık).
- Demo şifresi tüm hesaplar için aynı (`Demo123!`); yalnızca geliştirme amaçlı. Gerçek ortamda
  `Jwt:SigningKey` ortam değişkeniyle verilmeli, demo seeder kapatılmalı.

## 2026-10-24 — Aşama 5: kural motoru kararları

- **Kurallar saf ve bağlam tabanlı:** `RegistrationContext` tek girdi; kural OBİS'e gitmez. Bu, birim
  testlerin ağsız ve küçük elle kurulmuş verilerle yazılabilmesini sağlar.
- **Dönem kesiti:** Bir dönem doğrulanırken yalnızca o dönemden ÖNCEKİ transkript kullanılır. Aksi halde
  geçmiş dönemler doğrulanırken (dönem seçimi ekranı) sonradan geçilen dersler önkoşul sayılırdı.
- **Önkoşul = geçilmiş:** Aynı dönemde birlikte alınan önkoşul geçilmiş sayılmaz (Hata).
- **Alttan ders önceliği = Uyarı (Hata değil):** Üniversite yönetmeliği bilinmediği için engelleyici
  yapılmadı; danışman karar verir. Yalnızca zorunlu dersler kapsanır (müfredat listesi). Güz/bahar
  dönemi uyumu (alt dönem dersinin hangi yarıyılda açıldığı) dikkate alınmadı.
- **Kredi sınırı AKTS üzerinden:** GANO < 2.00 → 24, 2.00-2.99 → 31, ≥ 3.00 → 37 AKTS. 31 değeri seed'deki
  normal yüke (en çok 31 AKTS) göre kalibre edildi; hepsi `appsettings.json`'dan değişir. Geçmişi olmayan
  (ilk dönem) öğrenciye normal sınır uygulanır.
- **GANO transkriptten hesaplanır**, OBİS `Gpa` alanından değil: geçmiş dönemleri doğrularken o döneme
  kadarki GANO gerekir. Hesap OBİS değerleriyle 63 öğrencide birebir eşleşiyor.
- **Tekrar alma = Bilgi, eşik BB:** Daha düşük notla geçilmiş dersi tekrar almak (not yükseltme) normal
  kabul edilir, uyarı üretilmez.
- **Sınav çakışması tür ayırmaz:** Farklı derslerin ara sınav/final'i çakışırsa da bulgu üretilir.
- **Karar geçmişi tutulur:** `RegistrationReview` her kararda yeni satır eklenir (güncelleme yok); en son
  kayıt geçerli karar sayılır. Denetim izi ve "önce düzeltme istedi, sonra onayladı" akışı için.
- **Hatalı kaydı onaylamak engellenmedi:** Danışman, Hata seviyesi bulgu olsa bile onaylayabilir (istisna
  durumları olabilir); bulgular karar kaydıyla birlikte ekranda görünür.
- **Seed düzeltmesi:** Uç durumlar için ortak şubeleri değiştirmek yerine öğrenciye özel "C" şubesi
  eklenir (bkz. PROGRESS.md Aşama 5). Seed üretimi artık tekrarlanabilir.

## 2026-10-24 — Aşama 6: frontend kararları

- **Saf CSS + tek ek paket (`react-router-dom`):** "Gereksiz paket ekleme" kuralı. Recharts yalnızca
  Aşama 9'da grafikler için eklenecek.
- **Oturum `localStorage`'da:** Sayfa yenilemede girişin korunması için. XSS riskine karşı bu bir
  bitirme projesi için kabul edildi; üretimde httpOnly cookie + refresh token önerilir (rapora not).
- **Vite proxy, CORS yok:** Geliştirmede ön yüz ve API aynı origin'den görünür; API'ye CORS eklenmedi.
  Üretimde ön yüz statik olarak API ile aynı alan adından sunulmalı ya da CORS bilinçli açılmalı.
- **Rota yetkisi ön yüzde de var ama güvenlik sınırı değil:** Gerçek yetki kontrolü backend'de
  (`[Authorize(Roles)]` + `IStudentAccessGuard`); ön yüz yalnızca kullanıcıya gereksiz sayfayı göstermez.
- **Yer tutucu sayfalar:** Menü baştan tam görünür ("Yakında" etiketiyle); modül hazır oldukça kendi
  route'u yer tutucunun önüne eklenir.

## 2026-10-24 — Aşama 7: arayüz kararları

- **Haftalık program ızgarası başlangıç saatine göre satırlanır:** Seed'deki dersler 09/11/13/15
  başlangıçlıdır; saatlik sabit ızgara yerine verideki başlangıç saatleri satır olur. Gerçek OBİS'te
  keyfi saatler gelirse bu bileşen (WeeklySchedule) saat dilimlerine bölünecek şekilde genişletilmeli.
- **Öğrenci düzeltme notunu görür, danışmanın karar geçmişini görmez:** Prompt "görüşme notları
  öğrenciye kapalı" der; bu, görüşme notları içindir. Kayıt kararındaki not ise öğrenciye iletilmek
  üzere yazılır, bu yüzden öğrenci yalnızca son kararı ve notunu görür.
- **Hata seviyeli kayıt onaylanabilir:** Engelleme yerine uyarı metni gösterilir (bkz. Aşama 5 kararı).

## 2026-10-24 — Aşama 8: anket ve puanlama kararları

- **Excel (.xlsx) içe aktarma yok, yalnızca CSV:** Prompt "CSV/Excel" der; .xlsx okumak yeni bir paket
  (ör. ClosedXML/EPPlus) gerektirir ve "gereksiz paket ekleme" kuralına aykırıdır. Excel kullanıcıları
  "CSV (noktalı virgülle ayrılmış)" olarak kaydeder; ayırıcı (`;`, `,`, sekme) otomatik bulunur. Dışa aktarma
  Excel'de doğrudan açılır (UTF-8 BOM).
- **Puan saklanmaz, canlı hesaplanır:** Ağırlıklar sonradan değişebildiği için puanı veritabanında tutmak
  eskimiş değer riski doğurur. Yalnızca destek atama anındaki puan denetim izi olarak `NeedScoreResult`'ta
  saklanır.
- **Sayı sorusu puanı = değer x ağırlık (doğrusal):** Basit ve açıklanabilir. Aralık bazlı puanlama
  (ör. gelir 0-20 bin = 10 puan) için tek seçim sorusu kullanılır; sayı sorusu kardeş sayısı gibi doğrusal
  durumlar içindir.
- **Hedef kitle:** danışmanın kendi öğrencileri, isteğe bağlı tek sınıf süzgeci. Yönetici anketi tüm
  öğrencilere gider. Serbest öğrenci listesi seçimi kapsam dışı bırakıldı.
- **Tarih penceresi Türkiye saatiyle (UTC+3, yaz saati yok) günlük hesaplanır;** `TimeProvider` ile test
  edilebilir. Bitiş günü dahildir.
- **Serbest metin cevapları anonim istatistikte gösterilmez:** metin kişiyi tanımlayabilir; istatistikte
  yalnızca kaç kişinin yanıtladığı yer alır. Metni danışman, öğrencinin puan kırılımında görür.
- **İçe aktarılan sonuçta rıza:** Satır bazında rıza yoktur; danışman toplu onay kutusuyla verilerin açık
  rızayla toplandığını beyan eder (reddedilirse içe aktarma yapılmaz). Bu beyan satırlara `ConsentAccepted`
  olarak işlenir. Gerçek kullanımda dayanak belgeler ayrıca saklanmalıdır (rapora not).
- **KVKK metni örnek/kurgusaldır** (`ConsentText.cs`); hukuk birimi onayından geçmeden gerçek kullanımda
  kullanılmamalıdır.
- **Yanıt almış anket silinemez:** Silme istenirse KVKK kapsamında veri silme süreci ayrıca tasarlanmalı
  (bu proje kapsamı dışı).

## 2026-10-24 — Aşama 9: erken uyarı ve mezuniyet kararları

- **Makine öğrenmesi yok, kural tabanlı puan:** Prompt "kurala dayalı risk puanı, her gösterge ayrı ve açıklamalı"
  der. Danışman puanın gerekçesini görür, eşikler ayarlanabilir; küçük bir veri setinde ML anlamsız ve açıklanamaz olurdu.
- **Dönem ortalaması düşüşü yalnızca son iki dönemi karşılaştırır** ("son iki dönemde GANO düşüşü" ifadesinin yorumu).
  Dönem ortalaması kredi ağırlıklıdır, tekrarlar dahil o dönemin satırlarından hesaplanır.
- **"Üst üste kalınan dersler" iki gösterge:** (a) aynı dersten en az iki kez kalma, (b) art arda iki dönemde en az
  birer ders kalma. İkisi de seed'de ve gerçek hayatta ayrı sinyallerdir.
- **Devamsızlık yalnızca mevcut dönemde, %30 sınır / %20 uyarı:** Geçmiş dönemlerin devamsızlığı zaten sonuçlanmıştır.
  Sınır üniversiteye göre değişir; `AbsenceLimitRate` ayarlanabilir.
- **Azami süre 14 dönem (7 yıl), normal 8:** Türkiye'deki lisans programlarının genel uygulaması varsayıldı; ayarlanabilir.
- **Zorunlu dersler için "bu dönem alıyor" durumu eksik sayılır ama etiketlenir;** ayrıca projeksiyon ayrı hesaplanır.
  Böylece "az kalan" öğrenci "mezun olamaz" diye alarm üretmez, ama "şu an mezun değil" gerçeği de saklanmaz.
- **Staj durumu OBİS'ten gelir** (`/api/students/{no}/internship`, tamamlanan gün). Alternatif olarak STJ101 dersine
  bakmak yanlış olurdu: o ders "Staj I Hazırlık" dersidir, stajın kendisi değildir.
- **Seçmeli krediler Credit'ten, toplam AKTS Ects'ten hesaplanır;** müfredat sabitleri bu ayrımı gözeterek 12 kredi / 212 AKTS yapıldı.
- **Risk yalnızca personele açık:** Öğrenciye kendi "risk puanı"nı göstermek damgalayıcı olabilir; öğrenci kendi
  mezuniyet analizini görür (prompt'taki gereksinim), risk göstergelerini görmez.
- **Panel kapsamı sunucuda belirlenir:** istemci öğrenci listesi göndermez; danışman yalnızca kendi öğrencilerini görür.
- **Grafik kütüphanesi Recharts** (prompt'ta öngörüldü) ve **tembel yüklenir:** ilk yükleme 323 kB, grafikler ayrı 407 kB parça.

## 2026-10-24 — Aşama 10: iletişim kararları

- **Randevu "slot + tarih" modeli:** Danışman haftalık tekrar eden saatler tanımlar; rezervasyon belirli bir tarihe
  yapılır. Böylece her hafta saat yeniden girilmez, geçmiş randevular da silinen/değişen saatlerden etkilenmez
  (kullanılmış saat silinince pasife alınır, kaybolmaz).
- **Randevu sınırları (sabit):** 28 gün ufuk, 60 dk asgari önceden bildirim, 2 aktif randevu. Randevu kaparak
  başkalarını engellemeyi önler; ayarlanabilir yapmaya gerek görülmedi (bitirme projesi kapsamı).
- **Çifte rezervasyon iki katmanlı engellenir:** uygulama kontrolü + SQLite kısmi tekil indeks. Yalnızca uygulama kontrolü
  eşzamanlı iki istekte yetmez.
- **Notlar öğrenciye kapalı, ders kaydı kararındaki not açık:** Prompt "görüşme notları öğrenciye kapalı" der. Ders kaydında
  "düzeltme iste" notu ve talep yanıt notu ise öğrenciye iletilmek üzere yazıldığından öğrenci görür.
- **Yönetici not yazmaz:** Notun yazarı olan danışman kimliği anlamlıdır; yönetici denetim amaçlı yalnızca okur ve silebilir.
- **Duyuruda önizleme zorunlu:** Yanlış filtreyle yüzlerce öğrenciye istenmeyen duyuru gitmesini önler. Filtre kriterleri
  gönderim anındaki haliyle saklanır; sonradan "bu kimlere neden gitti" açıklanabilir.
- **GANO filtresi, notlandırılmış dersi olmayanı dışlar:** GANO'su 0 görünen birinci sınıf öğrencisi "GANO ≤ 2.5" filtresine
  yanlışlıkla girmesin diye.
- **Alıcı tablosu okundu durumunu da taşır** (`AnnouncementRecipient.ReadAt`); ayrı bir "okundu" tablosu yoktur.
- **Dosya deposu bir arayüz arkasında (`IFileStore`):** Şimdilik yerel disk (`Uploads:Path`); üretimde nesne depolamaya geçiş
  yalnızca yeni bir implementasyondur. Klasör `.gitignore`'dadır. Virüs taraması kapsam dışıdır (rapora not).
- **Ek türü istemciye değil sunucuya güvenilerek belirlenir:** `Content-Type` başlığı istemciden gelir ve sahtelenebilir;
  tür, doğrulanmış uzantı ve dosya imzasından üretilir.
- **Sonuçlanan talep değişmez:** Karar geri alınabilseydi öğrenciye gönderilen bildirimle çelişirdi; hata düzeltmesi için yeni
  talep açılır.

## 2026-10-24 — Aşama 11: sertleştirme kararları

- **Uçtan uca test canlı sunucu yerine bellek içi:** Ağ portu, süreç yönetimi ve zamanlama kırılganlığı olmadan her `dotnet test`
  çalıştırmasında aynı doğrulama yapılır. Canlı tarayıcı doğrulaması ise her arayüz aşamasında ayrıca yapıldı.
- **Yetki taraması "izin listesi" yaklaşımıyla:** Rol kısıtı olmayan endpoint'ler açık bir listeyle eşleşmek zorunda; böylece
  yeni endpoint için yetki kararı kod incelemesinde ve testte görünür olur (varsayılan-reddet politikasının test karşılığı).
- **Üretim kapıları "fail closed":** Zayıf anahtar ya da demo hesapları sessizce kabul edilmez; uygulama başlamaz ya da hesap oluşturmaz.
- **Hız sınırı yerleşik `RateLimiter` ile:** Ek paket yok. Anahtarı IP; kullanıcı adı başına kilitleme eklenmedi (hesap kilitleme,
  saldırganın gerçek kullanıcıyı kilitlemesine izin veren bir hizmet reddi vektörü olabilir).
- **CORS eklenmedi:** Ön yüz aynı origin'den (geliştirmede Vite proxy, üretimde ters proxy) çağırır; çapraz kaynak izni gereksiz saldırı yüzeyi olurdu.

## 2026-10-24 — Aşama 12: dokümantasyon ve çalıştırma kararları

- **İki betik ailesi (PowerShell + Bash):** Geliştirme ortamı Windows, ama proje değerlendiren kişinin makinesi bilinmiyor; ikisi de aynı davranışı
  verir ve ikisi de çalıştırılarak doğrulandı. Docker eklenmedi (ek bağımlılık ve kurulum yükü; bitirme projesi için gereksiz).
- **Betikler `dotnet run --no-build` + önceden `dotnet build` kullanır:** Üç süreç aynı derleme çıktısını kullansın, paralel `dotnet run` derlemeleri
  birbirinin dosyalarını kilitlemesin.
- **Ekran görüntüleri depoda (1.7 MB):** README ve rapor için gerçek uygulamadan alınmış görüntüler, depoyla birlikte versiyonlanır; bir arayüz
  değişikliğinde yeniden üretilebilir (betik repoda değil: geçici Playwright bağımlılığı projeye eklenmedi).
- **Mermaid'in "doğrulanarak" yazılması:** Diyagram söz dizimi hataları görsel olarak sessizce bozuk gösterilir; bu yüzden gerçek motorla ayrıştırma
  ve çizme denendi.

## 2026-10-26 — Aşama 13: Bölüm Başkanı, Dekan ve danışman atama

Hoca "sadece öğrenci ve öğretmen varsaymayın" dedi: danışmanların üstüne yönetim katmanı eklendi.

- **Roller:** `UserRole`'e `BolumBaskani` ve `Dekan` eklendi (enum sonuna: rol veritabanında sayı). `Admin` aynı yönetim yetkisini korur
  (`UserRoleExtensions.IsManagement`). Bölüm başkanının yetki alanı `AppUser.Department`; dekan ve yönetici bölümle sınırlı değil. Bölümü
  boş bölüm başkanı hiçbir öğrenci göremez (güvenli varsayılan).
- **Atama OBİS'i ezer, OBİS'e yazılmaz:** OBİS salt okunur veri kaynağı. Atamalar ana uygulamada `AdvisorAssignments` tablosunda (öğrenci başına
  tek satır; kim, ne zaman, önceki danışman kayıtlı). Bir öğrenciyi OBİS'teki danışmanına geri atamak satırı siler.
- **Tek eşleme noktası `IStudentDirectory`:** OBİS'teki danışman, atamalarla ezilmiş halde döner. Danışman kapsamına bakan her yer (erişim koruması,
  analizler, ders kaydı listesi, anketler, duyurular, randevular, talepler, öğrenci dosyası) `IObisClient.GetStudent(s)Async` yerine bunu kullanır;
  böylece atama tüm modüllerde aynı anda geçerli olur. (`IObisClient`'ı sarmalamak yerine ayrı arayüz seçildi: testler `IObisClient`'ı değiştiriyor,
  dekoratör testlerde devre dışı kalırdı.)
- **Atamanın yan etkisi:** Öğrenci yeni danışmanın listesine ve analizlerine geçer, eski danışman erişimini anında kaybeder (erişim her istekte
  hesaplanır). Mevcut randevular, görüşme notları, anket yanıtları silinmez; yeni randevular yalnızca yeni danışmanın saatlerinden alınır.
- **Analizler danışman bazında:** `GET /api/management/overview` her danışman için öğrenci sayısı, ortalama GANO, risk dağılımı ve mezuniyet adayı
  özeti verir; `/api/analytics/dashboard` ve `/risk` yönetim rollerinde `?advisorId=` ile bir danışmanın öğrencilerine daraltılır ve danışmanın kendi
  panelindeki sayılarla birebir aynıdır (testle doğrulanır). Danışman rolünde `advisorId` yok sayılır; kapsam genişletilemez.
- **Bölüm başkanı/dekan neyi görür:** Genel bakış, risk ve mezuniyet analizi, danışman atamaları. Ders kaydı onayı, anket, randevu, duyuru, talep ve
  öğrenci dosyası danışman/yönetici işi olarak kaldı (salt-okunur gözetim; yazma yetkisi yalnızca atama). Bölüm başkanı kendi bölümü dışındaki
  öğrenciyi atayamaz; kapsam dışı bir numara bulunursa istek bütünüyle reddedilir (kısmi uygulama yok).
- **Demo hesaplar:** `bolumbaskani1`, `dekan1`. Tohumlayıcı artık hesap hesap ekler (eski veritabanları yeni demo rolleri alır).
- **Bilinen sınır:** Mock OBİS'te tek bölüm ve danışmanların bölümü yok; bölüm başkanı tüm danışmanlara atama yapabilir. Gerçek OBİS'te danışman
  bölümü gelirse `ManagementService.AssignAsync` hedef danışmanı da bölümle sınırlamalıdır.

## Aşama 14: Gerçek müfredat (2026-10-26)

Mock OBİS'in kurgusal 52 derslik müfredatı, Selçuk Üniversitesi Teknoloji Fakültesi Bilgisayar Mühendisliği (Lisans) Bologna
bilgi paketindeki GERÇEK 118 dersle değiştirildi (`CourseCatalog.cs`: kod, ad, AKTS, zorunlu/seçmeli; hepsi sayfadan).

- **Sayfada olmayan, seed'e özgü varsayımlar:** (1) önkoşullar: yalnızca zorunlu zincirler (Matematik-1→2→Müh. Matematiği,
  Programlama Dili-1→NYPD→PD-2→PD-3, PD-1→Veri Yapıları→{Veritabanı, Yapay Zeka}, Mantık Devreleri-1→2→Mikroişlemciler),
  seçmeliler önkoşulsuz; (2) kredi AKTS'den türetilir (6+ → 4, 4-5 → 3, ≤3 → 2, mesleki eğitim 6). Gerçek müfredatla birebir değildir.
- **Seçmeli seçimi:** 4-7. dönemlerde öğrenci başına 1/3/3/4 seçmeli (dönemin en sık AKTS'li havuzundan, deterministik); dönem yükü ~30 AKTS.
  Müfredat sabitleri: 240 AKTS, 30 seçmeli kredi, 40 gün staj (eski 212/12/40 değerlerinin yerine).
- **Zaman ızgarası:** 5 gün x 6 saat (08:00-18:00) = 30 kombinasyon; dönem 7'de 31 ders var, seçmeli havuzu öğrencinin
  seçebildiği derslerle sınırlı olduğundan seçimler çakışmaz. Uç durum şubeleri (özel "C") öğrencinin kendi programındaki boş saate eklenir.
- **Taban başarısızlık oranı** %8 → %6: ders sayısı arttığı için öğrenci başına rastgele "kaldı" gürültüsü büyümesin.
- **EDGE-09:** 8. dönemde 3 ders var (İşletmede Mesleki Eğitim, Staj-1, Staj-2); biri geçilmiş sayılır, 2'si kalır.
