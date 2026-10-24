import { useAuth } from '../auth/AuthContext'
import { StaffAppointments } from '../components/StaffAppointments'
import { StudentBooking } from '../components/StudentBooking'

export function AppointmentsPage() {
  const { user } = useAuth()
  if (!user) return null

  return (
    <>
      <h1>Randevular</h1>
      {user.role === 'Ogrenci' ? <StudentBooking /> : <StaffAppointments canDefineSlots={user.role === 'Danisman'} />}
    </>
  )
}
