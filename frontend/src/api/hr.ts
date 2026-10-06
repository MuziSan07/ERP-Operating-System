// Types and helpers for HR & Payroll.
import dayjs from 'dayjs'

export type Gender = 'Male' | 'Female' | 'Other'
export type EmploymentType = 'Permanent' | 'Probation' | 'Contract' | 'Intern' | 'DailyWage'
export type EmployeeStatus = 'Active' | 'Resigned' | 'Terminated' | 'Retired'
export type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'HalfDay' | 'Leave' | 'Holiday' | 'WeeklyOff'
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export type ApprovalStatus = 'Waiting' | 'Pending' | 'Approved' | 'Rejected' | 'Skipped'
export type ApproverType = 'LineManager' | 'DepartmentHead' | 'HrPermission' | 'SpecificUser'
export type PayComponentKind = 'Earning' | 'Deduction'
export type PayrollRunStatus = 'Draft' | 'Approved' | 'Posted' | 'Cancelled'

export const EMPLOYMENT_TYPES: EmploymentType[] = ['Permanent', 'Probation', 'Contract', 'Intern', 'DailyWage']
export const EMPLOYEE_STATUSES: EmployeeStatus[] = ['Active', 'Resigned', 'Terminated', 'Retired']
export const APPROVER_LABELS: Record<ApproverType, string> = {
  LineManager: "Employee's line manager",
  DepartmentHead: 'Head of department',
  HrPermission: 'HR team (anyone with leave-approve permission)',
  SpecificUser: 'A specific person',
}

export interface Department { id: string; entityId: string; entityName: string; name: string; code: string; headEmployeeId?: string; headName?: string; employeeCount: number }
export interface Designation { id: string; title: string; grade?: string }

export interface EmployeeListItem {
  id: string; employeeCode: string; userId: string; fullName: string; email: string; phone?: string; entityId: string
  entityName: string; department?: string; designation?: string; managerName?: string; employmentType: EmploymentType
  status: EmployeeStatus; joinDate: string
}

export interface EmployeeData {
  employeeCode: string; fullName: string; phone?: string; entityId: string; departmentId?: string; designationId?: string
  managerId?: string; employmentType: EmploymentType; status: EmployeeStatus; joinDate: string; confirmationDate?: string
  exitDate?: string; fatherName?: string; cnic?: string; gender?: Gender; dateOfBirth?: string; address?: string; city?: string
  emergencyContactName?: string; emergencyContactPhone?: string; bankName?: string; bankAccountTitle?: string; iban?: string
  ntn?: string; eobiNumber?: string; eobiMember: boolean; providentFundMember: boolean; socialSecurityMember: boolean
}

export interface Employee extends EmployeeData {
  id: string; userId: string; email: string; userType: string; entityName: string; department?: string
  designation?: string; managerName?: string
}

export interface AttendanceRow {
  employeeId: string; employeeCode: string; fullName: string; department?: string; status?: AttendanceStatus
  checkIn?: string; checkOut?: string; remarks?: string; dayType?: string
}
export interface AttendanceDay { date: string; status?: AttendanceStatus; dayType?: string; checkIn?: string; checkOut?: string; remarks?: string; leaveType?: string }
export interface MonthlyAttendance { employeeId: string; fullName: string; year: number; month: number; days: AttendanceDay[]; summary: Record<string, number> }
export interface Holiday { id: string; date: string; name: string; entityId?: string; entityName?: string }

export interface LeaveType { id: string; code: string; name: string; daysPerYear: number; isPaid: boolean; allowHalfDay: boolean; onlyForGender?: Gender; isActive: boolean }
export interface LeaveStep { id: string; stepOrder: number; name: string; approverType: ApproverType; approverUserId?: string; approverName?: string }
export interface LeaveBalance { leaveTypeId: string; code: string; name: string; isPaid: boolean; entitled: number; used: number; pending: number; available?: number }
export interface LeaveApproval { stepOrder: number; stepName: string; approverType: ApproverType; approverName?: string; status: ApprovalStatus; actedBy?: string; actedAt?: string; comment?: string }
export interface LeaveRequest {
  id: string; employeeId: string; employeeName: string; employeeCode: string; entityName: string; leaveTypeId: string
  leaveType: string; isPaid: boolean; fromDate: string; toDate: string; isHalfDay: boolean; days: number; reason?: string
  status: LeaveStatus; createdAt: string; approvals: LeaveApproval[]; canAct: boolean
}

