import type { Role } from '../api/types'

export interface NavItem {
  to: string
  label: string
  roles: Role[]
  /** Modülün geliştirileceği aşama; doluysa menüde "Yakında" etiketi çıkar. */
  soonPhase?: number
}

const STAFF: Role[] = ['Admin', 'Danisman']

export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Genel Bakış', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/kayitlar', label: 'Ders Kayıtları', roles: STAFF, soonPhase: 7 },
  { to: '/kayitlarim', label: 'Kayıt Durumum', roles: ['Ogrenci'], soonPhase: 7 },
  { to: '/anketler', label: 'Anketler', roles: STAFF, soonPhase: 8 },
  { to: '/anketlerim', label: 'Anketlerim', roles: ['Ogrenci'], soonPhase: 8 },
  { to: '/risk', label: 'Risk ve Mezuniyet', roles: STAFF, soonPhase: 9 },
  { to: '/mezuniyet', label: 'Mezuniyet Analizim', roles: ['Ogrenci'], soonPhase: 9 },
  { to: '/randevular', label: 'Randevular', roles: ['Admin', 'Danisman', 'Ogrenci'], soonPhase: 10 },
  { to: '/duyurular', label: 'Duyurular', roles: ['Admin', 'Danisman', 'Ogrenci'], soonPhase: 10 },
  { to: '/talepler', label: 'Talepler', roles: ['Admin', 'Danisman', 'Ogrenci'], soonPhase: 10 },
]

export const ROLE_LABELS: Record<Role, string> = {
  Admin: 'Yönetici',
  Danisman: 'Danışman',
  Ogrenci: 'Öğrenci',
}
