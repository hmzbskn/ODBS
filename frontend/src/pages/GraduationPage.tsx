import { useAuth } from '../auth/AuthContext'
import { EmptyState } from '../components/Feedback'
import { GraduationView } from '../components/GraduationView'

/** Öğrencinin kendi mezuniyet analizi. */
export function GraduationPage() {
  const { user } = useAuth()

  return (
    <>
      <h1>Mezuniyet Analizim</h1>
      {user?.linkedStudentNo ? (
        <GraduationView studentNo={user.linkedStudentNo} />
      ) : (
        <EmptyState title="Öğrenci bulunamadı">Hesabınıza bağlı bir öğrenci numarası yok.</EmptyState>
      )}
    </>
  )
}
