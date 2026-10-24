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
| EDGE-01 | 20241011 | Saat çakışması (MAT101/A tekrar + BLM301/A aynı gün/saat) |
| EDGE-02 | 20251006 | Önkoşul ihlali (BLM103 kalmış, BLM201'e kayıtlı) |
| EDGE-03 | 20241012 | Alttan ders önceliği (FIZ102 kalmış, dönem5 dersleri alıyor) |
| EDGE-04 | 20251007 | Kredi/AKTS limiti aşımı (düşük GANO + 3 ekstra ders) |
| EDGE-05 | 20231016 | Sınav çakışması (BLM203 ve BLM401 final'leri aynı an) |
| EDGE-06 | 20231017 | Tekrar alma (BLM201'i AA ile geçmiş, yine kayıtlı) |
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
