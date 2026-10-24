# Mimari

> Bu dosya proje tamamlanınca (Aşama 12) ayrıntılı ve gerekçeli şekilde yazılacak;
> bitirme raporunun teknik bölümüne temel oluşturacak. Şimdilik iskelet.

## Katmanlar

- **Advisor.Domain** — Entity'ler, value object'ler, domain'e özgü enum'lar. Hiçbir
  dış bağımlılığı yok (EF Core dahil).
- **Advisor.Application** — Arayüzler (`IObisClient`, `IRegistrationRule`, ...), DTO'lar,
  servis sözleşmeleri. Domain'e bağımlı, Infrastructure'a bağımlı değil (bağımlılık
  yönü içe doğru).
- **Advisor.Infrastructure** — EF Core `DbContext`, repository/servis implementasyonları,
  `HttpObisClient`, kural motoru implementasyonları. Application + Domain'e bağımlı.
- **Advisor.Api** — ASP.NET Core Web API (controller tabanlı), JWT auth, Swagger,
  DI kompozisyonu. Infrastructure + Application + Domain'e bağımlı.
- **Obis.MockApi** — Bağımsız Minimal API projesi, gerçek OBİS'i taklit eder. Ana
  uygulamaya hiçbir şekilde bağımlı değil; ana uygulama ona yalnızca HTTP üzerinden
  `IObisClient` adaptörüyle bağlanır.

## Adapter deseni

Ana uygulama OBİS'e `IObisClient` arayüzü üzerinden bağlanır. Şu an tek implementasyon
`HttpObisClient` (Obis.MockApi'ye HTTP istekleri atar). Gerçek OBİS API'si geldiğinde
yapılacak iş: yeni bir `IObisClient` implementasyonu yazmak ve DI kaydını değiştirmek —
`Advisor.Application`, `Advisor.Domain` ve `Advisor.Api`'nin hiçbir satırı değişmez.

## Kural motoru

(Aşama 5'te doldurulacak.)

## Veri akışı

(Aşama 12'de Mermaid diyagramıyla doldurulacak.)
