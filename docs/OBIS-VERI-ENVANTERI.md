# ODBS: OBİS'ten Çekilecek Veriler (Veri Envanteri)

Bu belge, Öğrenci - Danışman Bilgi Sistemi'nin (ODBS) OBİS veya OBİS API'sinden hangi verilere ihtiyaç duyduğunu, her verinin
hangi ekranda ve hangi kuralda kullanıldığını ve hangi rolün (öğrenci / danışman / yönetici) neyi gördüğünü listeler.
Kaynak: `IObisClient` arayüzü ve onu kullanan kod (güncel hâl, 2026-10-26).

## 1. Temel ilke

- ODBS, OBİS'e **yalnızca okuma** yapar; OBİS'e hiçbir şey yazmaz.
- Tüm OBİS erişimi tek arayüzden geçer (`IObisClient`). Gerçek API geldiğinde yalnızca bu arayüzün yeni bir uygulaması yazılır,
  kural motoru ve ekranlar değişmez.
- OBİS'te **olmayan** veriyi (randevu, duyuru, mesaj, dilekçe, anket, danışman kararı, yönetici ataması) ODBS kendi veritabanında tutar.
- Sistem şu iki girdiden çalışır: **(1) OBİS ders programı, (2) öğrencinin dönem başında seçtiği dersler.** Önkoşul ve sınav
  çakışması kontrolü yoktur.

## 2. OBİS'ten çekilen veri kümeleri

### 2.1 Öğrenci kaydı (`Students`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Öğrenci no | Her şeyin anahtarı; OBİS kayıtlarını ODBS hesabına bağlar | Tüm ekranlar, giriş eşleştirme |
| Ad, soyad | Görüntüleme | Tüm listeler, profil, üst çubuk |
| Sınıf (1-4) | Danışman-sınıf kuralı, filtreleme, toplu atama | Atama ekranı, öğrenci listesi, sınıfa göre toplu seçim |
| Bölüm | Bölüm Başkanı yetki sınırı, filtreleme | Atama ekranı, yönetici panoları |
| Danışman no (`AdvisorId`) | Öğrenci-danışman eşleşmesi; yönetici ataması bunu ezer | Danışmanın öğrenci listesi, öğrencinin "Danışmanım" kartı, erişim denetimi |
| GANO | Özet kartları, risk | Genel bakış, öğrenci profili, risk |
| Durum (aktif, dondurulmuş...) | Aktif olmayan öğrencileri ayırma | Öğrenci profili, listeler |
| E-posta | Danışmana Zimbra'dan yazmak için adres | Öğrenci genel bakışı (Danışmanım kartı), öğrenci profili |
| Telefon, ikamet | İletişim bilgisi | Öğrenci profili |
| **Öğretim türü** (normal / ikinci öğretim) | NÖ öğrencisi yalnızca NÖ veya NÖ/İÖ, İÖ öğrencisi yalnızca İÖ veya NÖ/İÖ şubesinden ders alabilir | Öğretim türü kuralı, alternatif şube önerisi, haftalık program, öğrenci profili |

### 2.2 Danışman kaydı (`Advisors`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Danışman no | Öğrenci eşleşmesi, giriş hesabı eşleşmesi | Atama, erişim denetimi |
| Ad, soyad, unvan | Görüntüleme | Danışmanım kartı, atama ekranı, randevu, mesajlaşma |
| (E-posta) | Zimbra'dan yazma | Şu an `adsoyad@selcuk.edu.tr` olarak **türetiliyor**; OBİS'ten gerçek adres gelirse türetme kalkar |

### 2.3 Ders kataloğu (`Courses`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Ders kodu, ad | Görüntüleme, eşleştirme | Ders kayıtları, transkript, risk, mezuniyet |
| AKTS | Dönem AKTS toplamı ve sınırı, mezuniyet ilerlemesi | Ders kayıtları (AKTS limiti), mezuniyet analizi |
| Kredi | GANO ağırlığı, seçmeli kredi toplamı | GANO, mezuniyet (seçmeli kredi) |
| Ders dönemi (1-8) | Alt/üst dönem ayrımı | Alttan ders önceliği uyarısı, çakışma önceliği |
| Zorunlu / seçmeli | Mezuniyet ve eksik ders kontrolü | Mezuniyet analizi, alttan ders uyarısı |

