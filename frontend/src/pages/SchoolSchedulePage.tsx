import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import type { DayName, SchoolSchedule, SchoolScheduleSlot } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { DAY_LABELS } from '../components/labels'
import { assignLanes, PERIODS, PX_PER_MIN, toMin } from '../components/WeeklySchedule'
import { useAsync } from '../components/useAsync'

const WEEK_DAYS: DayName[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']
const JS_DAYS: DayName[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const PERIOD_LENGTH = 45

const classYearOf = (term: number) => Math.ceil(term / 2)
const hhmm = (min: number) => `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`

/** Anlık saat: dakikada bir tazelenir; ekran açık kalsa da "şu an" doğru kalır. */
function useNow() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 15_000)
    return () => window.clearInterval(id)
  }, [])
  return now
}

/** Şu an hangi ders saatindeyiz: "5. ders saati (12:10-12:55)", teneffüs ya da ders saati dışı. */
function describePeriod(nowMin: number, isWeekday: boolean) {
  if (!isWeekday) return 'Hafta sonu, ders yok'
  const idx = PERIODS.findIndex((p) => nowMin >= toMin(p) && nowMin < toMin(p) + PERIOD_LENGTH)
  if (idx >= 0) return `${idx + 1}. ders saati (${PERIODS[idx]}-${hhmm(toMin(PERIODS[idx]) + PERIOD_LENGTH)})`
  if (nowMin < toMin(PERIODS[0])) return `Ders saatleri ${PERIODS[0]}'da başlıyor`
  if (nowMin >= toMin(PERIODS[PERIODS.length - 1]) + PERIOD_LENGTH) return 'Ders saatleri bitti'
  return 'Teneffüs'
}

const isActive = (s: SchoolScheduleSlot, day: DayName, nowMin: number) =>
  s.day === day && toMin(s.startTime) <= nowMin && nowMin < toMin(s.endTime)

/** Izgarada bir ders bloğu: ad, hoca, saat/şube/derslik; şu an işleniyorsa vurgulu. */
function SlotBlock({
  s, first, lane, lanes, live,
}: { s: SchoolScheduleSlot; first: number; lane: number; lanes: number; live: boolean }) {
  return (
    <div
      className={`wk-block sc-y${classYearOf(s.term)}${live ? ' sc-live' : ''}`}
      style={{
        top: (toMin(s.startTime) - first) * PX_PER_MIN,
        height: (toMin(s.endTime) - toMin(s.startTime)) * PX_PER_MIN - 2,
        left: `calc(${(lane * 100) / lanes}% + 2px)`,
        width: `calc(${100 / lanes}% - 4px)`,
      }}
      title={`${s.courseName} (${s.courseCode}) · ${classYearOf(s.term)}. sınıf · ${s.section} · ${s.startTime}-${s.endTime} · ${s.room} · ${s.instructor ?? '-'}`}
    >
      <strong className="wk-name">{s.courseName}</strong>
      <span className="sc-instructor">{s.instructor ?? 'Öğretim elemanı belirtilmemiş'}</span>
      <span className="wk-meta">
        {s.startTime}-{s.endTime} · {s.section} · {s.room}
      </span>
      {live && <span className="wk-caption">Şu an işleniyor</span>}
    </div>
  )
}

