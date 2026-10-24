/** GANO 0 ise öğrencinin henüz notlandırılmış dersi yoktur; "0.00" yerine tire gösterilir. */
export function formatGpa(gpa: number): string {
  return gpa > 0 ? gpa.toFixed(2) : '-'
}

/** "2026-10-24" -> "24.10.2026" */
export function formatDate(iso: string): string {
  const [y, m, d] = iso.split('-')
  return `${d}.${m}.${y}`
}

/** Puanlar tam sayıysa ondalıksız, değilse en fazla 2 basamakla gösterilir. */
export function formatScore(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.?0+$/, '')
}
