// Withholding tax on supplier payments (s.153: computed ex-sales-tax, doubled for non-ATL suppliers, deposited with a CPR,
// certificates) and bank reconciliation (CSV import, auto-match, bank charges from the statement, deposits in transit and
// outstanding cheques, completion only when the books agree).
// Usage: node tests/compliance-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1xYz`
const mail = n => `${n}.${run}@cmp.test`.toLowerCase()
const day = n => new Date(Date.now() + 5 * 3600_000 + n * 86400_000).toISOString().slice(0, 10)
const today = day(0)
let failures = 0

async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  return { status: res.status, data: text ? JSON.parse(text) : null }
}
const ok = async (p, label) => { const r = await p; if (r.status >= 400) throw new Error(`${label}: ${r.status} ${JSON.stringify(r.data)}`); return r.data }
function check(cond, label, detail) {
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}${!cond && detail !== undefined ? `  → got ${JSON.stringify(detail)}` : ''}`)
  if (!cond) failures++
}
const login = async (email, password = pw) => (await ok(call(null, 'POST', '/auth/login', { email, password }), `login ${email}`)).accessToken

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', { name: `Compliance ${run}`, code: `CMP${run}`, industry: 'General', maxUsers: 10, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c)?.id
const taxes = await ok(call(owner, 'GET', '/finance/tax-rates'), 'taxes')
const pst = taxes.find(t => t.code === 'PST-PB').id
const rates = await ok(call(owner, 'GET', '/finance/withholding/rates'), 'wht rates')
const rate = c => rates.find(r => r.code === c)
check(!!acc('2185') && rate('WHT-SVC-CO')?.rate === 0.09 && rate('WHT-GOODS-OTH')?.rate === 0.055, 'withholding payable account and s.153 default rates seeded')

const contact = b => ok(call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 30, isActive: true, isCustomer: false, isVendor: true, ...b }), b.code)
const v1 = await contact({ code: 'V-SOFT', name: 'Lahore Software (Pvt) Ltd', ntn: '1234567-8' })
const v2 = await contact({ code: 'V-TRADER', name: 'Bashir Traders', cnic: '35202-1111111-1', notOnActiveTaxpayerList: true })
const cust = await contact({ code: 'C-1', name: 'Customer', isCustomer: true, isVendor: false })
const bill = async (vendor, lines) => {
  const b = await ok(call(owner, 'POST', '/finance/bills', { entityId: E, contactId: vendor.id, date: today, lines }), 'bill')
  return ok(call(owner, 'POST', `/finance/bills/${b.id}/approve`), 'approve bill')
}
const pay = (vendor, docs, amount, whtCode, extra = {}) => call(owner, 'POST', '/finance/payments', { kind: 'Payment', entityId: E, contactId: vendor.id, date: today,
  bankAccountId: acc('1120'), amount, allocations: docs, withholdingTaxRateId: whtCode ? rate(whtCode).id : undefined, ...extra })

// ---- withholding ----
const b1 = await bill(v1, [{ description: 'ERP implementation', accountId: acc('6600'), quantity: 1, unitPrice: 100000, taxRateId: pst }])
check(b1.total === 116000, 'services bill 100,000 + 16% PST = 116,000', b1.total)
const p1 = await ok(pay(v1, [{ documentId: b1.id, amount: 116000 }], 116000, 'WHT-SVC-CO'), 'p1')
check(p1.withholdingTax === 9000 && p1.netPaid === 107000 && p1.withholdingSection === '153(1)(b)', 'services: 9% on 100,000 (ex-PST) = 9,000 withheld, 107,000 paid', [p1.withholdingTax, p1.netPaid])
const b2 = await bill(v2, [{ description: 'Office furniture', accountId: acc('6500'), quantity: 1, unitPrice: 50000 }])
const p2 = await ok(pay(v2, [{ documentId: b2.id, amount: 50000 }], 50000, 'WHT-GOODS-OTH'), 'p2')
check(p2.withholdingTax === 5500 && p2.netPaid === 44500, 'goods from a non-ATL individual: 5.5% doubled to 11% = 5,500', [p2.withholdingTax, p2.netPaid])
const b3 = await bill(v1, [{ description: 'Support retainer', accountId: acc('6600'), quantity: 1, unitPrice: 20000 }])
const p3 = await ok(pay(v1, [{ documentId: b3.id, amount: 10000 }], 10000, 'WHT-SVC-CO'), 'p3')
const b3after = await ok(call(owner, 'GET', `/finance/bills/${b3.id}`), 'b3')
check(p3.withholdingTax === 900 && p3.netPaid === 9100 && b3after.amountPaid === 10000 && b3after.status === 'PartiallyPaid',
  'part payment: withholding on the part paid (900); the bill shows 10,000 settled', [p3.withholdingTax, b3after.amountPaid, b3after.status])
const inv = await ok(call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: cust.id, date: today, lines: [{ description: 'Sale', accountId: acc('4100'), quantity: 1, unitPrice: 30000 }] }), 'inv')
await ok(call(owner, 'POST', `/finance/invoices/${inv.id}/approve`), 'approve inv')
const badReceipt = await call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: cust.id, date: today, bankAccountId: acc('1120'), amount: 30000,
  allocations: [{ documentId: inv.id, amount: 30000 }], withholdingTaxRateId: rate('WHT-SVC-CO').id })
check(badReceipt.status === 400, 'withholding is only deducted on payments to suppliers')
const rcpt = await ok(call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: cust.id, date: today, bankAccountId: acc('1120'), amount: 30000,
  allocations: [{ documentId: inv.id, amount: 30000 }] }), 'receipt')

const monthStart = today.slice(0, 8) + '01'
const ded = await ok(call(owner, 'GET', `/finance/withholding/deductions?from=${monthStart}&to=${today}`), 'deductions')
check(ded.deductions.length === 3 && ded.totalBase === 160000 && ded.totalTax === 15400 && ded.undeposited === 15400, 'register: 3 deductions, base 160,000, tax 15,400 to deposit',
  [ded.deductions.length, ded.totalBase, ded.totalTax])
const cert = await ok(call(owner, 'GET', `/finance/withholding/certificate?vendorId=${v1.id}&from=${monthStart}&to=${today}`), 'certificate')
check(cert.totalBase === 110000 && cert.totalTax === 9900 && cert.vendorNtn === '1234567-8', 'certificate for Lahore Software: 9,900 withheld on 110,000', [cert.totalBase, cert.totalTax])
const [y, m] = today.split('-').map(Number)
const dep = await ok(call(owner, 'POST', '/finance/withholding/deposits', { entityId: E, year: y, month: m, date: today, bankAccountId: acc('1120'), cprNumber: 'CPR-2026-778899' }), 'deposit')
check(dep.amount === 15400 && dep.deductions === 3, 'deposit with FBR: 15,400 covering 3 deductions (CPR recorded)', dep)
const again = await call(owner, 'POST', '/finance/withholding/deposits', { entityId: E, year: y, month: m, date: today, bankAccountId: acc('1120'), cprNumber: 'CPR-X' })
check(again.status === 400, 'nothing left to deposit for the month')
const voidPaid = await call(owner, 'POST', `/finance/payments/${p1.id}/void`, { reason: 'test' })
check(voidPaid.status === 400, "a payment whose tax was already deposited can't be voided")

let tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb')
let row = c => tb.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }
// Bank: −107,000 −44,500 −9,100 +30,000 −15,400 = −146,000. Payables: 186,000 billed − 176,000 settled = 10,000.
check(row('2185').debit === 0 && row('2185').credit === 0 && row('2110').credit === 10000 && row('1120').credit === 146000 && tb.totalDebit === tb.totalCredit,
  'ledger: withholding payable cleared, payables 10,000, bank −146,000', ['2185', '2110', '1120'].map(c => [c, row(c).debit, row(c).credit]))

// ---- bank reconciliation ----
// The statement shows three of the four payments (Bashir's cheque not yet presented), not the customer deposit, plus a 500 charge.
const csv = ['Date,Description,Reference,Debit,Credit',
  `${today},"Cheque paid, Lahore Software",${p1.number},107000,`,
  `${today},Cheque paid,${p3.number},9100,`,
  `${today},FBR e-payment,CPR-2026-778899,15400,`,
  `${today},Bank charges,,500,`].join('\n')
let rec = await ok(call(owner, 'POST', '/finance/reconciliations', { entityId: E, bankAccountId: acc('1120'), statementDate: today, statementBalance: -132000, csv }), 'rec')
check(rec.lines.length === 4 && rec.lines[0].amount === -107000, 'statement CSV imported (debit/credit columns, quoted text)', rec.lines.map(l => l.amount))
const auto = await ok(call(owner, 'POST', `/finance/reconciliations/${rec.id}/auto-match`), 'auto')
rec = auto.reconciliation
// Books −146,000; statement −132,000 + 30,000 in transit − 44,500 unpresented = −146,500 → 500 short (the charge).
check(auto.matched === 3 && rec.unmatchedLines === 1 && rec.depositsInTransit === 30000 && rec.outstandingPayments === 44500 && rec.difference === 500,
  'auto-match pairs 3 lines; deposit in transit 30,000, unpresented cheque 44,500, 500 unexplained', [auto.matched, rec.depositsInTransit, rec.outstandingPayments, rec.difference])
const early = await call(owner, 'POST', `/finance/reconciliations/${rec.id}/complete`)
check(early.status === 400, "can't complete while the books and the statement disagree")
const charge = rec.lines.find(l => l.journalLineId == null)
rec = await ok(call(owner, 'POST', `/finance/reconciliations/${rec.id}/create-entry`, { statementLineId: charge.id, accountId: acc('6800'), description: 'Bank charges — September' }), 'charge')
check(rec.difference === 0 && rec.unmatchedLines === 0 && rec.canComplete, 'posting the bank charge from the statement line reconciles to zero', [rec.difference, rec.unmatchedLines])
rec = await ok(call(owner, 'POST', `/finance/reconciliations/${rec.id}/complete`), 'complete')
check(rec.status === 'Completed', 'reconciliation completed')

// Next statement: the deposit and Bashir's cheque clear.
const csv2 = ['Date,Description,Reference,Amount', `${day(1)},Deposit,,30000`, `${day(1)},Cheque paid,${p2.number},-44500`].join('\n')
let rec2 = await ok(call(owner, 'POST', '/finance/reconciliations', { entityId: E, bankAccountId: acc('1120'), statementDate: day(1), statementBalance: -146500, csv: csv2 }), 'rec2')
check(rec2.bookItems.filter(b => !b.matched).length === 2, 'the next reconciliation only offers the two items still outstanding', rec2.bookItems.length)
rec2 = (await ok(call(owner, 'POST', `/finance/reconciliations/${rec2.id}/auto-match`), 'auto2')).reconciliation
check(rec2.difference === 0 && rec2.canComplete, 'second statement reconciles (single Amount column)', [rec2.difference, rec2.unmatchedLines])

tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb2')
row = c => tb.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }
check(row('6800').debit === 500 && row('1120').credit === 146500 && tb.totalDebit === tb.totalCredit, 'ledger: bank charges 500, bank −146,500, books balance')

console.log(failures === 0 ? '\nALL COMPLIANCE CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
