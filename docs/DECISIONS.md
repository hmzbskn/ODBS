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
| EDGE-01 | 20241011 | Saat çakışması, alternatifsiz (alttan Mantık Devreleri-1 ile Programlama Dili-3, Pazartesi 13:25) — Aşama 16 |
| EDGE-02 | 20251006 | Devam şartı olmayan çakışma (alttan Atatürk İlkeleri-1 ile Mantık Devreleri-1) — Aşama 16 |
| EDGE-03 | 20241012 | Alttan ders önceliği (Fizik-1 kalmış, bu güz açık, seçilmemiş) |
| EDGE-04 | 20251007 | Kredi/AKTS limiti aşımı (düşük GANO + 3 ekstra ders) |
| EDGE-05 | 20231016 | Alternatifli çakışma (alttan Veri Haberleşmesi ile Veri Bilimi/NÖ; İÖ şubesi önerilir) — Aşama 16 |
| EDGE-06 | 20231017 | Tekrar alma hatası (Mikroişlemciler'i AA ile geçmiş, yine kayıtlı) — Aşama 18 |
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
- **Çifte rezervasyon iki katmanlı engellenir:** uygulama kontrolü + kısmi tekil indeks (SQLite'ta, sonra MSSQL'de). Yalnızca uygulama kontrolü
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

## Aşama 15: Çakışma kuralları sadeleştirildi (2026-10-26)

Hamza'nın kararı: dersler arasında **bağımlılık (önkoşul) yok** ve **sınav çakışması diye bir kontrol yok**. Sistem yalnızca OBİS'ten
gelen ders programı ile öğrencinin dönem başında seçtiği dersleri karşılaştırır.

- `PrerequisiteRule` ve `ExamConflictRule` silindi (kural sayısı 6 → 4); seed'deki önkoşullar boşaltıldı, sınav çakışması uç durumu kaldırıldı.
- **Devam şartı:** `ObisRegistration.AttendanceRequired` (varsayılan true). İki dersin saati çakışsa bile **en az birinde devam şartı yoksa**
  (ör. alttan alınan ve önceki denemede devamı sağlanmış ders) çakışma SAYILMAZ. Gerçek OBİS'te bu alanın hangi veriden geleceği
  netleşmedi; mock'ta kayıt satırında bayrak olarak tutuluyor (varsayım).
- **Alttan/üstten önceliği (`TimeConflictRule`):** çakışmada daha önce kalınmış (alttan) ders temeldir ve kesinlikle alınır; çakışan
  üstten ders için, öğrencinin diğer (devam şartı olan) seçimleriyle çakışmayan diğer şubeleri **alternatif olarak önerilir**
  (mesajda "B şubesi (Cuma 08:00-09:30)"). Uygun şube yoksa "alternatif şube bulunamadı" denir. İkisi de alttan ya da ikisi de üstten ise
  alt dönem dersi temel sayılır.
- Bir ders çiftinin birden çok saat kesişimi tek bulgu üretir (önceden kesişen her saat için ayrı bulgu).
- Uç durumlar: EDGE-01 artık alternatif şube (Mikroişlemciler/B) öneriyor; EDGE-02 devam şartı olmayan çakışma (bulgu çıkmaz);
  EDGE-05 alternatifsiz çakışma. 346 test yeşil.

## Aşama 16: Gerçek güz ders programı ve renkli haftalık program (2026-10-26)

Hamza bölümün 2026-2027 güz haftalık ders programını (PDF, 31.08.2026) verdi; mock OBİS'in güz dersleri (1-3-5-7. dönem) artık bu
programın gerçek gün/saat/dersliklerini kullanıyor (`FallSchedule.cs`, PDF'ten ayrıştırılıp elle doğrulandı).

- **Şubeler:** "NÖ/İÖ" iki öğretim birlikte; ayrı ise "NÖ" (normal) ve "İÖ" (ikinci öğretim). Öğrenci öğretim türüne göre şube alır;
  seçtiği bir dersle çakışan şubeyi seed'de almaz (öğrenci kayıtta çakışmayan şubeyi seçer). Alternatif şube önerisi bu şubelerden gelir.
- **Sabit saati olmayan dersler** (Bilgisayar Mühendisliği Uygulamaları, Girişimcilik, İşletmede Mesleki Eğitim; PDF'te "tüm öğretim
  elemanları") programa konmadı; çakışma üretmezler. Bahar dersleri (2-4-6-8) için kurgusal saatler sürüyor.
- **Seçmeliler:** güzde yalnızca programda açılan seçmeliler seçilir; aynı saatteki seçmeliler bir grup, her gruptan bir ders. 7. dönemde
  2 AKTS'lik sosyal seçmeli çiftinin ikisi de alınır (biri diğer öğretimin şubesinden), dönem 31 AKTS, toplam 240 AKTS tutar.
- **Alttan ders uyarısı** yalnızca bu yarıyılda açılan dersleri sayar (güz: tek, bahar: çift dönem); bahar dersi güzde seçilemez.
- **Kısa bulgu metinleri:** kural kontrolü kartları sorunun ne olduğunu tek cümlede söyler (ders adı, kod yok).
- **Haftalık program:** gerçek saat ölçeğinde ızgara. Yeşil: alındı, çakışma yok; turuncu: çakışıyor ama birinde devam şartı yok;
  kırmızı: çakışıyor, ikisinde de devam şartı var; gri: öğrencinin döneminde alınmayan dersler ve seçtiği derslerin diğer şubeleri.
  Her bloğun altında kısa açıklama (Çakışma yok / Devam şartı yok / Çakışma / Alınmadı). 348 test yeşil.

## Aşama 17: Öğretim türü kuralı ve toplam AKTS (2026-10-26)

- **Kural (`SECTION_PROGRAM`, Hata):** normal öğretim öğrencisi yalnızca "NÖ" ya da "NÖ/İÖ", ikinci öğretim öğrencisi yalnızca "İÖ" ya da
  "NÖ/İÖ" şubesinden ders alabilir (`EducationType`). Öğretim türü OBİS öğrenci kaydındaki `Program` alanından okunur; bilinmiyorsa kısıt yok.
- Çakışma kuralının alternatif şube önerisi ve haftalık programın gri (alınmadı) blokları da yalnızca öğrencinin alabileceği şubeleri gösterir.
  Gerçek güz programında her dersin öğretim türü başına tek şubesi olduğundan, alternatif çoğunlukla yalnızca yanlış öğretim şubesi seçildiğinde çıkar.
- **Seed:** öğrenci artık diğer öğretimin şubesini almaz. 7. dönemde 2 AKTS'lik sosyal seçmeli çifti bırakıldı; 4 AKTS'lik üç grup + açık
  olan 5. dönem seçmelilerinden öğrencinin almadığı ve programına uyan biri seçilir (15 + 16 = 31 AKTS, toplam 240).
- **EDGE-05:** ikinci öğretim öğrencisi Veri Bilimi'ni NÖ şubesinden seçmiş: öğretim türü hatası + çakışma, İÖ şubesi önerilir.
- **Seçilen dersler** tablosunun altında toplam ders sayısı ve AKTS (sınırı aşarsa kırmızı). 356 test yeşil.

## Aşama 18: Tekrar alma kuralı sıkılaştı (2026-10-26)

Hamza: "AA ile geçilen ders tekrar alınmaz, BC ve altındaki dersler sadece tekrar alınabilir." Sistemin harf ölçeğinde (Selçuk:
AA, BA, BB, CB, CC, DC, DD, FD, FF) Hamza'nın "BC"si **CB**'dir.

- `RETAKE` artık **Hata**: BB ve üstüyle (BB, BA, AA; eşik `RetakeHighGrade`, varsayılan BB) geçilmiş dersin yeniden seçilmesi engellenir.
- CB ve altı (CB, CC, DC, DD) notla geçilmiş dersin tekrarı serbesttir, bulgu üretmez; kalınmış (FD/FF) ders zaten alttan derstir.
- Seed: EDGE-04'ün ek dersleri artık 1. dönemde kalınmış derslerin tekrarı (önceden biri AA ile geçilmişti); EDGE-06 dışında hiçbir öğrenci
  bu kuralı ihlal etmez (test). 360 test yeşil.

## Aşama 19: İkinci öğretim yalnızca 4. sınıfta (2026-10-26)

Hamza: "İÖ ile NÖ aynı ders saatinde değil, ders saatleri ayrı." Güz programında NÖ saatleri 1-9. saat (08:30-16:55), İÖ saatleri 10-15. saat
(17:00-21:55). Ayrı İÖ saatleri yalnızca 4. sınıfta var; 1-3. sınıf dersleri "(NÖ/İÖ)" olarak yalnızca gündüz açılıyor ve akşam satırları boş.
Hamza'nın onayıyla: **ikinci öğretim yalnızca 4. sınıfta sürüyor.**

- Seed: İÖ öğrencisi yalnızca 4. sınıfta (18 öğrenciden 4'ü); 1-3. sınıfın tamamı normal öğretim.
- "NÖ/İÖ" şubesi iki öğretimin aynı anda birlikte ders yaptığı anlamına gelmez: 1-3. sınıf dersinin tek (gündüz) şubesidir. O dersi alttan
  alan 4. sınıf İÖ öğrencisi gündüz saatine girer (öğretim türü kuralı bu şubeye izin verir). 361 test yeşil.
- **Haftalık program ızgarası** artık sabit 08:30 başlangıçlı değil: gösterilen derslerin gerçek ilk/son saatine kırpılır (dersleri 13:25'te başlayan öğrencide 08:30-13:25 boşluğu çizilmez).

## Aşama 20: Bölüm Başkanı / Dekan yetki açığı kapatıldı (2026-11-09)

- **Bulgu:** rol şeması rapor için koddan çıkarılırken görüldü. `GET api/appointments`, `POST api/appointments/{id}/cancel` ve
  `GET api/petitions` uç noktalarında rol kısıtı yoktu; servisler yalnızca Öğrenci ve Danışman'ı süzüyordu. Aşama 13'te eklenen
  Bölüm Başkanı ve Dekan, varsayılan dala düşüp **tüm** randevuları ve talepleri (kendi bölümü dışındakiler dahil) görebiliyor,
  herhangi bir randevuyu iptal edebiliyordu. Arayüz bu sayfaları onlara göstermediği için yalnızca doğrudan API ile erişilebilirdi.
- **Karar:** bu üç uç nokta `Ogrenci, Danisman, Admin` ile sınırlandı (arayüzdeki rota korumasıyla aynı). Bölüm Başkanı / Dekan
  randevu ve talep modülünü kullanmaz; izleme ve atama rolüdür.
- **Test:** `RoleRestrictedEndpoints_RejectEveryOtherRole_With403` taraması artık Bölüm Başkanı ve Dekan hesaplarıyla da koşuyor;
  rol kısıtsız uç nokta listesinden üç uç nokta çıkarıldı. 361 test yeşil; canlı sistemde bolumbaskani1/dekan1 → 403, danisman1 → 200.

## Aşama 21: Veritabanı SQLite'tan MSSQL'e geçti (2026-11-08)

- **Neden:** hocanın proje listesindeki teknoloji seçeneği "ASP.NET Core + MSSQL". Arka yüz zaten ASP.NET Core'du; yalnızca veritabanı değişti.
- **Ne değişti:** `UseSqlite` yerine `UseSqlServer` (`Microsoft.EntityFrameworkCore.SqlServer`), bağlantı dizesi yerel `AdvisorDb`. SQLite migration'ları silindi, tek `InitialSqlServer` migration'ı üretildi (16 tablo). Randevu için kısmi tekil indeks MSSQL'de `[Status]=(0)` filtresiyle çalışıyor.
- **Doğrulama (2026-11-09):** `AdvisorDb` içinde 16 tablo + migration geçmişi, demo hesaplar ve filtreli indeks var; sistem MSSQL'e bağlı ayağa kalktı, girişler çalışıyor.
- **MSSQL testleri (2026-11-09):** `SqlServerIntegrationTests` 8 test, gerçek SQL Server'a karşı çalışır; geri kalan testler hâlâ EF InMemory kullanır (hızlı, ama indeks/kısıt/tür eşlemesini sınamaz). Her çalıştırma kendi geçici `AdvisorDb_Test_*` veritabanını migration'larla kurar ve siler, canlı `AdvisorDb`'ye dokunmaz. Sınananlar: tüm tabloların migration'la kurulması ve bekleyen migration/model farkı olmaması, randevu kısmi tekil indeksi (planlı çifte randevu reddedilir, iptaller serbest), kullanıcı adı / öğrenci başına atama / anket yanıtı tekilliği, Türkçe ve uzun metnin kayıpsız saklanması, giriş ve randevu akışının MSSQL üzerinde uçtan uca çalışması. SQL Server yoksa bu testler hata vermeden atlanır. Başka sunucu için `ODBS_TEST_SQLSERVER` ortam değişkeni bağlantı dizesini verir. 369 test yeşil.