/** Yönetici / bölüm başkanı / dekan: okuldaki tüm derslerin haftalık programı, ders veren hocalarla; şu an işlenen dersler öne çıkar. */
export function SchoolSchedulePage() {
  const now = useNow()
  const today = JS_DAYS[now.getDay()]
  const nowMin = now.getHours() * 60 + now.getMinutes()
  const isWeekday = WEEK_DAYS.includes(today)

  const [semester, setSemester] = useState<'' | 'fall' | 'spring'>('')
  const schedule = useAsync(() => api.get<SchoolSchedule>(`/api/management/schedule${semester ? `?semester=${semester}` : ''}`), [semester])
  const [day, setDay] = useState<DayName>(isWeekday ? today : 'Monday')
  const [query, setQuery] = useState('')
  const [onlyActive, setOnlyActive] = useState(false)

  const all = schedule.state.status === 'ready' ? schedule.state.data.slots : []
  const activeSemester = schedule.state.status === 'ready' ? schedule.state.data.semester : null

  const matches = (s: SchoolScheduleSlot) => {
    const q = query.trim().toLocaleLowerCase('tr-TR')
    return !q || [s.courseName, s.courseCode, s.instructor ?? '', s.room].some((v) => v.toLocaleLowerCase('tr-TR').includes(q))
  }

  const activeNow = useMemo(
    () => (isWeekday ? all.filter((s) => isActive(s, today, nowMin)) : []),
    [all, today, nowMin, isWeekday],
  )
  const nextUp = useMemo(() => {
    if (!isWeekday || activeNow.length > 0) return []
    const later = all.filter((s) => s.day === today && toMin(s.startTime) > nowMin)
    if (later.length === 0) return []
    const soonest = Math.min(...later.map((s) => toMin(s.startTime)))
    return later.filter((s) => toMin(s.startTime) === soonest)
  }, [all, today, nowMin, isWeekday, activeNow.length])

  if (schedule.state.status === 'loading') return <Loading />
  if (schedule.state.status === 'error') return <ErrorState message={schedule.state.message} onRetry={schedule.retry} />

  const visible = all.filter((s) => s.day === day && matches(s) && (!onlyActive || isActive(s, day, nowMin)))
  const years = [...new Set(all.map((s) => classYearOf(s.term)))].sort((a, b) => a - b)

  const dayCount = (d: DayName) => all.filter((s) => s.day === d).length
  const first = Math.min(...(visible.length ? visible : all.filter((s) => s.day === day)).map((s) => toMin(s.startTime)), toMin(PERIODS[0]))
  const last = Math.max(...(visible.length ? visible : all.filter((s) => s.day === day)).map((s) => toMin(s.endTime)), toMin(PERIODS[8]))
  const height = (last - first) * PX_PER_MIN
  const marks = PERIODS.filter((p) => toMin(p) >= first && toMin(p) < last)
  const showNowLine = day === today && nowMin >= first && nowMin <= last

  return (
    <>
      <div className="page-head">
        <h1>Ders Programı</h1>
        <div className="seg" role="group" aria-label="Dönem">
          {(['fall', 'spring'] as const).map((k) => (
            <button
              key={k}
              type="button"
              className={`seg-btn${activeSemester === k ? ' seg-on' : ''}`}
              aria-pressed={activeSemester === k}
              onClick={() => setSemester(k)}
            >
              {k === 'fall' ? 'Güz dönemi' : 'Bahar dönemi'}
            </button>
          ))}
        </div>
      </div>

      <section className="card sc-now" aria-live="polite">
        <div className="sc-clock">
          <span className="sc-time">{now.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}</span>
          <span className="sc-day">{DAY_LABELS[today]}</span>
        </div>
        <div className="sc-now-body">
          <strong>{describePeriod(nowMin, isWeekday)}</strong>
          {activeNow.length > 0 ? (
            <span className="muted">{activeNow.length} derste ders işleniyor</span>
          ) : nextUp.length > 0 ? (
            <span className="muted">
              Şu an aktif ders yok. Sıradaki: {nextUp[0].startTime} ({nextUp.length} ders)
            </span>
          ) : (
            <span className="muted">Şu an aktif ders yok.</span>
          )}
        </div>
        {activeNow.length > 0 && (
          <ul className="sc-active-list">
            {activeNow.map((s) => (
              <li key={`${s.courseCode}-${s.section}-${s.startTime}`} className={`sc-chip sc-y${classYearOf(s.term)}`}>
                <strong>{s.courseName}</strong>
                <span>{s.instructor ?? 'Öğretim elemanı belirtilmemiş'}</span>
                <span className="muted">
                  {classYearOf(s.term)}. sınıf · {s.section} · {s.room} · {s.endTime}'a kadar
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="card toolbar">
        <div className="seg" role="tablist" aria-label="Gün">
          {WEEK_DAYS.map((d) => (
            <button
              key={d}
              type="button"
              role="tab"
              aria-selected={day === d}
              className={`seg-btn${day === d ? ' seg-on' : ''}${d === today ? ' seg-today' : ''}`}
              onClick={() => setDay(d)}
            >
              {DAY_LABELS[d]} <span className="seg-count">{dayCount(d)}</span>
            </button>
          ))}
        </div>
        <label className="field">
          <span>Ders, hoca ya da derslik ara</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Örn. Veri Yapıları, Arslan, LAB3" />
        </label>
        <label className="check">
          <input type="checkbox" checked={onlyActive} onChange={(e) => setOnlyActive(e.target.checked)} />
          Yalnızca şu an aktif olanlar
        </label>
      </div>

      {all.length === 0 ? (
        <EmptyState title="Bu dönem için programda ders yok." />
      ) : visible.length === 0 ? (
        <EmptyState title="Bu gün için gösterilecek ders yok." />
      ) : (
        <div className="table-wrap">
          <div className="sc-grid" style={{ gridTemplateColumns: `72px repeat(${years.length}, minmax(260px, 1fr))` }}>
            <div className="wk-head" />
            {years.map((y) => (
              <div key={y} className="wk-head">
                {y}. sınıf
              </div>
            ))}

            <div className="wk-times" style={{ height }}>
              {marks.map((m) => (
                <span key={m} style={{ top: (toMin(m) - first) * PX_PER_MIN }}>
                  {m}
                </span>
              ))}
            </div>

            {years.map((y) => {
              const placed = assignLanes(visible.filter((s) => classYearOf(s.term) === y))
              return (
                <div key={y} className="wk-day" style={{ height }}>
                  {marks.map((m) => (
                    <div key={m} className="wk-line" style={{ top: (toMin(m) - first) * PX_PER_MIN }} />
                  ))}
                  {showNowLine && <div className="sc-nowline" style={{ top: (nowMin - first) * PX_PER_MIN }} />}
                  {placed.map(({ slot: s, lane, lanes }) => (
                    <SlotBlock
                      key={`${s.courseCode}-${s.section}-${s.startTime}`}
                      s={s}
                      first={first}
                      lane={lane}
                      lanes={lanes}
                      live={day === today && isActive(s, today, nowMin)}
                    />
                  ))}
                </div>
              )
            })}
          </div>
        </div>
      )}
    </>
  )
}
