import { useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Appointment, Availability } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { formatDateLong } from './format'
import { APPOINTMENT_STATUS_LABELS } from './labels'
import { useAsync } from './useAsync'

/** Öğrenci: kendi danışmanının boş saatlerinden randevu alır, randevularını görür ve iptal eder. */
export function StudentBooking() {
  const availability = useAsync(() => api.get<Availability>('/api/appointments/availability'), [])
  const mine = useAsync(() => api.get<Appointment[]>('/api/appointments'), [])
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  async function run(action: () => Promise<unknown>, success: string) {
    setMessage(null)
    try {
      await action()
      setMessage({ text: success, ok: true })
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'İşlem başarısız oldu.', ok: false })
    }
    availability.retry()
    mine.retry()
  }

  return (
    <>
      {message && (
        <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </p>
      )}

      <section className="card">
        <h2>Randevularım</h2>
        {mine.state.status === 'loading' && <Loading />}
        {mine.state.status === 'error' && <ErrorState message={mine.state.message} onRetry={mine.retry} />}
        {mine.state.status === 'ready' &&
          (mine.state.data.length === 0 ? (
            <p className="muted">Henüz randevunuz yok.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Saat</th>
                    <th>Durum</th>
                    <th aria-label="İşlem" />
                  </tr>
                </thead>
                <tbody>
                  {mine.state.data.map((a) => (
                    <tr key={a.id}>
                      <td>{formatDateLong(a.date)}</td>
                      <td>
                        {a.startTime} - {a.endTime}
                      </td>
                      <td className="wrap-cell">
                        {APPOINTMENT_STATUS_LABELS[a.status]}
                        {a.cancellationReason && <span className="muted"> · {a.cancellationReason}</span>}
                      </td>
                      <td>
                        {a.status === 'Scheduled' && !a.isPast && (
                          <button
                            type="button"
                            className="btn btn-small"
                            onClick={() => void run(() => api.post(`/api/appointments/${a.id}/cancel`, {}), 'Randevunuz iptal edildi.')}
                          >
                            İptal et
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
      </section>

      <section className="card">
        <h2>Randevu al</h2>
        {availability.state.status === 'loading' && <Loading />}
        {availability.state.status === 'error' && <ErrorState message={availability.state.message} onRetry={availability.retry} />}
        {availability.state.status === 'ready' && <Slots data={availability.state.data} onBook={(slotId, date) => void run(() => api.post('/api/appointments/book', { slotId, date }), 'Randevunuz oluşturuldu.')} />}
      </section>
    </>
  )
}

function Slots({ data, onBook }: { data: Availability; onBook: (slotId: string, date: string) => void }) {
  const full = data.activeAppointments >= data.maxActiveAppointments
  const byDate = new Map<string, Availability['slots']>()
  for (const s of data.slots) byDate.set(s.date, [...(byDate.get(s.date) ?? []), s])

  return (
    <>
      <p className="muted">
        Danışmanınız: <strong>{data.advisorName}</strong> · Aktif randevu: {data.activeAppointments} / {data.maxActiveAppointments}
        {full && ' (yeni randevu için önce birini iptal edin)'}
      </p>

      {byDate.size === 0 ? (
        <EmptyState title="Şu an alınabilecek randevu saati yok">Danışmanınız görüşme saati tanımladığında burada görünür.</EmptyState>
      ) : (
        [...byDate.entries()].map(([date, slots]) => (
          <div className="day-group" key={date}>
            <h3>{formatDateLong(date)}</h3>
            {slots.map((s) => (
              <div className={`slot-row${!s.available && !s.bookedByMe ? ' taken' : ''}`} key={`${s.slotId}-${s.date}`}>
                <span>
                  {s.startTime} - {s.endTime}
                </span>
                {s.bookedByMe ? (
                  <span className="badge badge-ok">Randevunuz</span>
                ) : s.available ? (
                  <button type="button" className="btn btn-small btn-primary" disabled={full} onClick={() => onBook(s.slotId, s.date)}>
                    Randevu al
                  </button>
                ) : (
                  <span className="badge">Dolu</span>
                )}
              </div>
            ))}
          </div>
        ))
      )}
    </>
  )
}
