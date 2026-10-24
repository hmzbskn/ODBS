import type { DayName, IssueSeverity, RegistrationDecision } from '../api/types'

export const DECISION_LABELS: Record<RegistrationDecision, string> = {
  Pending: 'Karar bekliyor',
  Approved: 'Onaylandı',
  RevisionRequested: 'Düzeltme istendi',
}

export const SEVERITY_LABELS: Record<IssueSeverity, string> = {
  Error: 'Hata',
  Warning: 'Uyarı',
  Info: 'Bilgi',
}

export const RULE_LABELS: Record<string, string> = {
  TIME_CONFLICT: 'Ders saati çakışması',
  PREREQUISITE: 'Önkoşul',
  LOWER_SEMESTER_PRIORITY: 'Alttan ders önceliği',
  CREDIT_LIMIT: 'AKTS limiti',
  EXAM_CONFLICT: 'Sınav çakışması',
  RETAKE: 'Tekrar alma',
}

export const DAY_LABELS: Record<DayName, string> = {
  Monday: 'Pazartesi',
  Tuesday: 'Salı',
  Wednesday: 'Çarşamba',
  Thursday: 'Perşembe',
  Friday: 'Cuma',
  Saturday: 'Cumartesi',
  Sunday: 'Pazar',
}
