import type { Role } from '../api/types'

export interface NavItem {
  to: string
  label: string
  icon: string
  roles: Role[]
}

const MANAGEMENT: Role[] = ['Admin', 'BolumBaskani', 'Dekan']
const STAFF: Role[] = ['Admin', 'Danisman']
// Risk ve mezuniyet analizini danışmanlar ve onların üstündeki yönetim katmanı görür.
export const ANALYTICS_ROLES: Role[] = ['Admin', 'Danisman', 'BolumBaskani', 'Dekan']
export const MANAGEMENT_ROLES = MANAGEMENT

export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Genel Bakış', icon: 'dashboard', roles: ['Admin', 'Danisman', 'Ogrenci', 'BolumBaskani', 'Dekan'] },
  { to: '/atamalar', label: 'Danışman Atamaları', icon: 'users', roles: MANAGEMENT },
  { to: '/ders-programi', label: 'Ders Programı', icon: 'schedule', roles: MANAGEMENT },
  { to: '/ogrenciler', label: 'Öğrenciler', icon: 'users', roles: ANALYTICS_ROLES },
  { to: '/kayitlar', label: 'Ders Kayıtları', icon: 'clipboard', roles: STAFF },
  { to: '/kayitlarim', label: 'Kayıt Durumum', icon: 'clipboard', roles: ['Ogrenci'] },
  { to: '/anketler', label: 'Anketler', icon: 'survey', roles: STAFF },
  { to: '/anketlerim', label: 'Anketlerim', icon: 'survey', roles: ['Ogrenci'] },
  { to: '/risk', label: 'Risk ve Mezuniyet', icon: 'alert', roles: ANALYTICS_ROLES },
  { to: '/mezuniyet', label: 'Mezuniyet Analizim', icon: 'cap', roles: ['Ogrenci'] },
  { to: '/randevular', label: 'Randevular', icon: 'calendar', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/duyurular', label: 'Duyurular', icon: 'megaphone', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/talepler', label: 'Talepler', icon: 'file', roles: ['Admin', 'Danisman', 'Ogrenci'] },
]

export const ROLE_LABELS: Record<Role, string> = {
  Admin: 'Yönetici',
  Danisman: 'Danışman',
  Ogrenci: 'Öğrenci',
  BolumBaskani: 'Bölüm Başkanı',
  Dekan: 'Dekan',
}
