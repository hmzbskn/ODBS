import type { RegistrationDecision } from '../api/types'

export const DECISION_LABELS: Record<RegistrationDecision, string> = {
  Pending: 'Karar bekliyor',
  Approved: 'Onaylandı',
  RevisionRequested: 'Düzeltme istendi',
}
