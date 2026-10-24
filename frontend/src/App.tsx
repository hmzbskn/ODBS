import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Layout } from './components/Layout'
import { RequireAuth } from './components/RequireAuth'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { AnnouncementsPage } from './pages/AnnouncementsPage'
import { AppointmentsPage } from './pages/AppointmentsPage'
import { GraduationPage } from './pages/GraduationPage'
import { PetitionsPage } from './pages/PetitionsPage'
import { RegistrationDetailPage } from './pages/RegistrationDetailPage'
import { RegistrationsPage } from './pages/RegistrationsPage'
import { RiskDetailPage } from './pages/RiskDetailPage'
import { RiskPage } from './pages/RiskPage'
import { StudentFilePage } from './pages/StudentFilePage'
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
              <Route path="/randevular" element={<AppointmentsPage />} />
              <Route path="/duyurular" element={<AnnouncementsPage />} />
              <Route path="/talepler" element={<PetitionsPage />} />
              <Route element={<RequireAuth roles={['Admin', 'Danisman']} />}>
                <Route path="/ogrenci/:studentNo" element={<StudentFilePage />} />
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
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
