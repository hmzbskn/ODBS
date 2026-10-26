import { useState, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const DEMO_ACCOUNTS = [
  { username: 'admin', label: 'Yönetici' },
  { username: 'danisman1', label: 'Danışman' },
  { username: 'ogrenci2', label: 'Öğrenci' },
]

export function LoginPage() {
  const { user, login } = useAuth()
  const location = useLocation()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (user) {
    const from = (location.state as { from?: string } | null)?.from
    return <Navigate to={from ?? '/'} replace />
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(username.trim(), password)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Giriş yapılamadı. Lütfen tekrar deneyin.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login-page login-split">
      <aside className="login-aside">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            O
          </span>
          <span>Öğrenci - Danışman Bilgi Sistemi (ODBS)</span>
        </div>
        <h2 className="login-tagline">Öğrenci danışmanlığını tek ekrandan yönetin.</h2>
        <ul className="login-points">
          <li>Ders kaydı kurallarını otomatik doğrulayın</li>
          <li>Riskli öğrencileri erken görün</li>
          <li>Randevu, duyuru ve talepleri takip edin</li>
        </ul>
      </aside>

      <form className="card login-card" onSubmit={onSubmit}>
        <div>
          <h1>Giriş yap</h1>
          <p className="muted login-sub">Hesabınızla devam edin.</p>
        </div>

        <label className="field">
          <span>Kullanıcı adı</span>
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </label>
        <label className="field">
          <span>Şifre</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
          {busy ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </button>

        <div className="demo-box">
          <p className="muted">Demo hesaplar (şifre: Demo123!)</p>
          <div className="demo-list">
            {DEMO_ACCOUNTS.map((a) => (
              <button
                key={a.username}
                type="button"
                className="btn btn-small"
                onClick={() => {
                  setUsername(a.username)
                  setPassword('Demo123!')
                }}
              >
                {a.label}
              </button>
            ))}
          </div>
        </div>
      </form>
    </div>
  )
}
