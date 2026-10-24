import { useLocation } from 'react-router-dom'
import { NAV_ITEMS } from '../components/nav'
import { EmptyState } from '../components/Feedback'

/** Henüz geliştirilmemiş modüller için yer tutucu; hangi aşamada geleceğini söyler. */
export function ComingSoonPage() {
  const { pathname } = useLocation()
  const item = NAV_ITEMS.find((n) => n.to === pathname)

  return (
    <>
      <h1>{item?.label ?? 'Sayfa'}</h1>
      <div className="card">
        <EmptyState title="Bu modül henüz hazır değil">
          {item?.soonPhase ? `Geliştirme planında ${item.soonPhase}. aşamada eklenecek.` : 'Yakında eklenecek.'}
        </EmptyState>
      </div>
    </>
  )
}
