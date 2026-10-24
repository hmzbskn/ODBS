import type { DayName, WeeklySlot } from '../api/types'
import { DAY_LABELS } from './labels'

const WEEK_DAYS: DayName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']

/** Haftalık ders programı: satırlar ders başlangıç saatleri, sütunlar günler. Çakışan dersler kırmızı. */
export function WeeklySchedule({ slots }: { slots: WeeklySlot[] }) {
  if (slots.length === 0) {
    return <p className="muted">Bu dönem için programda yer alan ders yok.</p>
  }

  const days = [...WEEK_DAYS, ...(['Saturday', 'Sunday'] as DayName[]).filter((d) => slots.some((s) => s.day === d))]
  const times = [...new Set(slots.map((s) => s.startTime))].sort()

  return (
    <div className="table-wrap">
      <table className="schedule" aria-label="Haftalık ders programı">
        <thead>
          <tr>
            <th scope="col">Saat</th>
            {days.map((d) => (
              <th key={d} scope="col">
                {DAY_LABELS[d]}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {times.map((time) => (
            <tr key={time}>
              <th scope="row">{time}</th>
              {days.map((day) => {
                const cell = slots.filter((s) => s.day === day && s.startTime === time)
                return (
                  <td key={day}>
                    {cell.map((s) => (
                      <div
                        key={`${s.courseCode}-${s.section}`}
                        className={`slot${s.inConflict ? ' slot-conflict' : ''}`}
                        title={s.inConflict ? 'Çakışıyor' : undefined}
                      >
                        <strong>{s.courseCode}</strong> <span className="muted">{s.section}</span>
                        <div className="slot-meta">
                          {s.startTime}-{s.endTime} · {s.room}
                        </div>
                        {s.inConflict && <span className="badge badge-danger">Çakışma</span>}
                      </div>
                    ))}
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
