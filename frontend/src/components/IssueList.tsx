import type { IssueSeverity, RegistrationIssue } from '../api/types'
import { RULE_LABELS, SEVERITY_LABELS } from './labels'

const BADGE: Record<IssueSeverity, string> = {
  Error: 'badge badge-danger',
  Warning: 'badge badge-warn',
  Info: 'badge',
}

export function IssueList({ issues }: { issues: RegistrationIssue[] }) {
  if (issues.length === 0) {
    return <p className="muted">Kural kontrollerinde bulgu çıkmadı. Kayıt uygun görünüyor.</p>
  }

  return (
    <ul className="issue-list">
      {issues.map((issue, i) => (
        <li key={`${issue.ruleCode}-${i}`} className={`issue issue-${issue.severity.toLowerCase()}`}>
          <div className="issue-head">
            <span className={BADGE[issue.severity]}>{SEVERITY_LABELS[issue.severity]}</span>
            <strong>{RULE_LABELS[issue.ruleCode] ?? issue.ruleCode}</strong>
          </div>
          <p>{issue.message}</p>
        </li>
      ))}
    </ul>
  )
}
