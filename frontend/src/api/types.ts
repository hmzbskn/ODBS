// Backend sözleşmesinin ön yüz tarafı (Advisor.Api / Advisor.Application).
// Enum'lar API'de metin olarak gelir.

export type Role = 'Admin' | 'Danisman' | 'Ogrenci' | 'BolumBaskani' | 'Dekan'

export interface UserInfo {
  id: string
  username: string
  displayName: string
  role: Role
  linkedAdvisorId: string | null
  linkedStudentNo: string | null
  department?: string | null
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
  conflictCount: number
}

export interface StudentListRow {
  studentNo: string
  fullName: string
  classYear: number
  program: string
  gpa: number
  advisorName: string
  status: string
}

export interface StudentProfileDetail {
  school: {
    studentNo: string
    fullName: string
    department: string
    classYear: number
    term: number
    program: string
    status: string
  }
  contact: { email: string | null; phone: string | null; residence: string | null }
  academic: {
    gpa: number
    earnedEcts: number
    requiredEcts: number
    remainingEcts: number
    currentTermCourseCount: number
  }
  advisor: AdvisorContact | null
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
  status: SlotStatus
}

/** ok: alındı, çakışma yok · soft: çakışıyor ama birinde devam şartı yok · conflict: çakışma · notTaken: alınmadı */
export type SlotStatus = 'ok' | 'soft' | 'conflict' | 'notTaken'

export interface RegistrationReview {
  id: string
  decision: RegistrationDecision
  note: string | null
  reviewedByAdvisorId: string
  reviewedAt: string
}