export interface HrSettings {
  weeklyOffDays: string[]; minimumWage: number; eobiEnabled: boolean; eobiEmployeeRate: number; eobiEmployerRate: number
  providentFundEnabled: boolean; providentFundEmployeeRate: number; providentFundEmployerRate: number
  socialSecurityEnabled: boolean; socialSecurityEmployerRate: number; socialSecurityWageCeiling: number
  payrollRequiresSecondApprover: boolean
}

export interface PayComponent { id: string; code: string; name: string; kind: PayComponentKind; isTaxable: boolean; isBasic: boolean; exemptUpToFractionOfBasic?: number; sortOrder: number; isActive: boolean }
export interface SalaryLine { payComponentId: string; code: string; name: string; kind: PayComponentKind; amount: number }
export interface Salary { id: string; effectiveFrom: string; remarks?: string; gross: number; deductions: number; lines: SalaryLine[] }
export interface TaxSlab { from: number; to?: number | null; fixedTax: number; rate: number }
export interface TaxYear { id: string; year: number; surchargeThreshold?: number | null; surchargeRate: number; notes?: string; slabs: TaxSlab[] }
export interface TaxPreview { taxYear: number; annualIncome: number; annualTax: number; monthlyTax: number; effectiveRate: number }

export interface PayrollRun {
  id: string; entityId: string; entityName: string; year: number; month: number; includeSubEntities: boolean
  status: PayrollRunStatus; notes?: string; employeeCount: number; totalGross: number; totalDeductions: number
  totalNet: number; totalEmployerContributions: number; createdByName?: string; createdAt: string; approvedByName?: string
  approvedAt?: string; postedByName?: string; postedAt?: string; journalEntryId?: string; paymentJournalEntryId?: string; warnings: string[]
}
export interface PayslipLine { code: string; name: string; kind: PayComponentKind; amount: number; isEmployerContribution: boolean }
export interface Payslip {
  id: string; payrollRunId: string; runStatus: PayrollRunStatus; employeeId: string; year: number; month: number
  employeeCode: string; employeeName: string; designation?: string; department?: string; entityName?: string; cnic?: string
  iban?: string; bankName?: string; daysInMonth: number; unpaidDays: number; payableDays: number; monthlyGross: number
  grossEarnings: number; taxableIncome: number; incomeTax: number; totalDeductions: number; netPay: number
  employerContributions: number; projectedAnnualTaxable: number; projectedAnnualTax: number; lines: PayslipLine[]
}
export interface PayrollAdjustment { id: string; employeeId: string; name: string; kind: PayComponentKind; amount: number; isTaxable: boolean }
export interface PayrollRunDetail { run: PayrollRun; payslips: Payslip[]; adjustments: PayrollAdjustment[] }
export interface MyHr { employee?: Employee; balances: LeaveBalance[]; pendingApprovals: number }

// ---- formatting ----
export const money = (n?: number) => (n ?? 0).toLocaleString('en-PK', { maximumFractionDigits: 0 })
export const pct = (fraction?: number) => `${+((fraction ?? 0) * 100).toFixed(2)}%`
export const monthName = (year: number, month: number) => dayjs(new Date(year, month - 1, 1)).format('MMMM YYYY')
export const fmtDate = (d?: string) => (d ? dayjs(d).format('DD MMM YYYY') : '—')
export const toIsoDate = (d?: dayjs.Dayjs | null) => (d ? d.format('YYYY-MM-DD') : undefined)

export const LEAVE_COLORS: Record<LeaveStatus, string> = { Pending: 'gold', Approved: 'green', Rejected: 'red', Cancelled: 'default' }
export const RUN_COLORS: Record<PayrollRunStatus, string> = { Draft: 'blue', Approved: 'gold', Posted: 'green', Cancelled: 'default' }
export const ATTENDANCE_COLORS: Record<AttendanceStatus, string> = {
  Present: 'green', Absent: 'red', Late: 'orange', HalfDay: 'gold', Leave: 'purple', Holiday: 'cyan', WeeklyOff: 'default',
}