### 2.4 Ders programı (`Schedule`) — çakışma kontrolünün ana girdisi

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Ders kodu, **şube** | Hangi şubenin hangi saatte olduğu; alternatif şube önerisi | Çakışma kuralı |
| Gün, başlangıç, bitiş saati | Saat çakışmasını bulmak | Çakışma kuralı, haftalık program |
| Derslik | Görüntüleme | Haftalık program |
| Dönem | Dönem bazlı sorgu | Tüm kayıt doğrulama |

Dersin **tüm şubelerinin** programı gerekir; alternatif şube önerisi çekilen programdaki diğer şubelerden üretilir.
Bölümün gerçek programında şube = öğretim türü: "NÖ" (gündüz, 08:30-16:55), "İÖ" (akşam, 17:00-21:55, ayrı saatler) ve "NÖ/İÖ"
(İÖ'nün yalnızca 4. sınıfta sürdüğü için 1-3. sınıf derslerinin her iki öğretime açık tek gündüz şubesi). Sabit saati
olmayan derslerin (uygulama, mesleki eğitim, staj) program kaydı yoktur. Haftalık program ekranı bu veriyle öğrencinin derslerini
yeşil (çakışma yok), turuncu (çakışıyor, devam şartı yok), kırmızı (çakışma) ve gri (alınmadı) gösterir.

### 2.5 Öğrencinin dönem başı ders seçimi (`Registrations`) — çakışma kontrolünün diğer girdisi

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Öğrenci no, dönem | Hangi dönemin seçimi olduğu | Ders kayıtları listesi ve detayı |
| Ders kodu, **şube** | Seçilen ders ve şube | Çakışma kuralı, AKTS limiti, haftalık program |
| Onay durumu (Beklemede/Onaylandı...) | Hangi kaydın danışman onayını beklediği | Ders kayıtları listesi, güncel dönemin belirlenmesi |
| **Devam şartı var mı** (`AttendanceRequired`) | Devam şartı olmayan ders çakışma sayılmaz | Çakışma kuralı |

Not: "Devam şartı" bilgisinin gerçek OBİS'te hangi alandan geleceği belli değil (bkz. bölüm 6).

### 2.6 Transkript (`Transcript`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Dönem, ders kodu, ad | Geçmiş akademik kayıt | Transkript, öğrenci dosyası |
| Harf notu, geçti/kaldı | Alttan (kalınmış) ders tespiti, GANO | Çakışmada temel ders tespiti, alttan ders uyarısı, tekrar uyarısı, risk |
| Kredi, AKTS | GANO ağırlığı, kazanılan AKTS | GANO hesabı, mezuniyet |

### 2.7 Devamsızlık (`Attendance`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Öğrenci no, dönem, ders kodu | Ders bazlı devamsızlık | Risk analizi |
| Toplam oturum, katıldığı oturum | Devamsızlık oranı (%20 uyarı, %30 sınır) | Risk puanı, danışman risk listesi |

### 2.8 Müfredat (`Curriculum`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Zorunlu ders kodları | Eksik zorunlu ders tespiti | Mezuniyet analizi, alttan ders uyarısı |
| Toplam AKTS şartı | Mezuniyet ilerleme yüzdesi | Mezuniyet analizi |
| Seçmeli kredi şartı | Seçmeli eksik/tamam | Mezuniyet analizi |
| Staj gün şartı | Staj eksik/tamam | Mezuniyet analizi |

### 2.9 Staj (`Internship`)

| Alan | Neden | Nerede kullanılıyor |
| --- | --- | --- |
| Öğrenci no, tamamlanan staj günü | Staj şartı karşılaştırması | Mezuniyet analizi, danışman/yönetici mezuniyet listesi |

## 3. Rol bazında: kim hangi veriyi görüyor

### 3.1 Öğrenci (yalnızca kendi verisi)

| Ekran | Kullanılan OBİS verisi |
| --- | --- |
| Genel bakış | Öğrenci kaydı (dönem, GANO), seçilen dersler ve AKTS, danışman kaydı (ad, unvan, e-posta) |
| Kayıt durumum | Dönem başı ders seçimi, ders programı, kural bulguları (çakışma, AKTS limiti, alttan ders) |
| Mezuniyet | Transkript, müfredat, staj, AKTS |
| Randevu / mesaj / dilekçe / anket | Danışman bilgisi (kime gideceği); gerisi ODBS'nin kendi verisi |

### 3.2 Danışman (yalnızca kendisine atanmış öğrenciler)

| Ekran | Kullanılan OBİS verisi |
| --- | --- |
| Öğrenciler, öğrenci profili | Öğrenci kaydı (iletişim, okul, GANO), transkript (alınan/kalan AKTS) |
| Ders kayıtları | Her öğrencinin ders seçimi, ders programı, çakışma ve alternatif şube, AKTS limiti, alttan ders; onay/düzeltme kararı ODBS'ye yazılır |
| Risk ve mezuniyet | Transkript, devamsızlık, müfredat, staj |
| Randevu, duyuru, talepler, anket | Öğrenci kaydı (kime gönderileceği); içerik ODBS'nin kendi verisi |

### 3.3 Yönetici (Admin, Bölüm Başkanı, Dekan)

| Ekran | Kullanılan OBİS verisi |
| --- | --- |
| Danışman atamaları | Tüm öğrenciler (sınıf, bölüm), tüm danışmanlar; atama ODBS'de OBİS'in danışman bilgisini ezen kayıt olarak tutulur |
| Danışman karşılaştırma tablosu | Öğrenci kaydı, danışman kaydı, risk/mezuniyet özetleri |
| Tüm öğrenciler, ders kayıtları, risk | Danışmanın gördüğünün tamamı, kapsam bölüm/fakülteye göre |

## 4. ODBS'nin kendi tuttuğu veri (OBİS'ten çekilmez)

Kullanıcı hesapları ve rolleri, danışman kararları (onay / düzeltme isteği), yönetici danışman atamaları, randevular, mesajlar,
duyurular, dilekçe/talepler, anketler ve yanıtları, bildirimler.

## 5. Gerekmeyen veriler (kaldırıldı)

- **Ders önkoşulları**: sistem ders bağımlılığı kontrol etmez.
- **Sınav programı** (ara sınav/final tarihleri): sınav çakışması kontrolü yoktur.

## 6. Hocaya sorulacak / netleşmesi gereken noktalar

1. **Erişim yöntemi:** OBİS'in bir REST/SOAP servisi var mı, yoksa veritabanı görünümü/dosya aktarımı mı? Kimlik doğrulama (API anahtarı, OAuth, kurumsal SSO)?
2. **Giriş:** Kullanıcılar ODBS'ye OBİS/kurumsal hesaplarıyla mı girecek? Rol (öğrenci/danışman/yönetici) OBİS'ten mi gelecek?
3. **Devam şartı:** OBİS'te "bu derste devam şartı aranır/aranmaz" bilgisi hangi alanda tutuluyor (kayıt satırında mı, önceki devam durumundan mı türetiliyor)?
4. **Alttan ders tespiti:** OBİS'te alttan alınan ders ayrı işaretli mi, yoksa transkriptten (kalınmış ders) mi çıkarılacak?
5. **Dönem başı ders seçimi:** Öğrencinin seçtiği ama henüz onaylanmamış dersler OBİS'ten okunabiliyor mu, hangi aşamadan itibaren?
6. **Tüm şubeler:** Bir dersin açık tüm şubelerinin saatleri (alternatif şube önerisi için) alınabiliyor mu?
7. **E-posta:** Öğrenci ve danışman e-posta adresleri OBİS'te var mı, yoksa `adsoyad@selcuk.edu.tr` / `adsoyad@ogr.selcuk.edu.tr` kuralı mı kullanılacak?
8. **Müfredat ve staj:** Zorunlu ders listesi, AKTS/seçmeli kredi/staj şartı ve tamamlanan staj günü OBİS'ten alınabiliyor mu?
9. **Danışman-öğrenci eşleşmesi:** "Her danışman tek sınıfa bakar" kuralı OBİS'te zaten var mı, yoksa ODBS mi uygulayacak?
10. **Güncelleme sıklığı ve KVKK:** Veri anlık mı çekilecek, önbelleğe alınacak mı; öğrenci verisinin ODBS'de saklanma/yetki sınırları.
