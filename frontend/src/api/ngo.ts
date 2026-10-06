// Types for the NGO module (donors, funds, grants, beneficiaries).
export type FundKind = 'Unrestricted' | 'Restricted' | 'Zakat' | 'Endowment'
export type DonorType = 'Individual' | 'Corporate' | 'Foundation' | 'Institutional' | 'Government'
export type GrantStatus = 'Proposal' | 'Active' | 'Closed' | 'Cancelled'
export type ReportingFrequency = 'Monthly' | 'Quarterly' | 'SemiAnnual' | 'Annual' | 'EndOnly'
export type BudgetCategory = 'Personnel' | 'Activities' | 'Assistance' | 'Equipment' | 'Travel' | 'Administration' | 'Other'
export type DonationMethod = 'Cash' | 'BankTransfer' | 'Cheque' | 'Online'
export type FunctionalCategory = 'Program' | 'ManagementGeneral' | 'Fundraising'
export type Gender = 'Male' | 'Female' | 'Other'
export type AssistanceType = 'Cash' | 'InKind' | 'Service'

export interface Donor {
  id: string; contactId: string; code: string; name: string; type: DonorType; email?: string; phone?: string; cnic?: string; ntn?: string; address?: string
  city?: string; country?: string; notes?: string; totalDonated: number; donations: number; lastDonation?: string; grants: number; grantsReceived: number
}
export interface Fund { id: string; code: string; name: string; kind: FundKind; purpose?: string; grantId?: string; grantNumber?: string; isActive: boolean; received: number; spent: number; balance: number }
export interface NgoProgram { id: string; entityId: string; entityName: string; code: string; name: string; sector?: string; description?: string; targetBeneficiaries: number; isActive: boolean; beneficiaries: number; spent: number }
export interface GrantListItem {
  id: string; number: string; title: string; donorName: string; currency: string; amount: number; startDate: string; endDate: string; status: GrantStatus
  receivedBase: number; spentBase: number; burnPercent: number; timeElapsedPercent: number; nextReportDue?: string
}
export interface BudgetLine { id: string; code: string; description: string; category: BudgetCategory; amount: number; expenseAccountId?: string; expenseAccountName?: string; actualBase: number; actual: number; variance: number; burnPercent: number; overBudget: boolean }
export interface Tranche { id: string; sequence: number; dueDate: string; amount: number; condition?: string; receivedDate?: string; receivedAmount?: number; receivedBase?: number; overdue: boolean }
export interface GrantReport { id: string; title: string; periodStart: string; periodEnd: string; dueDate: string; submittedOn?: string; notes?: string; overdue: boolean }
export interface FundExpense { id: string; number: string; date: string; fundCode: string; grantNumber?: string; budgetLineCode?: string; programName?: string; function: FunctionalCategory; accountName: string; description: string; amount: number; paidHow: string; billId?: string; assistanceId?: string }
export interface Grant {
  id: string; number: string; entityId: string; entityName: string; title: string; donorId: string; donorName: string; agreementRef?: string; programId?: string
  programName?: string; fundId: string; fundCode: string; currency: string; agreementRate: number; startDate: string; endDate: string; status: GrantStatus
  reportingFrequency: ReportingFrequency; flexibilityPercent: number; notes?: string; amount: number; receivedAmount: number; receivedBase: number; spentBase: number
  spent: number; averageRate: number; unspentBase: number; burnPercent: number; timeElapsedPercent: number; budgetLines: BudgetLine[]; tranches: Tranche[]
  reports: GrantReport[]; expenses: FundExpense[]; warnings: string[]
}
export interface Donation {
  id: string; number: string; entityId: string; entityName: string; date: string; donorId: string; donorName: string; donorCnic?: string; donorNtn?: string
  donorAddress?: string; fundId: string; fundName: string; fundKind: FundKind; amount: number; amountInWords: string; method: DonationMethod; reference?: string
  bankAccountName: string; programName?: string; notes?: string; organizationName: string
}
export interface DonationListItem { id: string; number: string; date: string; donorName: string; fundCode: string; fundKind: FundKind; amount: number; method: DonationMethod; reference?: string }
export interface BeneficiaryListItem { id: string; registrationNo: string; fullName: string; cnic?: string; gender: Gender; district?: string; householdSize: number; programName?: string; zakatEligible: boolean; isActive: boolean; enrolledOn: string; assistanceCount: number; assistanceValue: number; lastAssisted?: string }
export interface AssistanceRow { id: string; date: string; type: AssistanceType; description: string; quantity?: number; value: number; programName?: string; fundCode?: string; expenseNumber?: string }
export interface Beneficiary {
  id: string; entityId: string; entityName: string; registrationNo: string; fullName: string; cnic?: string; gender: Gender; dateOfBirth?: string; age?: number
  phone?: string; district?: string; address?: string; householdSize: number; vulnerabilities?: string; zakatEligible: boolean; programId?: string; programName?: string
  enrolledOn: string; isActive: boolean; assistance: AssistanceRow[]; assistanceValue: number
}
export interface FunctionalRow { program: string; programCost: number; managementGeneral: number; fundraising: number; total: number }
export interface FunctionalExpenses { from: string; to: string; rows: FunctionalRow[]; programCost: number; managementGeneral: number; fundraising: number; total: number; programRatio: number }
export interface DonorSummaryRow { donorId: string; donorName: string; type: DonorType; donations: number; gifts: number; grantsCommitted: number; grantsReceived: number }
export interface DueItem { kind: string; grantId: string; grantNumber: string; title: string; dueDate: string; amount?: number; overdue: boolean }
export interface NgoDashboard {
  donationsThisMonth: number; donorsThisMonth: number; activeGrants: number; committedBase: number; receivedBase: number; spentBase: number
  activeBeneficiaries: number; assistedThisMonth: number; programRatio: number; funds: Fund[]; grants: GrantListItem[]; dueSoon: DueItem[]
}

export const GRANT_COLORS: Record<GrantStatus, string> = { Proposal: 'gold', Active: 'blue', Closed: 'green', Cancelled: 'red' }
export const FUND_COLORS: Record<FundKind, string> = { Unrestricted: 'green', Restricted: 'blue', Zakat: 'purple', Endowment: 'gold' }
export const BUDGET_CATEGORIES: BudgetCategory[] = ['Personnel', 'Activities', 'Assistance', 'Equipment', 'Travel', 'Administration', 'Other']
export const DONOR_TYPES: DonorType[] = ['Individual', 'Corporate', 'Foundation', 'Institutional', 'Government']
export const words = (s: string) => ({ ManagementGeneral: 'Management & general', InKind: 'In kind', BankTransfer: 'Bank transfer', SemiAnnual: 'Semi-annual', EndOnly: 'End of grant only' } as Record<string, string>)[s]
  ?? s.replace(/([a-z])([A-Z])/g, '$1 $2')
