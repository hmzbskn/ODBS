// Backend sözleşmesinin ön yüz tarafı (Advisor.Api / Advisor.Application).
// Enum'lar API'de metin olarak gelir.

export type Role = 'Admin' | 'Danisman' | 'Ogrenci'

export interface UserInfo {
  id: string
  username: string
  displayName: string
  role: Role
  linkedAdvisorId: string | null
  linkedStudentNo: string | null
}

export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
  user: UserInfo
}

export type RegistrationDecision = 'Pending' | 'Approved' | 'RevisionRequested'
export type IssueSeverity = 'Info' | 'Warning' | 'Error'

export interface AdvisorStudentRow {
  studentNo: string
  fullName: string
  classYear: number
  gpa: number
  term: number
  courseCount: number
  totalEcts: number
  errorCount: number
  warningCount: number
  infoCount: number
  decision: RegistrationDecision
}

export interface AdvisorStudentList {
  term: number | null
  availableTerms: number[]
  students: AdvisorStudentRow[]
}

export interface RegistrationIssue {
  ruleCode: string
  severity: IssueSeverity
  message: string
  courseCodes: string[]
}

export interface RegisteredCourse {
  courseCode: string
  courseName: string
  section: string
  ects: number
  courseTerm: number
  inConflict: boolean
}

export type DayName = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday'

export interface WeeklySlot {
  courseCode: string
  courseName: string
  section: string
  day: DayName
  startTime: string
  endTime: string
  room: string
  inConflict: boolean
}

export interface RegistrationReview {
  id: string
  decision: RegistrationDecision
  note: string | null
  reviewedByAdvisorId: string
  reviewedAt: string
}

export interface RegistrationDetail {
  studentNo: string
  fullName: string
  classYear: number
  term: number
  gpa: number
  totalEcts: number
  maxEcts: number
  courses: RegisteredCourse[]
  weeklySchedule: WeeklySlot[]
  issues: RegistrationIssue[]
  errorCount: number
  warningCount: number
  infoCount: number
  decision: RegistrationDecision
  reviews: RegistrationReview[]
}

// ---- anketler ----

export type QuestionType = 'SingleChoice' | 'MultiChoice' | 'Number' | 'Text'
export type SurveyStatus = 'Planned' | 'Open' | 'Closed'

export interface SurveySummary {
  id: string
  title: string
  startDate: string
  endDate: string
  status: SurveyStatus
  questionCount: number
  targetCount: number
  responseCount: number
}

export interface SurveyOption {
  id: string
  text: string
  score: number
}

export interface SurveyQuestion {
  id: string
  text: string
  type: QuestionType
  order: number
  weight: number
  required: boolean
  options: SurveyOption[]
}

export interface SurveyDefinition {
  id: string
  title: string
  description: string | null
  startDate: string
  endDate: string
  status: SurveyStatus
  targetClassYear: number | null
  createdByAdvisorId: string
  targetCount: number
  responseCount: number
  questions: SurveyQuestion[]
}

export interface CreateSurveyPayload {
  title: string
  description: string | null
  startDate: string
  endDate: string
  targetClassYear: number | null
  questions: {
    text: string
    type: QuestionType
    weight: number
    required: boolean
    options: { text: string; score: number }[] | null
  }[]
}

export interface ScoreItem {
  questionId: string
  questionText: string
  answerText: string
  points: number
}

export interface ResultRow {
  rank: number
  studentNo: string
  fullName: string
  classYear: number
  totalScore: number
  importedExternally: boolean
  support: string | null
  breakdown: ScoreItem[]
}

export interface ResultsResponse {
  surveyId: string
  title: string
  targetCount: number
  responseCount: number
  rows: ResultRow[]
}

export interface ImportError {
  row: number
  studentNo: string | null
  message: string
}

export interface ImportResult {
  imported: number
  errors: ImportError[]
}

export interface QuestionStat {
  questionId: string
  text: string
  type: QuestionType
  options: { text: string; count: number }[] | null
  number: { count: number; min: number; max: number; average: number } | null
  answeredCount: number
}

export interface StatsResponse {
  targetCount: number
  responseCount: number
  questions: QuestionStat[]
}

export interface StudentSurveyItem {
  id: string
  title: string
  description: string | null
  startDate: string
  endDate: string
  status: SurveyStatus
  submitted: boolean
}

export interface SurveyForm {
  id: string
  title: string
  description: string | null
  endDate: string
  consentText: string
  consentStatement: string
  questions: {
    id: string
    text: string
    type: QuestionType
    required: boolean
    options: { id: string; text: string }[]
  }[]
}
