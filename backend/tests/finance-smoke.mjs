// End-to-end test of Finance: GST invoices/bills, multi-currency with realized FX, payments, journals,
// statements that must balance, sales tax return, period lock, voids and automatic payroll postings.
// Usage: node tests/finance-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
let failures = 0

async function call(token, method, path, body) {
  const res = await fetch(base + path, {
    method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  return { status: res.status, data: text ? JSON.parse(text) : null }
}
const ok = async (p, label) => { const r = await p; if (r.status >= 400) throw new Error(`${label}: ${r.status} ${JSON.stringify(r.data)}`); return r.data }
function check(cond, label, detail) {
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}${!cond && detail !== undefined ? `  → got ${JSON.stringify(detail)}` : ''}`)
  if (!cond) failures++
}
const login = async (email, password = pw) => (await ok(call(null, 'POST', '/auth/login', { email, password }), `login ${email}`)).accessToken

// ---- setup ----
const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', {
  name: `Fin Test ${run}`, code: `FIN${run}`, industry: 'Travel', maxUsers: 20, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['hr', 'payroll', 'finance'], superAdminName: 'Owner', superAdminEmail: `owner.${run}@fin.test`.toLowerCase(), superAdminPassword: pw,
}), 'tenant')
const owner = await login(`owner.${run}@fin.test`.toLowerCase())
const root = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0]
const E = root.id

const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = code => accounts.find(a => a.code === code).id
check(accounts.length > 40 && accounts.some(a => a.code === '2140'), 'default IFRS chart of accounts seeded')
const taxes = await ok(call(owner, 'GET', '/finance/tax-rates'), 'taxes')
const tax = code => taxes.find(t => t.code === code).id
const settings = await ok(call(owner, 'GET', '/finance/settings'), 'settings')
check(settings.baseCurrency === 'PKR' && settings.fiscalYearStartMonth === 7, 'base currency PKR, fiscal year from July')
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
check(roles.some(r => r.name === 'Accountant'), 'Accountant default role exists')

const usdBank = await ok(call(owner, 'POST', '/finance/accounts', { code: '1125', name: 'Bank - USD account', type: 'Asset', subType: 'Bank', parentId: acc('1100'), isGroup: false, currency: 'USD', isActive: true }), 'usd bank')
await ok(call(owner, 'PUT', '/finance/exchange-rates', { currency: 'USD', date: '2026-09-01', rate: 280 }), 'rate')

const local = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-LHR', name: 'Lahore Travels', isCustomer: true, isVendor: false, ntn: '1234567-8', paymentTermsDays: 30, isActive: true }), 'customer')
const foreign = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-GT', name: 'Global Tours LLC', isCustomer: true, isVendor: false, currency: 'USD', paymentTermsDays: 15, isActive: true }), 'usd customer')
const vendor = await ok(call(owner, 'POST', '/finance/contacts', { code: 'V-OS', name: 'Office Supplies Co', isCustomer: false, isVendor: true, strn: '3277876123456', paymentTermsDays: 0, isActive: true }), 'vendor')

// ---- capital: manual journal, draft → post ----
const badJv = await call(owner, 'POST', '/finance/journals', { entityId: E, date: '2026-09-01', description: 'bad', lines: [
  { accountId: acc('1120'), debit: 100, credit: 0 }, { accountId: acc('3100'), debit: 0, credit: 90 }] })
check(badJv.status === 400, 'unbalanced journal is rejected')
const groupJv = await call(owner, 'POST', '/finance/journals', { entityId: E, date: '2026-09-01', description: 'bad', lines: [
  { accountId: acc('1100'), debit: 100, credit: 0 }, { accountId: acc('3100'), debit: 0, credit: 100 }] })
check(groupJv.status === 400, 'posting to a group account is rejected')
const jv = await ok(call(owner, 'POST', '/finance/journals', { entityId: E, date: '2026-09-01', description: 'Owner capital introduced', lines: [
  { accountId: acc('1120'), debit: 1000000, credit: 0 }, { accountId: acc('3100'), debit: 0, credit: 1000000 }] }), 'jv')
check(jv.status === 'Draft' && jv.number === null, 'manual journal saved as draft without a number')
const editedJv = await ok(call(owner, 'PUT', `/finance/journals/${jv.id}`, { entityId: E, date: '2026-09-01', description: 'Owner capital introduced (edited)', lines: [
  { accountId: acc('1120'), debit: 1000000, credit: 0 }, { accountId: acc('3100'), debit: 0, credit: 1000000 }] }), 'edit jv')
check(editedJv.description.endsWith('(edited)') && editedJv.lines.length === 2, 'a draft journal can be edited (lines replaced)')
const postedJv = await ok(call(owner, 'POST', `/finance/journals/${jv.id}/post`), 'post jv')
check(postedJv.number === 'JV-2027-00001', 'posting numbers it in fiscal year 2027 (Sept 2026)', postedJv.number)

// ---- invoices & bills ----
const inv1 = await ok(call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: local.id, date: '2026-09-05', lines: [
  { description: 'Umrah package handling', accountId: acc('4100'), quantity: 2, unitPrice: 50000, taxRateId: tax('PST-PB') }] }), 'inv1')
check(inv1.subtotal === 100000 && inv1.taxTotal === 16000 && inv1.total === 116000 && inv1.dueDate === '2026-10-05', 'invoice totals with Punjab 16% services tax, due date from terms', [inv1.total, inv1.dueDate])
const inv1a = await ok(call(owner, 'POST', `/finance/invoices/${inv1.id}/approve`), 'approve inv1')
check(inv1a.number === 'INV-2027-00001' && inv1a.status === 'Open' && !!inv1a.journalEntryId, 'approval numbers the invoice and posts its journal', inv1a.number)
const inv1j = await ok(call(owner, 'GET', `/finance/journals/${inv1a.journalEntryId}`), 'inv1 journal')
const jl = (j, code) => j.lines.find(l => l.accountCode === code)
check(jl(inv1j, '1200')?.debit === 116000 && jl(inv1j, '4100')?.credit === 100000 && jl(inv1j, '2190')?.credit === 16000, 'Dr receivable / Cr revenue / Cr provincial tax payable')

const bill = await ok(call(owner, 'POST', '/finance/bills', { entityId: E, contactId: vendor.id, date: '2026-09-10', reference: 'OS-7781', lines: [
  { description: 'Stationery and toner', accountId: acc('6500'), quantity: 1, unitPrice: 20000, taxRateId: tax('GST18') }] }), 'bill')
const billA = await ok(call(owner, 'POST', `/finance/bills/${bill.id}/approve`), 'approve bill')
const billJ = await ok(call(owner, 'GET', `/finance/journals/${billA.journalEntryId}`), 'bill journal')
check(billA.total === 23600 && jl(billJ, '6500')?.debit === 20000 && jl(billJ, '1220')?.debit === 3600 && jl(billJ, '2110')?.credit === 23600, 'bill: Dr expense + Dr input GST 18% / Cr payable')

const inv2 = await ok(call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: foreign.id, date: '2026-09-12', lines: [
  { description: 'Northern areas tour — group of 4', accountId: acc('4100'), quantity: 1, unitPrice: 1000 }] }), 'inv2')
check(inv2.currency === 'USD' && inv2.exchangeRate === 280, 'USD invoice picks the customer currency and the stored rate', [inv2.currency, inv2.exchangeRate])
const inv2a = await ok(call(owner, 'POST', `/finance/invoices/${inv2.id}/approve`), 'approve inv2')
check(inv2a.number === 'INV-2027-00002' && inv2a.baseTotal === 280000, 'second invoice numbered sequentially; base total 280,000', [inv2a.number, inv2a.baseTotal])

// ---- payments ----
const wrongBank = await call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: foreign.id, date: '2026-09-20', bankAccountId: acc('1120'),
  currency: 'USD', exchangeRate: 285, amount: 1000, allocations: [{ documentId: inv2.id, amount: 1000 }] })
check(wrongBank.status === 400, 'USD receipt into a PKR bank account is rejected')
const rcptUsd = await ok(call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: foreign.id, date: '2026-09-20', bankAccountId: usdBank.id,
  exchangeRate: 285, amount: 1000, allocations: [{ documentId: inv2.id, amount: 1000 }] }), 'usd receipt')
const rcptJ = await ok(call(owner, 'GET', `/finance/journals/${rcptUsd.journalEntryId}`), 'receipt journal')
check(jl(rcptJ, '1125')?.baseDebit === 285000 && jl(rcptJ, '1200')?.baseCredit === 280000 && jl(rcptJ, '4910')?.baseCredit === 5000,
  'USD receipt at 285: bank 285,000, receivable cleared at 280,000, realized FX gain 5,000')
check((await ok(call(owner, 'GET', `/finance/invoices/${inv2.id}`), 'inv2')).status === 'Paid', 'USD invoice fully paid')

const partial = await ok(call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: local.id, date: '2026-09-25', bankAccountId: acc('1120'),
  amount: 50000, allocations: [{ documentId: inv1.id, amount: 50000 }] }), 'partial')
const inv1p = await ok(call(owner, 'GET', `/finance/invoices/${inv1.id}`), 'inv1 after')
check(inv1p.status === 'PartiallyPaid' && inv1p.balance === 66000, 'partial receipt leaves 66,000 outstanding', [inv1p.status, inv1p.balance])
const over = await call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: local.id, date: '2026-09-25', bankAccountId: acc('1120'),
  amount: 70000, allocations: [{ documentId: inv1.id, amount: 70000 }] })
check(over.status === 400, 'over-allocation beyond the balance is rejected')
await ok(call(owner, 'POST', '/finance/payments', { kind: 'Payment', entityId: E, contactId: vendor.id, date: '2026-09-26', bankAccountId: acc('1120'),
  amount: 23600, allocations: [{ documentId: bill.id, amount: 23600 }] }), 'pay vendor')

// ---- statements (hand-computed) ----
// Revenue 100,000 + 280,000 = 380,000; FX gain 5,000; office expense 20,000 → profit 365,000.
const q = 'entityId=' + E
const pl = await ok(call(owner, 'GET', `/finance/reports/profit-loss?${q}&from=2026-07-01&to=2026-09-30`), 'pl')
check(pl.result === 365000, 'profit for the quarter = 365,000', pl.result)
const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?${q}&asOf=2026-09-30`), 'tb')
check(tb.totalDebit === tb.totalCredit && tb.totalDebit > 0, 'trial balance balances', [tb.totalDebit, tb.totalCredit])
// Assets: PKR bank 1,026,400 + USD bank 285,000 + receivables 66,000 + input tax 3,600 = 1,381,000
// Equity 1,000,000 + profit 365,000; liabilities: services tax 16,000 → 1,381,000
const bs = await ok(call(owner, 'GET', `/finance/reports/balance-sheet?${q}&asOf=2026-09-30`), 'bs')
const section = t => bs.sections.find(s => s.title === t)?.total
check(section('Total assets') === 1381000 && bs.result === 0, 'balance sheet: total assets 1,381,000 and it balances', [section('Total assets'), bs.result])
const st = await ok(call(owner, 'GET', `/finance/reports/sales-tax?${q}&from=2026-09-01&to=2026-09-30`), 'sales tax')
check(st.totalOutput === 16000 && st.totalInput === 3600 && st.netPayable === 12400, 'sales tax return: output 16,000 − input 3,600 = 12,400', [st.totalOutput, st.totalInput])
const aging = await ok(call(owner, 'GET', `/finance/reports/aging?kind=Invoice&${q}&asOf=2026-10-20`), 'aging')
check(aging.totals.total === 66000 && aging.totals.days1To30 === 66000, 'receivables aging: 66,000 overdue 1–30 days on 20 Oct', aging.totals)
const gl = await ok(call(owner, 'GET', `/finance/reports/general-ledger?accountId=${acc('1120')}&${q}&from=2026-09-01&to=2026-09-30`), 'gl')
check(gl.closing === 1026400 && gl.rows.length === 3, 'general ledger for the PKR bank: 3 entries, closing 1,026,400', [gl.closing, gl.rows.length])

