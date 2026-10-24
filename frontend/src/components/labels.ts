import type { DayName, IssueSeverity, QuestionType, RegistrationDecision, SurveyStatus } from '../api/types'

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

export const SURVEY_STATUS_LABELS: Record<SurveyStatus, string> = {
  Planned: 'Planlandı',
  Open: 'Açık',
  Closed: 'Kapandı',
}

export const QUESTION_TYPE_LABELS: Record<QuestionType, string> = {
  SingleChoice: 'Tek seçim',
  MultiChoice: 'Çoklu seçim',
  Number: 'Sayı',
  Text: 'Metin',
}
