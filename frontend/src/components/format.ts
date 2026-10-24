/** GANO 0 ise öğrencinin henüz notlandırılmış dersi yoktur; "0.00" yerine tire gösterilir. */
export function formatGpa(gpa: number): string {
  return gpa > 0 ? gpa.toFixed(2) : '-'
}