// ---- voids & period lock ----
const voidPaid = await call(owner, 'POST', `/finance/invoices/${inv1.id}/void`, {})
check(voidPaid.status === 400, 'an invoice with payments cannot be voided')
await ok(call(owner, 'POST', `/finance/payments/${partial.id}/void`, {}), 'void payment')
check((await ok(call(owner, 'GET', `/finance/invoices/${inv1.id}`), 'inv1')).status === 'Open', 'voiding the payment reopens the invoice')
await ok(call(owner, 'POST', `/finance/invoices/${inv1.id}/void`, {}), 'void invoice')
const pl2 = await ok(call(owner, 'GET', `/finance/reports/profit-loss?${q}&from=2026-07-01&to=2026-09-30`), 'pl2')
check(pl2.result === 265000, 'voided invoice drops out of profit (365,000 − 100,000)', pl2.result)

await ok(call(owner, 'PUT', '/finance/settings', { ...settings, lockedThrough: '2026-08-31' }), 'lock')
const lockedInv = await call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: local.id, date: '2026-08-15', lines: [{ description: 'Late', accountId: acc('4100'), quantity: 1, unitPrice: 100 }] })
check(lockedInv.status === 400, 'documents dated in a closed period are rejected')

// ---- payroll posts to the ledger ----
// Employee 100,000: taxable 93,333 × 10 = 933,330 → 3,333 → 333/month; EOBI 407 / 2,035; net 99,260.
const emp = await ok(call(owner, 'POST', '/hr/employees', { data: { employeeCode: 'E1', fullName: 'Asad Accountant', entityId: E, employmentType: 'Permanent',
  status: 'Active', joinDate: '2026-01-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false },
  email: `asad.${run}@fin.test`.toLowerCase(), password: pw, userType: 'Employee', monthlyGross: 100000 }), 'employee')
const prun = await ok(call(owner, 'POST', '/payroll/runs', { entityId: E, year: 2026, month: 9, includeSubEntities: true }), 'payroll run')
await ok(call(owner, 'POST', `/payroll/runs/${prun.run.id}/approve`), 'approve payroll')
const posted = await ok(call(owner, 'POST', `/payroll/runs/${prun.run.id}/post`), 'post payroll')
check(!!posted.run.journalEntryId, 'posting payroll creates the accrual journal')
const pj = await ok(call(owner, 'GET', `/finance/journals/${posted.run.journalEntryId}`), 'payroll journal')
check(jl(pj, '6100')?.debit === 100000 && jl(pj, '6110')?.debit === 2035 && jl(pj, '2140')?.credit === 333 &&
  jl(pj, '2150')?.credit === 2442 && jl(pj, '2130')?.credit === 99260, 'payroll journal: salaries, EOBI, tax withheld, net payable',
  pj.lines.map(l => [l.accountCode, l.debit, l.credit]))
await ok(call(owner, 'POST', `/finance/payments/payroll/${prun.run.id}`, { bankAccountId: acc('1120'), date: '2026-10-01' }), 'pay salaries')
const tb2 = await ok(call(owner, 'GET', `/finance/reports/trial-balance?${q}&asOf=2026-10-01`), 'tb2')
check(!tb2.rows.some(r => r.code === '2130') && tb2.totalDebit === tb2.totalCredit, 'after paying salaries, salaries payable is cleared and TB still balances')

const empTok = await login(`asad.${run}@fin.test`.toLowerCase())
const peek = await call(empTok, 'GET', '/finance/journals')
check(peek.status === 200 && peek.data.total === 0, 'an employee sees no journals')
const peekReport = await call(empTok, 'GET', '/finance/reports/trial-balance')
check(peekReport.status === 403, 'an employee cannot open financial reports')

console.log(failures === 0 ? '\nALL FINANCE CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
