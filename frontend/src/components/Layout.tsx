import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { NAV_ITEMS, ROLE_LABELS } from './nav'

export function Layout() {
  const { user, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)

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
                  {item.soonPhase && <span className="badge badge-muted">Yakında</span>}
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
