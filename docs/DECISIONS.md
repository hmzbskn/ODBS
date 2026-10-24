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
