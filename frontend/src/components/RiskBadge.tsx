import type { RiskLevel } from '../api/types'
import { RISK_LEVEL_LABELS } from './labels'

const CLASSES: Record<RiskLevel, string> = {
  Dusuk: 'badge badge-ok',
  Orta: 'badge badge-warn',
  Yuksek: 'badge badge-danger',
}

export function RiskBadge({ level }: { level: RiskLevel }) {
  return <span className={CLASSES[level]}>{RISK_LEVEL_LABELS[level]}</span>
}
