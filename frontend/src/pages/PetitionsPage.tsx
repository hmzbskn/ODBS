import { useAuth } from '../auth/AuthContext'
import { StaffPetitions } from '../components/StaffPetitions'
import { StudentPetitions } from '../components/StudentPetitions'

export function PetitionsPage() {
  const { user } = useAuth()
  if (!user) return null

  return (
    <>
      <h1>Talepler</h1>
      {user.role === 'Ogrenci' ? <StudentPetitions /> : <StaffPetitions />}
    </>
  )
}
