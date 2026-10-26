import { Bar, BarChart, CartesianGrid, Cell, LabelList, Legend, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { DashboardResponse } from '../api/types'

// Renkler CSS değişkenlerinden gelir: açık/koyu tema grafikte de otomatik uygulanır.
const TICK = { fill: 'var(--muted)', fontSize: 12 }
const TOOLTIP_STYLE = {
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  color: 'var(--text)',
}

export function RiskDistributionChart({ risk }: { risk: DashboardResponse['risk'] }) {
  const data = [
    { name: 'Düşük', value: risk.low, color: 'var(--ok)' },
    { name: 'Orta', value: risk.medium, color: 'var(--warn)' },
    { name: 'Yüksek', value: risk.high, color: 'var(--danger)' },
  ]
  const total = risk.low + risk.medium + risk.high

  return (
    <div role="img" aria-label={`Risk dağılımı: ${risk.low} düşük, ${risk.medium} orta, ${risk.high} yüksek`}>
      <ResponsiveContainer width="100%" height={240}>
        <PieChart>
          <Pie
            data={data}
            dataKey="value"
            nameKey="name"
            innerRadius={55}
            outerRadius={85}
            paddingAngle={2}
            stroke="var(--surface)"
            isAnimationActive={false}
          >
            {data.map((d) => (
              <Cell key={d.name} fill={d.color} />
            ))}
          </Pie>
          <Tooltip contentStyle={TOOLTIP_STYLE} formatter={(v) => [`${v} öğrenci`, '']} />
          <Legend
            formatter={(value) => {
              const d = data.find((x) => x.name === value)
              const pct = total > 0 && d ? Math.round((d.value / total) * 100) : 0
              return (
                <span style={{ color: 'var(--text)' }}>
                  {value}: {d?.value ?? 0} (%{pct})
                </span>
              )
            }}
          />
        </PieChart>
      </ResponsiveContainer>
      <p className="muted chart-note">Toplam {total} öğrenci</p>
    </div>
  )
}

export function GpaTrendChart({ trend }: { trend: DashboardResponse['gpaTrend'] }) {
  if (trend.length === 0) return <p className="muted">Henüz notlandırılmış dönem yok.</p>

  return (
    <div role="img" aria-label="Dönem bazlı ortalama GANO eğilimi">
      <ResponsiveContainer width="100%" height={240}>
        <LineChart data={trend} margin={{ top: 8, right: 16, bottom: 4, left: -16 }}>
          <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" />
          <XAxis dataKey="term" tick={TICK} tickFormatter={(t) => `${t}. dön.`} stroke="var(--border)" />
          <YAxis domain={[0, 4]} tick={TICK} stroke="var(--border)" />
          <Tooltip
            contentStyle={TOOLTIP_STYLE}
            labelFormatter={(t) => `${t}. dönem`}
            formatter={(value, _name, item) => [`${Number(value).toFixed(2)} (${(item.payload as { studentCount: number }).studentCount} öğrenci)`, 'Ortalama']}
          />
          <Line type="monotone" dataKey="averageGpa" stroke="var(--chart)" strokeWidth={2.5} dot={{ r: 4, fill: 'var(--chart)' }} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

export function CourseSuccessChart({ courses }: { courses: DashboardResponse['courseSuccess'] }) {
  if (courses.length === 0) return <p className="muted">Yeterli veri yok.</p>

  return (
    <div role="img" aria-label="Başarı oranı en düşük dersler">
      <ResponsiveContainer width="100%" height={Math.max(220, courses.length * 28 + 40)}>
        <BarChart data={courses} layout="vertical" margin={{ top: 4, right: 24, bottom: 4, left: 8 }}>
          <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" horizontal={false} />
          <XAxis type="number" domain={[0, 100]} tick={TICK} tickFormatter={(v) => `%${v}`} stroke="var(--border)" />
          <YAxis type="category" dataKey="courseCode" width={70} tick={TICK} stroke="var(--border)" />
          <Tooltip
            contentStyle={TOOLTIP_STYLE}
            cursor={{ fill: 'var(--bg)' }}
            labelFormatter={(code) => courses.find((c) => c.courseCode === code)?.courseName ?? code}
            formatter={(value, _name, item) => {
              const c = item.payload as { passed: number; attempts: number }
              return [`%${Number(value).toFixed(1)} (${c.passed}/${c.attempts})`, 'Başarı']
            }}
          />
          <Bar dataKey="successRate" fill="var(--chart)" radius={[0, 4, 4, 0]}>
            <LabelList dataKey="successRate" position="right" formatter={(v) => `%${Number(v).toFixed(0)}`} fill="var(--text)" fontSize={12} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
