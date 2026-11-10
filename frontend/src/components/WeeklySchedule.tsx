import type { DayName, SlotStatus, WeeklySlot } from '../api/types'
import { DAY_LABELS } from './labels'

const WEEK_DAYS: DayName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']

/** Bölüm programının ders saatleri (45 dk ders + ara); ızgara çizgileri ve sol sütun etiketleri. */
export const PERIODS = ['08:30', '09:25', '10:20', '11:15', '12:10', '13:25', '14:20', '15:15', '16:10', '17:00', '17:50', '18:40', '19:30', '20:20', '21:10']

export const PX_PER_MIN = 1.9

export const STATUS_INFO: Record<SlotStatus, { label: string; caption: string }> = {
  ok: { label: 'Alındı, çakışma yok', caption: 'Çakışma yok' },
  soft: { label: 'Çakışıyor, birinde devam şartı yok', caption: 'Devam şartı yok' },
  conflict: { label: 'Çakışıyor, ikisinde de devam şartı var', caption: 'Çakışma' },
  notTaken: { label: 'Alınmadı', caption: 'Alınmadı' },
}

export const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number)
  return h * 60 + m
}

/** Aynı gündeki blokları şeritlere dağıtır: birbiriyle (dolaylı da olsa) kesişen dersler bir küme oluşturur ve
 *  yan yana durur; kesişmeyen ders sütunun tamamını kullanır. */
export function assignLanes<T extends { startTime: string; endTime: string }>(slots: T[], tieBreak: (s: T) => number = () => 0) {
  const sorted = [...slots].sort((a, b) => toMin(a.startTime) - toMin(b.startTime) || tieBreak(a) - tieBreak(b))
  const placed: { slot: T; lane: number; lanes: number }[] = []
  let cluster: { slot: T; lane: number; lanes: number }[] = []
  let laneEnds: number[] = []
  let clusterEnd = -1
  const flush = () => {
    cluster.forEach((c) => (c.lanes = laneEnds.length))
    placed.push(...cluster)
    cluster = []
    laneEnds = []
  }
  for (const s of sorted) {
    if (toMin(s.startTime) >= clusterEnd) flush()
    let lane = laneEnds.findIndex((end) => end <= toMin(s.startTime))
    if (lane === -1) {
      lane = laneEnds.length
      laneEnds.push(0)
    }
    laneEnds[lane] = toMin(s.endTime)
    clusterEnd = Math.max(clusterEnd, toMin(s.endTime))
    cluster.push({ slot: s, lane, lanes: 1 })
  }
  flush()
  return placed
}

/** Haftalık ders programı: öğrencinin aldığı dersler (yeşil / turuncu / kırmızı) ile dönemindeki alınmayan dersler ve
 *  seçtiği derslerin diğer şubeleri (gri), gerçek saat ölçeğinde. */
export function WeeklySchedule({ slots }: { slots: WeeklySlot[] }) {
  if (slots.length === 0) {
    return <p className="muted">Bu dönem için programda yer alan ders yok.</p>
  }

  const days = [...WEEK_DAYS, ...(['Saturday', 'Sunday'] as DayName[]).filter((d) => slots.some((s) => s.day === d))]
  // Izgara, gösterilen derslerin gerçek saat aralığına kırpılır: dersleri 13:25'te başlayan öğrencide 08:30-13:25 boşluğu çizilmez.
  const first = Math.min(...slots.map((s) => toMin(s.startTime)))
  const last = Math.max(...slots.map((s) => toMin(s.endTime)))
  const height = (last - first) * PX_PER_MIN
  const marks = PERIODS.filter((p) => toMin(p) >= first && toMin(p) < last)
  const statuses = (Object.keys(STATUS_INFO) as SlotStatus[]).filter((k) => slots.some((s) => s.status === k))

  return (
    <div className="wk">
      <ul className="wk-legend" aria-label="Renk açıklaması">
        {statuses.map((k) => (
          <li key={k}>
            <span className={`wk-swatch wk-${k}`} aria-hidden="true" />
            {STATUS_INFO[k].label}
          </li>
        ))}
      </ul>
      <div className="table-wrap">
        <div className="wk-grid" style={{ gridTemplateColumns: `72px repeat(${days.length}, minmax(235px, 1fr))` }}>
          <div className="wk-head" />
          {days.map((d) => (
            <div key={d} className="wk-head">
              {DAY_LABELS[d]}
            </div>
          ))}

          <div className="wk-times" style={{ height }}>
            {marks.map((m) => (
              <span key={m} style={{ top: (toMin(m) - first) * PX_PER_MIN }}>
                {m}
              </span>
            ))}
          </div>

          {days.map((day) => {
            const placed = assignLanes(
              slots.filter((s) => s.day === day),
              (s) => Number(s.status === 'notTaken'),
            )
            return (
              <div key={day} className="wk-day" style={{ height }}>
                {marks.map((m) => (
                  <div key={m} className="wk-line" style={{ top: (toMin(m) - first) * PX_PER_MIN }} />
                ))}
                {placed.map(({ slot: s, lane, lanes }) => (
                  <div
                    key={`${s.courseCode}-${s.section}-${s.startTime}`}
                    className={`wk-block wk-${s.status}`}
                    style={{
                      top: (toMin(s.startTime) - first) * PX_PER_MIN,
                      height: (toMin(s.endTime) - toMin(s.startTime)) * PX_PER_MIN - 2,
                      left: `calc(${(lane * 100) / lanes}% + 2px)`,
                      width: `calc(${100 / lanes}% - 4px)`,
                    }}
                    title={`${s.courseName} (${s.courseCode}) · ${s.section} şubesi · ${s.startTime}-${s.endTime} · ${s.room} · ${STATUS_INFO[s.status].label}`}
                  >
                    <strong className="wk-name">{s.courseName}</strong>
                    <span className="wk-meta">
                      {s.startTime}-{s.endTime} · {s.section} · {s.room}
                    </span>
                    <span className="wk-caption">{STATUS_INFO[s.status].caption}</span>
                  </div>
                ))}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
