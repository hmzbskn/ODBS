# Design System Master — Öğrenci Danışman Otomasyonu

Yönerge: ui-ux-pro-max (Minimalism & Swiss Style, kurumsal/akademik). Variance 3, Motion 3, Density 6.
Önceki "kids/playful" (Baloo 2 + Comic Neue, teal) yönü ciddi bir kurum aracına uymadığı için değiştirildi.

## Renkler (token'lar frontend/src/index.css :root içinde)
| Rol | Açık | Koyu |
|---|---|---|
| Primary | #1E3A5F | #7AA7E6 |
| Grafik/Ring | #2563EB | #6EA0F0 |
| Accent | #A16207 | #F0B429 |
| Arka plan | #F8FAFC | #0B1424 |
| Yüzey | #FFFFFF | #111D33 |
| Metin | #0F172A | #E6EDF7 |
| Kenarlık | #D5DDE8 | #25364F |
| Kenar çubuğu | #14263F | #08101F |

## Tipografi
- Başlık / marka / istatistik sayıları: EB Garamond (600)
- Gövde ve tablolar: Inter 400/500/600, tabular-nums

## Kurallar
- Etkileşimli öğeler min. 40px yükseklik, 200ms geçişler, görünür focus halkası.
- prefers-reduced-motion saygı görür; yalnızca SVG ikonlar (emoji yok).
- Kontrast en az 4.5:1; renk tek başına anlam taşımaz (rozet + metin).