export interface AdvisorContact {
  name: string
  title: string
  email: string
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
  advisor: AdvisorContact | null
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

// ---- erken uyarı ve mezuniyet ----

export type RiskLevel = 'Dusuk' | 'Orta' | 'Yuksek'

export interface RiskIndicator {
  code: string
  title: string
  points: number
  explanation: string
}

export interface RiskRow {
  studentNo: string
  fullName: string
  classYear: number
  gpa: number
  term: number
  score: number
  level: RiskLevel
  indicatorTitles: string[]
}

export interface RiskDetail {
  studentNo: string
  fullName: string
  classYear: number
  gpa: number
  term: number
  score: number
  level: RiskLevel
  indicators: RiskIndicator[]
}

export type MissingStatus = 'Failed' | 'NotTaken' | 'InProgress'

export interface MissingCourse {
  courseCode: string
  courseName: string
  term: number
  ects: number
  status: MissingStatus
}

export interface GraduationAnalysis {
  studentNo: string
  fullName: string
  currentTerm: number
  gpa: number
  minGpa: number
  gpaOk: boolean
  earnedEcts: number
  requiredEcts: number
  ectsProgressPercent: number
  mandatoryTotal: number
  mandatoryPassed: number
  missingMandatory: MissingCourse[]
  electiveEarnedCredits: number
  electiveRequiredCredits: number
  passedElectives: { courseCode: string; courseName: string; credit: number; ects: number }[]
  internshipDays: number
  internshipRequiredDays: number
  canGraduate: boolean
  blockers: string[]
  projectedCanGraduate: boolean
  projectedBlockers: string[]
  estimatedRemainingTerms: number
}

export interface DashboardResponse {
  studentCount: number
  averageGpa: number
  risk: { low: number; medium: number; high: number }
  gpaTrend: { term: number; averageGpa: number; studentCount: number }[]
  courseSuccess: { courseCode: string; courseName: string; attempts: number; passed: number; successRate: number }[]
  graduationCandidates: number
}

// ---- yönetim (bölüm başkanı, dekan, yönetici) ----

export interface AdvisorSummary {
  advisorId: string
  fullName: string
  title: string
  studentCount: number
  averageGpa: number
  risk: { low: number; medium: number; high: number }
  graduationCandidates: number
}

export interface ManagementOverview {
  total: DashboardResponse
  advisors: AdvisorSummary[]
}

export interface ManagedStudent {
  studentNo: string
  fullName: string
  classYear: number
  department: string
  gpa: number
  advisorId: string
  advisorName: string
  obisAdvisorId: string
  isReassigned: boolean
}

/** Yönetim ekranı: okuldaki tüm derslerin haftalık programı (öğretim elemanıyla). term: yarıyıl 1-8. */
export interface SchoolScheduleSlot {
  term: number
  courseCode: string
  courseName: string
  section: string
  day: DayName
  startTime: string
  endTime: string
  room: string
  instructor: string | null
}

export interface SchoolSchedule {
  semester: 'fall' | 'spring'
  slots: SchoolScheduleSlot[]
}

export interface ManagedAdvisor {
  advisorId: string
  fullName: string
  title: string
  studentCount: number
}

export interface AssignResult {
  assigned: number
  unchanged: number
  restoredToObis: number
}

// ---- randevu, görüşme notu ----

export type AppointmentStatus = 'Scheduled' | 'CancelledByStudent' | 'CancelledByAdvisor' | 'Completed'

export interface Slot {
  id: string
  advisorId: string
  day: DayName
  startTime: string
  endTime: string
  isActive: boolean
  upcomingBookings: number
}

export interface Appointment {
  id: string
  slotId: string
  advisorId: string
  studentNo: string
  studentName: string
  date: string
  startTime: string
  endTime: string
  status: AppointmentStatus
  cancellationReason: string | null
  bookedAt: string
  isPast: boolean
}

export interface AvailableSlot {
  slotId: string
  date: string
  day: DayName
  startTime: string
  endTime: string
  available: boolean
  bookedByMe: boolean
}

export interface Availability {
  advisorId: string
  advisorName: string
  maxActiveAppointments: number
  activeAppointments: number
  slots: AvailableSlot[]
}

export interface Note {
  id: string
  advisorId: string
  studentNo: string
  content: string
  createdAt: string
}

export interface TimelineItem {
  kind: 'Note' | 'Appointment' | 'Petition' | 'Announcement'
  date: string
  title: string
  detail: string | null
  refId: string | null
}

export interface StudentFile {
  studentNo: string
  fullName: string
  classYear: number
  gpa: number
  timeline: TimelineItem[]
  notes: Note[]
}

// ---- duyuru, gelen kutusu, talep ----

export interface AnnouncementFilter {
  classYear: number | null
  minGpa: number | null
  maxGpa: number | null
  riskLevel: RiskLevel | null
  internshipComplete: boolean | null
}

export interface RecipientPreview {
  studentNo: string
  fullName: string
  classYear: number
  gpa: number | null
  risk: RiskLevel
  internshipDays: number
}

export interface AudiencePreview {
  count: number
  students: RecipientPreview[]
}

export interface AnnouncementSummary {
  id: string
  title: string
  body: string
  createdAt: string
  advisorId: string
  filter: AnnouncementFilter
  recipientCount: number
  readCount: number
}

export interface AnnouncementDetail {
  summary: AnnouncementSummary
  recipients: { studentNo: string; fullName: string; readAt: string | null }[]
}

export interface InboxItem {
  id: string
  kind: 'Announcement' | 'Notification'
  title: string
  body: string
  sender: string | null
  createdAt: string
  read: boolean
}

export type PetitionType = 'DersEkleBirak' | 'KayitDondurma' | 'Muafiyet' | 'Diger'
export type PetitionStatus = 'Beklemede' | 'Inceleniyor' | 'Onaylandi' | 'Reddedildi'

export interface Petition {
  id: string
  studentNo: string
  studentName: string
  type: PetitionType
  description: string
  status: PetitionStatus
  advisorNote: string | null
  hasAttachment: boolean
  attachmentFileName: string | null
  createdAt: string
  updatedAt: string | null
}
