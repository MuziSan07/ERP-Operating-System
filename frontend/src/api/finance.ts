// Types and helpers for Finance.
export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'
export type AccountSubType =
  | 'Cash' | 'Bank' | 'Receivable' | 'TaxReceivable' | 'Inventory' | 'Prepayment' | 'OtherCurrentAsset' | 'FixedAsset'
  | 'AccumulatedDepreciation' | 'IntangibleAsset' | 'OtherNonCurrentAsset' | 'Payable' | 'TaxPayable' | 'PayrollLiability'
  | 'AccruedLiability' | 'DeferredIncome' | 'OtherCurrentLiability' | 'NonCurrentLiability' | 'Capital' | 'RetainedEarnings'
  | 'Reserves' | 'Revenue' | 'OtherIncome' | 'CostOfSales' | 'OperatingExpense' | 'PayrollExpense' | 'Depreciation'
  | 'FinanceCost' | 'TaxExpense' | 'Group'
export type JournalStatus = 'Draft' | 'Posted' | 'Reversed'
export type JournalSource = 'Manual' | 'Invoice' | 'Bill' | 'Payment' | 'Payroll' | 'Reversal' | 'Inventory' | 'GoodsReceipt' | 'Ngo'
export type DocumentKind = 'Invoice' | 'Bill'
export type DocumentStatus = 'Draft' | 'Open' | 'PartiallyPaid' | 'Paid' | 'Void'
export type PaymentKind = 'Receipt' | 'Payment'

export const SUBTYPES_BY_TYPE: Record<AccountType, AccountSubType[]> = {
  Asset: ['Cash', 'Bank', 'Receivable', 'TaxReceivable', 'Inventory', 'Prepayment', 'OtherCurrentAsset', 'FixedAsset', 'AccumulatedDepreciation', 'IntangibleAsset', 'OtherNonCurrentAsset'],
  Liability: ['Payable', 'TaxPayable', 'PayrollLiability', 'AccruedLiability', 'DeferredIncome', 'OtherCurrentLiability', 'NonCurrentLiability'],
  Equity: ['Capital', 'RetainedEarnings', 'Reserves'],
  Income: ['Revenue', 'OtherIncome'],
  Expense: ['CostOfSales', 'OperatingExpense', 'PayrollExpense', 'Depreciation', 'FinanceCost', 'TaxExpense'],
}
export const splitWords = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')

