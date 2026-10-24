import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Layout } from './components/Layout'
import { NAV_ITEMS } from './components/nav'
import { RequireAuth } from './components/RequireAuth'
import { ComingSoonPage } from './pages/ComingSoonPage'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/giris" element={<LoginPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              {/* Yer tutucu sayfalar: modül hazır oldukça kendi route'larıyla değiştirilir. */}
              {NAV_ITEMS.filter((n) => n.soonPhase).map((n) => (
                <Route key={n.to} element={<RequireAuth roles={n.roles} />}>
                  <Route path={n.to} element={<ComingSoonPage />} />
                </Route>
              ))}
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
