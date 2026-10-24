import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Layout } from './components/Layout'
import { NAV_ITEMS } from './components/nav'
import { RequireAuth } from './components/RequireAuth'
import { ComingSoonPage } from './pages/ComingSoonPage'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { GraduationPage } from './pages/GraduationPage'
import { RegistrationDetailPage } from './pages/RegistrationDetailPage'
import { RegistrationsPage } from './pages/RegistrationsPage'
import { RiskDetailPage } from './pages/RiskDetailPage'
import { RiskPage } from './pages/RiskPage'
import { StudentSurveyFormPage } from './pages/StudentSurveyFormPage'
import { StudentSurveysPage } from './pages/StudentSurveysPage'
import { SurveyBuilderPage } from './pages/SurveyBuilderPage'
import { SurveyDetailPage } from './pages/SurveyDetailPage'
import { SurveysPage } from './pages/SurveysPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/giris" element={<LoginPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route element={<RequireAuth roles={['Admin', 'Danisman']} />}>
                <Route path="/kayitlar" element={<RegistrationsPage />} />
                <Route path="/kayitlar/:studentNo" element={<RegistrationDetailPage />} />
                <Route path="/risk" element={<RiskPage />} />
                <Route path="/risk/:studentNo" element={<RiskDetailPage />} />
                <Route path="/anketler" element={<SurveysPage />} />
                <Route path="/anketler/yeni" element={<SurveyBuilderPage />} />
                <Route path="/anketler/:id" element={<SurveyDetailPage />} />
              </Route>
              <Route element={<RequireAuth roles={['Ogrenci']} />}>
                <Route path="/kayitlarim" element={<RegistrationDetailPage />} />
                <Route path="/mezuniyet" element={<GraduationPage />} />
                <Route path="/anketlerim" element={<StudentSurveysPage />} />
                <Route path="/anketlerim/:id" element={<StudentSurveyFormPage />} />
              </Route>
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
