import { useAuth } from '../auth/AuthContext'
import { InboxView } from '../components/InboxView'
import { StaffAnnouncements } from '../components/StaffAnnouncements'

/** Danışman/yönetici: filtreli duyuru gönderir. Öğrenci: bildirim kutusunu görür. */
export function AnnouncementsPage() {
  const { user } = useAuth()
  if (!user) return null

  return (
    <>
      <h1>{user.role === 'Ogrenci' ? 'Duyurular ve Bildirimler' : 'Duyurular'}</h1>
      {user.role === 'Ogrenci' ? <InboxView /> : <StaffAnnouncements />}
    </>
  )
}
