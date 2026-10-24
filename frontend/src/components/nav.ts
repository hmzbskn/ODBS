import type { Role } from '../api/types'

export interface NavItem {
  to: string
  label: string
  roles: Role[]
}

const STAFF: Role[] = ['Admin', 'Danisman']

export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Genel Bakış', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/kayitlar', label: 'Ders Kayıtları', roles: STAFF },
  { to: '/kayitlarim', label: 'Kayıt Durumum', roles: ['Ogrenci'] },
  { to: '/anketler', label: 'Anketler', roles: STAFF },
  { to: '/anketlerim', label: 'Anketlerim', roles: ['Ogrenci'] },
  { to: '/risk', label: 'Risk ve Mezuniyet', roles: STAFF },
  { to: '/mezuniyet', label: 'Mezuniyet Analizim', roles: ['Ogrenci'] },
  { to: '/randevular', label: 'Randevular', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/duyurular', label: 'Duyurular', roles: ['Admin', 'Danisman', 'Ogrenci'] },
  { to: '/talepler', label: 'Talepler', roles: ['Admin', 'Danisman', 'Ogrenci'] },
]

export const ROLE_LABELS: Record<Role, string> = {
  Admin: 'Yönetici',
  Danisman: 'Danışman',
  Ogrenci: 'Öğrenci',
}
