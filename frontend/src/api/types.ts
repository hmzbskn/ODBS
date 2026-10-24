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
