import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { INBOX_CHANGED } from './inboxEvents'
import { NAV_ITEMS, ROLE_LABELS } from './nav'

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
            D
          </span>
          <span>Danışman Otomasyonu</span>
        </div>
        <nav>
          <ul className="nav-list">
            {items.map((item) => (
              <li key={item.to}>
                <NavLink to={item.to} end={item.to === '/'} className="nav-link" onClick={() => setMenuOpen(false)}>
                  <span>{item.label}</span>
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
          <button
            type="button"
            className="btn btn-icon menu-toggle"
            aria-label="Menüyü aç/kapat"
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((open) => !open)}
          >
            ☰
          </button>
          <div className="topbar-user">
            <div className="user-meta">
              <strong>{user.displayName}</strong>
              <span className="badge">{ROLE_LABELS[user.role]}</span>
            </div>
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
