import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { Appointment, DayName, Slot } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { formatDateLong } from './format'
import { APPOINTMENT_STATUS_LABELS, DAY_LABELS } from './labels'
import { useAsync } from './useAsync'

const WEEKDAYS: DayName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']

/** Danışman: görüşme saatlerini tanımlar, randevuları görür, iptal eder ya da tamamlandı işaretler. */
export function StaffAppointments({ canDefineSlots }: { canDefineSlots: boolean }) {
  const slots = useAsync(() => api.get<Slot[]>('/api/appointments/slots'), [])
  const appointments = useAsync(() => api.get<Appointment[]>('/api/appointments'), [])
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  const reload = () => {
    slots.retry()
    appointments.retry()
  }

  async function run(action: () => Promise<unknown>, success: string) {
    setMessage(null)
    try {
      await action()
      setMessage({ text: success, ok: true })
      reload()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'İşlem başarısız oldu.', ok: false })
    }
  }

  return (
    <>
      {message && (
        <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </p>
      )}

      <section className="card">
        <h2>Görüşme saatlerim</h2>
        <p className="muted">
          Haftalık tekrar eden saatlerdir; öğrenciler önümüzdeki 4 hafta için bu saatlerden randevu alır.
        </p>
        {canDefineSlots && <SlotForm onCreate={(body) => run(() => api.post('/api/appointments/slots', body), 'Görüşme saati eklendi.')} />}
        <SlotList state={slots.state} retry={slots.retry} onDelete={(id) => run(() => api.delete(`/api/appointments/slots/${id}`), 'Görüşme saati kaldırıldı.')} canEdit={canDefineSlots} />
      </section>

      <AppointmentTables
        state={appointments.state}
        retry={appointments.retry}
        onCancel={(id, reason) => run(() => api.post(`/api/appointments/${id}/cancel`, { reason }), 'Randevu iptal edildi; öğrenciye bildirim gönderildi.')}
        onComplete={(id) => run(() => api.post(`/api/appointments/${id}/complete`), 'Randevu tamamlandı olarak işaretlendi.')}
      />
    </>
  )
}

function SlotForm({ onCreate }: { onCreate: (body: { day: DayName; startTime: string; endTime: string }) => Promise<void> }) {
  const [day, setDay] = useState<DayName>('Monday')
  const [start, setStart] = useState('10:00')
  const [end, setEnd] = useState('10:30')
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    await onCreate({ day, startTime: start, endTime: end })
    setBusy(false)
  }

  return (
    <form className="slot-form" onSubmit={submit}>
      <label className="field">
        <span>Gün</span>
        <select value={day} onChange={(e) => setDay(e.target.value as DayName)}>
          {WEEKDAYS.map((d) => (
            <option key={d} value={d}>
              {DAY_LABELS[d]}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>Başlangıç</span>
        <input type="time" value={start} onChange={(e) => setStart(e.target.value)} step={300} required />
      </label>
      <label className="field">
        <span>Bitiş</span>
        <input type="time" value={end} onChange={(e) => setEnd(e.target.value)} step={300} required />
      </label>
      <button type="submit" className="btn btn-primary" disabled={busy}>
        Saat ekle
      </button>
    </form>
  )
}

function SlotList({
  state, retry, onDelete, canEdit,
}: {
  state: ReturnType<typeof useAsync<Slot[]>>['state']
  retry: () => void
  onDelete: (id: string) => void
  canEdit: boolean
}) {
  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />
  if (state.data.length === 0) return <EmptyState title="Henüz görüşme saati tanımlamadınız">Yukarıdaki formdan ekleyin.</EmptyState>

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Gün</th>
            <th>Saat</th>
            <th>Gelecek randevu</th>
            <th aria-label="İşlem" />
          </tr>
        </thead>
        <tbody>
          {state.data.map((s) => (
            <tr key={s.id}>
              <td>{DAY_LABELS[s.day]}</td>
              <td>
                {s.startTime} - {s.endTime}
              </td>
              <td>{s.upcomingBookings}</td>
              <td>
                {canEdit && (
                  <button type="button" className="btn btn-small" onClick={() => onDelete(s.id)}>
                    Kaldır
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function AppointmentTables({
  state, retry, onCancel, onComplete,
}: {
  state: ReturnType<typeof useAsync<Appointment[]>>['state']
  retry: () => void
  onCancel: (id: string, reason: string) => void
  onComplete: (id: string) => void
}) {
  const [cancelId, setCancelId] = useState<string | null>(null)
  const [reason, setReason] = useState('')

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const upcoming = state.data.filter((a) => a.status === 'Scheduled' && !a.isPast).reverse()
  const rest = state.data.filter((a) => !(a.status === 'Scheduled' && !a.isPast))

  return (
    <>
      <section className="card">
        <h2>Yaklaşan randevular</h2>
        {upcoming.length === 0 ? (
          <p className="muted">Yaklaşan randevu yok.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th>Saat</th>
                  <th>Öğrenci</th>
                  <th aria-label="İşlem" />
                </tr>
              </thead>
              <tbody>
                {upcoming.map((a) => (
                  <tr key={a.id}>
                    <td>{formatDateLong(a.date)}</td>
                    <td>
                      {a.startTime} - {a.endTime}
                    </td>
                    <td>
                      <Link to={`/ogrenci/${a.studentNo}`}>{a.studentName}</Link> <span className="muted">({a.studentNo})</span>
                    </td>
                    <td>
                      {cancelId === a.id ? (
                        <div className="inline-cancel">
                          <input
                            aria-label="İptal gerekçesi"
                            placeholder="Gerekçe (öğrenciye iletilir)"
                            value={reason}
                            maxLength={300}
                            onChange={(e) => setReason(e.target.value)}
                          />
                          <button
                            type="button"
                            className="btn btn-small btn-danger"
                            onClick={() => {
                              onCancel(a.id, reason)
                              setCancelId(null)
                              setReason('')
                            }}
                          >
                            İptal et
                          </button>
                          <button type="button" className="btn btn-small" onClick={() => setCancelId(null)}>
                            Vazgeç
                          </button>
                        </div>
                      ) : (
                        <button type="button" className="btn btn-small" onClick={() => setCancelId(a.id)}>
                          İptal
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="card">
        <h2>Geçmiş ve iptal edilenler</h2>
        {rest.length === 0 ? (
          <p className="muted">Kayıt yok.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th>Saat</th>
                  <th>Öğrenci</th>
                  <th>Durum</th>
                  <th aria-label="İşlem" />
                </tr>
              </thead>
              <tbody>
                {rest.map((a) => (
                  <tr key={a.id}>
                    <td>{formatDateLong(a.date)}</td>
                    <td>
                      {a.startTime} - {a.endTime}
                    </td>
                    <td>
                      <Link to={`/ogrenci/${a.studentNo}`}>{a.studentName}</Link>
                    </td>
                    <td className="wrap-cell">
                      {APPOINTMENT_STATUS_LABELS[a.status]}
                      {a.cancellationReason && <span className="muted"> · {a.cancellationReason}</span>}
                    </td>
                    <td>
                      {a.status === 'Scheduled' && a.isPast && (
                        <button type="button" className="btn btn-small btn-primary" onClick={() => onComplete(a.id)}>
                          Tamamlandı
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