export interface Account { id: string; code: string; name: string; type: AccountType; subType: AccountSubType; parentId?: string; isGroup: boolean; isSystem: boolean; currency?: string; description?: string; isActive: boolean }
export interface FinanceSettings {
  baseCurrency: string; fiscalYearStartMonth: number; lockedThrough?: string | null; ntn?: string; strn?: string
  receivableAccountId?: string; payableAccountId?: string; retainedEarningsAccountId?: string; exchangeGainLossAccountId?: string
  salaryExpenseAccountId?: string; salaryPayableAccountId?: string; salaryTaxPayableAccountId?: string; eobiExpenseAccountId?: string
  eobiPayableAccountId?: string; pfExpenseAccountId?: string; pfPayableAccountId?: string; socialSecurityExpenseAccountId?: string
  socialSecurityPayableAccountId?: string; otherPayrollDeductionsAccountId?: string
}
export interface ExchangeRate { id: string; currency: string; date: string; rate: number }
export interface TaxRate { id: string; code: string; name: string; rate: number; authority?: string; outputAccountId?: string; inputAccountId?: string; isActive: boolean }
export interface Contact {
  id: string; code: string; name: string; isCustomer: boolean; isVendor: boolean; email?: string; phone?: string; address?: string
  city?: string; country?: string; ntn?: string; strn?: string; cnic?: string; currency?: string; paymentTermsDays: number; isActive: boolean
  receivable: number; payable: number
}
export interface JournalLine { accountId: string; accountCode: string; accountName: string; entityId: string; entityName?: string; description?: string; debit: number; credit: number; baseDebit: number; baseCredit: number; contactId?: string; contactName?: string }
export interface JournalEntry {
  id: string; number?: string; entityId: string; entityName: string; date: string; reference?: string; description: string
  status: JournalStatus; source: JournalSource; sourceId?: string; currency: string; exchangeRate: number; total: number; baseTotal: number
  createdByName?: string; postedByName?: string; postedAt?: string; reversalOfId?: string; reversedById?: string; lines: JournalLine[]
}
export interface DocumentLine { id: string; description: string; accountId: string; accountName: string; quantity: number; unitPrice: number; amount: number; taxRateId?: string; taxRateName?: string; taxRate: number; taxAmount: number }
export interface FinanceDocument {
  id: string; kind: DocumentKind; number?: string; entityId: string; entityName: string; contactId: string; contactName: string
  contactNtn?: string; contactStrn?: string; contactAddress?: string; date: string; dueDate: string; reference?: string; notes?: string
  currency: string; exchangeRate: number; status: DocumentStatus; subtotal: number; taxTotal: number; total: number; amountPaid: number
  balance: number; baseTotal: number; journalEntryId?: string; createdAt: string; lines: DocumentLine[]
}
export interface DocumentListItem { id: string; kind: DocumentKind; number?: string; entityName: string; contactId: string; contactName: string; date: string; dueDate: string; reference?: string; currency: string; status: DocumentStatus; total: number; balance: number; daysOverdue: number }
export interface Payment {
  id: string; kind: PaymentKind; number: string; entityId: string; entityName: string; contactId: string; contactName: string; date: string
  bankAccountId: string; bankAccountName: string; currency: string; exchangeRate: number; amount: number; reference?: string; notes?: string
  isVoid: boolean; journalEntryId?: string; allocations: { documentId: string; documentNumber?: string; amount: number; baseAmount: number }[]
}
export interface TrialBalance { asOf: string; currency: string; rows: { accountId: string; code: string; name: string; type: AccountType; debit: number; credit: number }[]; totalDebit: number; totalCredit: number }
export interface StatementSection { title: string; lines: { code: string; name: string; amount: number }[]; total: number }
export interface FinancialStatement { title: string; from: string; to: string; currency: string; sections: StatementSection[]; result: number; resultLabel: string }
export interface GeneralLedger { accountId: string; code: string; name: string; from: string; to: string; opening: number; rows: { date: string; number?: string; journalEntryId: string; description: string; entityName?: string; contactName?: string; debit: number; credit: number; balance: number }[]; closing: number }
export interface AgingRow { contactId: string; contactName: string; current: number; days1To30: number; days31To60: number; days61To90: number; over90: number; total: number }
export interface Aging { kind: DocumentKind; asOf: string; currency: string; rows: AgingRow[]; totals: AgingRow }
export interface SalesTaxReport { from: string; to: string; currency: string; rows: { taxRateId: string; code: string; name: string; authority?: string; rate: number; salesValue: number; outputTax: number; purchaseValue: number; inputTax: number; net: number }[]; totalOutput: number; totalInput: number; netPayable: number }
export interface FinanceDashboard { currency: string; cashAndBank: number; receivables: number; payables: number; overdueReceivables: number; incomeThisYear: number; expensesThisYear: number; profitThisYear: number; monthly: { year: number; month: number; income: number; expenses: number }[] }

export const DOC_COLORS: Record<DocumentStatus, string> = { Draft: 'default', Open: 'blue', PartiallyPaid: 'gold', Paid: 'green', Void: 'red' }
export const JOURNAL_COLORS: Record<JournalStatus, string> = { Draft: 'default', Posted: 'green', Reversed: 'red' }
// Lakh/crore grouping as used in Pakistani accounts: 12,50,000.00 (en-IN has the same digit grouping as en-PK practice).
export const amount = (n?: number, digits = 2) => (n ?? 0).toLocaleString('en-IN', { minimumFractionDigits: digits, maximumFractionDigits: digits })
