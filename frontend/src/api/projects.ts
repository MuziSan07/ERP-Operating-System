// Types for Projects & services (clients, projects, tasks, timesheets).
export type BillingType = 'TimeAndMaterials' | 'FixedPrice' | 'NonBillable'
export type ProjectStatus = 'Planned' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled'
export type WorkItemStatus = 'Todo' | 'InProgress' | 'Review' | 'Done'
export type WorkItemPriority = 'Low' | 'Medium' | 'High' | 'Urgent'
export type TimeEntryStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Invoiced'

export interface ProjectPerson { employeeId: string; name: string; employeeCode: string; designation?: string; entityName: string; costRate: number }
export interface Client { id: string; code: string; name: string; email?: string; phone?: string; city?: string; ntn?: string; activeProjects: number; billed: number; unbilled: number }
export interface ProjectListItem {
  id: string; code: string; name: string; clientName?: string; billingType: BillingType; status: ProjectStatus; startDate: string; endDate?: string; managerName?: string
  budgetHours: number; hoursLogged: number; hoursPercent: number; billed: number; unbilled: number; cost: number; margin: number; openTasks: number; overBudget: boolean
}
export interface Member { id: string; employeeId: string; name: string; role?: string; billRate?: number; effectiveBillRate: number; costRate: number; hours: number; billableHours: number }
export interface Milestone { id: string; name: string; dueDate: string; amount: number; completedOn?: string; invoiceId?: string; invoiceNumber?: string; overdue: boolean }
export interface Project {
  id: string; entityId: string; entityName: string; code: string; name: string; clientId?: string; clientName?: string; billingType: BillingType; status: ProjectStatus
  startDate: string; endDate?: string; budgetHours: number; contractAmount: number; defaultBillRate: number; taxRateId?: string; managerEmployeeId?: string
  managerName?: string; description?: string; members: Member[]; milestones: Milestone[]; hoursLogged: number; hoursApproved: number; hoursPending: number
  billableHours: number; billed: number; unbilled: number; cost: number; margin: number; marginPercent: number; hoursPercent: number
  tasksByStatus: Record<string, number>; warnings: string[]
}
export interface Task {
  id: string; projectId: string; key: string; title: string; description?: string; status: WorkItemStatus; priority: WorkItemPriority; assigneeEmployeeId?: string
  assigneeName?: string; estimateHours: number; loggedHours: number; dueDate?: string; milestoneId?: string; sortOrder: number; overdue: boolean
}
export interface TimeEntry {
  id: string; employeeId: string; employeeName: string; projectId: string; projectCode: string; projectName: string; taskId?: string; taskKey?: string; taskTitle?: string
  date: string; hours: number; description?: string; billable: boolean; status: TimeEntryStatus; billRate: number; rejectReason?: string; approvedByName?: string
}
export interface TimesheetProject { id: string; code: string; name: string; billable: boolean; tasks: { id: string; key: string; title: string }[] }
export interface MyWeek { weekStart: string; weekEnd: string; employeeId?: string; entries: TimeEntry[]; projects: TimesheetProject[]; totalHours: number; billableHours: number; canSubmit: boolean }
export interface InvoiceResult { invoiceId: string; invoiceNumber?: string; hours: number; total: number; entries: number }
export interface UtilizationRow { employeeId: string; name: string; designation?: string; capacity: number; hours: number; billableHours: number; utilization: number; billableValue: number }
export interface Utilization { from: string; to: string; rows: UtilizationRow[]; capacity: number; hours: number; billableHours: number; utilization: number }
export interface MilestoneDue { projectId: string; projectCode: string; name: string; dueDate: string; amount: number; completed: boolean; overdue: boolean }
export interface ProjectsDashboard {
  activeProjects: number; hoursThisWeek: number; billableThisWeek: number; unbilled: number; billedThisMonth: number; pendingApprovals: number
  atRisk: ProjectListItem[]; milestonesDue: MilestoneDue[]; overdueTasks: Task[]
}

export const PROJECT_COLORS: Record<ProjectStatus, string> = { Planned: 'gold', Active: 'blue', OnHold: 'orange', Completed: 'green', Cancelled: 'red' }
export const ENTRY_COLORS: Record<TimeEntryStatus, string> = { Draft: 'default', Submitted: 'gold', Approved: 'green', Rejected: 'red', Invoiced: 'blue' }
export const PRIORITY_COLORS: Record<WorkItemPriority, string> = { Low: 'default', Medium: 'blue', High: 'orange', Urgent: 'red' }
export const COLUMNS: WorkItemStatus[] = ['Todo', 'InProgress', 'Review', 'Done']
export const words = (s: string) => ({ TimeAndMaterials: 'Time & materials', FixedPrice: 'Fixed price', NonBillable: 'Non-billable', OnHold: 'On hold', InProgress: 'In progress', Todo: 'To do' } as Record<string, string>)[s]
  ?? s.replace(/([a-z])([A-Z])/g, '$1 $2')
export const hrs = (n?: number) => `${+(n ?? 0).toFixed(2)} h`
