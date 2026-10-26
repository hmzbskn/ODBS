import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { INBOX_CHANGED } from './inboxEvents'
import { NAV_ITEMS, ROLE_LABELS } from './nav'

// Lucide tarzı çizgi ikonlar (24x24, stroke).
const ICONS: Record<string, string[]> = {
  dashboard: ['M3 3h7v9H3z', 'M14 3h7v5h-7z', 'M14 12h7v9h-7z', 'M3 16h7v5H3z'],
  clipboard: ['M9 2h6v4H9z', 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2', 'm9 14 2 2 4-4'],
  survey: ['M9 2h6v4H9z', 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2', 'M9 12h6', 'M9 16h6'],
  alert: ['m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3', 'M12 9v4', 'M12 17h.01'],
  cap: ['M21.42 10.92a1 1 0 0 0 0-1.84L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.83l8.57 3.91a2 2 0 0 0 1.66 0z', 'M22 10v6', 'M6 12.5V16a6 3 0 0 0 12 0v-3.5'],
  calendar: ['M8 2v4', 'M16 2v4', 'M3 6h18v16H3z', 'M3 10h18'],
  megaphone: ['m3 11 18-5v12L3 14z', 'M11.6 16.8a3 3 0 1 1-5.8-1.6'],
  users: ['M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2', 'M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M22 21v-2a4 4 0 0 0-3-3.87', 'M16 3.13a4 4 0 0 1 0 7.75'],
  file: ['M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7z', 'M14 2v4a2 2 0 0 0 2 2h4', 'M16 13H8', 'M16 17H8'],
}

function NavIcon({ name }: { name: string }) {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" aria-hidden="true">
      {(ICONS[name] ?? []).map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  )
}

export function Layout() {
  const { user, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const [unread, setUnread] = useState(0)
  const location = useLocation()
  const isStudent = user?.role === 'Ogrenci'

  // Öğrencinin okunmamış bildirim sayısı: sayfa değişince ve okundu durumu değişince yenilenir.
  useEffect(() => {
    if (!isStudent) return
    let cancelled = false
    const load = () =>
      api
        .get<{ count: number }>('/api/inbox/unread-count')
        .then((r) => !cancelled && setUnread(r.count))
        .catch(() => undefined)
    void load()
    window.addEventListener(INBOX_CHANGED, load)
    return () => {
      cancelled = true
      window.removeEventListener(INBOX_CHANGED, load)
    }
  }, [isStudent, location.pathname])

  if (!user) return null
  const items = NAV_ITEMS.filter((item) => item.roles.includes(user.role))

  return (
    <div className="shell">
      <aside className={`sidebar${menuOpen ? ' open' : ''}`} aria-label="Ana menü">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            O
          </span>
          <span title="Öğrenci Danışman Bilgi Sistemi">ODBS</span>
        </div>
        <nav>
          <ul className="nav-list">
            {items.map((item) => (
              <li key={item.to}>
                <NavLink to={item.to} end={item.to === '/'} className="nav-link" onClick={() => setMenuOpen(false)}>
                  <span className="nav-label">
                    <NavIcon name={item.icon} />
                    {item.label}
                  </span>
                  {item.to === '/duyurular' && unread > 0 && (
                    <span className="badge badge-count" aria-label={`${unread} okunmamış`}>
                      {unread}
                    </span>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </aside>
      {menuOpen && <div className="scrim" onClick={() => setMenuOpen(false)} aria-hidden="true" />}

      <div className="main">
        <header className="topbar">
          <div className="topbar-left">
            <button
              type="button"
              className="btn btn-icon menu-toggle"
              aria-label="Menüyü aç/kapat"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((open) => !open)}
            >
              <svg className="nav-icon" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
            <div className="user-meta">
              <span className="avatar" aria-hidden="true">
                {user.displayName.replace(/^(Dr\.|Doç\.|Prof\.|Öğr\.|Üyesi|\s)+/g, '').charAt(0)}
              </span>
              <strong>{user.displayName}</strong>
            </div>
          </div>
          <span className="badge topbar-role">{ROLE_LABELS[user.role]}</span>
          <div className="topbar-right">
            <button type="button" className="btn" onClick={logout}>
              Çıkış
            </button>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
