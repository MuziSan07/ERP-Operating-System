// Data-integrity checks: concurrent double-clicks must not double-post, a failure midway must leave nothing behind,
// and voiding an invoice must release what it billed.
// Usage: node tests/integrity-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@int.test`.toLowerCase()
const iso = d => d.toISOString().slice(0, 10)
const today = new Date(Date.now() + 5 * 3600_000)
const day = n => iso(new Date(today.getTime() + n * 86400_000))
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
const succeeded = rs => rs.filter(r => r.status < 400).length

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', { name: `Integrity ${run}`, code: `INT${run}`, industry: 'General', maxUsers: 20, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['finance', 'projects', 'hr', 'payroll', 'logistics'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c).id
const customer = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-1', name: 'Customer One', isCustomer: true, isVendor: false, isActive: true, paymentTermsDays: 30 }), 'customer')
const tb = async () => { const t = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb'); return { row: c => t.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }, t } }

// 1. Double-click on Approve.
const draft = await ok(call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: customer.id, date: day(0), lines: [{ description: 'Consulting', accountId: acc('4100'), quantity: 1, unitPrice: 1000 }] }), 'draft')
const approvals = await Promise.all([1, 2, 3].map(() => call(owner, 'POST', `/finance/invoices/${draft.id}/approve`)))
let books = await tb()
check(succeeded(approvals) === 1 && books.row('1200').debit === 1000, 'three simultaneous approvals post the invoice once (receivables 1,000)',
  [approvals.map(r => r.status), books.row('1200').debit])

// 2. Two receipts racing for the same invoice.
const receipt = () => call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E, contactId: customer.id, date: day(0), bankAccountId: acc('1120'), amount: 1000,
  allocations: [{ documentId: draft.id, amount: 1000 }] })
const receipts = await Promise.all([receipt(), receipt()])
books = await tb()
const inv = await ok(call(owner, 'GET', `/finance/invoices/${draft.id}`), 'invoice')
check(succeeded(receipts) === 1 && books.row('1200').debit === 0 && books.row('1200').credit === 0 && books.row('1120').debit === 1000 && inv.amountPaid === 1000,
  'two simultaneous 1,000 receipts on a 1,000 invoice: only one is accepted and the ledger matches the invoice',
  [receipts.map(r => r.status), books.row('1200'), books.row('1120').debit, inv.amountPaid])

// 3. A failure midway leaves nothing behind (prepaid booking without the account it was paid into).
const route = await ok(call(owner, 'POST', '/logistics/routes', { code: 'A-B', origin: 'A', destination: 'B', distanceKm: 100, standardHours: 3, ratePerKg: 10, minimumCharge: 0, fullTruckRate: 0, fuelSurchargePercent: 0, isActive: true }), 'route')
const bad = await call(owner, 'POST', '/logistics/shipments', { entityId: E, customerId: customer.id, bookingDate: day(0), shipperName: 'S', consigneeName: 'C', originCity: 'A',
  destinationCity: 'B', routeId: route.id, service: 'PartLoad', pieces: 1, weightKg: 10, declaredValue: 0, paymentMode: 'Prepaid', otherCharges: 0, codAmount: 0 })
const cns = await ok(call(owner, 'GET', '/logistics/shipments?pageSize=50'), 'shipments')
check(bad.status === 400 && cns.total === 0, 'a booking rejected halfway is not left half-saved', [bad.status, cns.total])

// 4. Two people invoicing the same project at once; then voiding the invoice frees the hours.
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const dev = await ok(call(owner, 'POST', '/hr/employees', { data: { employeeCode: 'E-1', fullName: 'Dev One', entityId: E, employmentType: 'Permanent', status: 'Active', joinDate: '2025-01-01',
  eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email: mail('dev'), password: pw, userType: 'Employee', roleId: roles.find(r => r.name === 'Employee').id, monthlyGross: 176000 }), 'dev')
const devToken = await login(mail('dev'))
const devId = dev.id ?? dev.employee?.id
let project = await ok(call(owner, 'POST', '/projects', { entityId: E, code: 'P1', name: 'Project', clientId: customer.id, billingType: 'TimeAndMaterials', startDate: day(-30),
  budgetHours: 0, contractAmount: 0, defaultBillRate: 2000, members: [{ employeeId: devId }], milestones: [] }), 'project')
await ok(call(owner, 'POST', `/projects/${project.id}/status/Active`), 'activate')
const entry = await ok(call(devToken, 'POST', '/timesheets/entries', { projectId: project.id, date: day(-1), hours: 5, billable: true }), 'time')
await ok(call(devToken, 'POST', `/timesheets/me/submit?date=${day(-1)}`), 'submit')
await ok(call(owner, 'POST', '/timesheets/approve', { entryIds: [entry.id] }), 'approve time')
const bills = await Promise.all([1, 2].map(() => call(owner, 'POST', `/projects/${project.id}/invoice`, {})))
books = await tb()
check(succeeded(bills) === 1 && books.row('4100').credit === 11000, 'two simultaneous "invoice project" clicks bill the 5 hours once (revenue 1,000 + 10,000)',
  [bills.map(r => r.status), books.row('4100').credit])
const invoiceId = bills.find(r => r.status < 400)?.data?.invoiceId
await ok(call(owner, 'POST', `/finance/invoices/${invoiceId}/void`, { reason: 'Wrong client' }), 'void')
project = await ok(call(owner, 'GET', `/projects/${project.id}`), 'project')
const rebill = await call(owner, 'POST', `/projects/${project.id}/invoice`, {})
check(project.unbilled === 10000 && rebill.status === 200 && rebill.data.hours === 5, 'voiding the invoice puts the hours back to be billed again', [project.unbilled, rebill.status, rebill.data])

books = await tb()
check(books.t.totalDebit === books.t.totalCredit, 'books balance')
console.log(failures === 0 ? '\nALL INTEGRITY CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
