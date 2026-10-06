// End-to-end test of HR & Payroll: employee logins, multi-step leave, attendance, Pakistan payroll math.
// Usage: node tests/hr-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@hr.test`.toLowerCase()
let failures = 0

async function call(token, method, path, body) {
  const res = await fetch(base + path, {
    method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  return { status: res.status, data: text ? JSON.parse(text) : null }
}
const ok = async (p, label) => {
  const r = await p
  if (r.status >= 400) throw new Error(`${label}: ${r.status} ${JSON.stringify(r.data)}`)
  return r.data
}
function check(cond, label, detail) {
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}${!cond && detail !== undefined ? `  → got ${JSON.stringify(detail)}` : ''}`)
  if (!cond) failures++
}
const login = async (email) => (await ok(call(null, 'POST', '/auth/login', { email, password: pw }), `login ${email}`)).accessToken
const empData = (code, name, entityId, extra = {}) => ({
  employeeCode: code, fullName: name, entityId, employmentType: 'Permanent', status: 'Active', joinDate: '2026-01-01',
  eobiMember: true, providentFundMember: false, socialSecurityMember: false, iban: 'PK36SCBL0000001123456702', ...extra,
})

// ---- setup: organization with an HR officer, a manager and an employee ----
const platform = (await ok(call(null, 'POST', '/auth/login', { email: cfg.Seed.PlatformAdminEmail, password: cfg.Seed.PlatformAdminPassword }), 'platform login')).accessToken
await ok(call(platform, 'POST', '/platform/tenants', {
  name: `HR Test ${run}`, code: `HRT${run}`, industry: 'SoftwareServices', maxUsers: 20, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['hr', 'payroll'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw,
}), 'create tenant')
const owner = await login(mail('owner'))
const root = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0]

const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const hrRole = roles.find(r => r.name === 'HR Officer')
const managerRole = roles.find(r => r.name === 'Manager')
check(!!hrRole && !managerRole.permissions.some(p => p.startsWith('payroll.')), 'default roles: HR Officer exists, Manager has no payroll access')

const dept = await ok(call(owner, 'POST', '/hr/departments', { entityId: root.id, name: 'Engineering', code: 'ENG' }), 'department')
const hr = await ok(call(owner, 'POST', '/hr/employees', {
  data: empData('HR-001', 'Hina HR', root.id, { gender: 'Female' }), email: mail('hr'), password: pw, userType: 'Admin', roleId: hrRole.id, monthlyGross: 120000,
}), 'create HR officer')
const mgr = await ok(call(owner, 'POST', '/hr/employees', {
  data: empData('EMP-001', 'Majid Manager', root.id, { departmentId: dept.id, gender: 'Male' }), email: mail('mgr'), password: pw,
  userType: 'Manager', roleId: managerRole.id, monthlyGross: 300000,
}), 'create manager')

const hrTok = await login(mail('hr'))
const emp = await ok(call(hrTok, 'POST', '/hr/employees', {
  data: empData('EMP-002', 'Eman Employee', root.id, { departmentId: dept.id, managerId: mgr.id, gender: 'Female', cnic: '35202-1234567-8' }),
  email: mail('emp'), password: pw, userType: 'Employee', monthlyGross: 150000,
}), 'HR creates employee')
check(emp.managerName === 'Majid Manager', 'HR officer creates an employee with a login and line manager')

const empTok = await login(mail('emp'))
const empMe = await ok(call(empTok, 'GET', '/auth/me'), 'emp me')
check(empMe.entities[0]?.permissions.includes('hr.leave.create'), 'new employee login gets the baseline Employee role')
const myHr = await ok(call(empTok, 'GET', '/me/hr'), 'me/hr')
const al = myHr.balances.find(b => b.code === 'AL')
check(al?.entitled === 14 && !myHr.balances.some(b => b.code === 'PL'), 'balances: 14 annual leave, no paternity leave for a woman', myHr.balances.map(b => b.code))

const peek = await call(empTok, 'GET', `/hr/employees/${mgr.id}/salary`)
check(peek.status === 403, "employee cannot see the manager's salary")
const ownSalary = await ok(call(empTok, 'GET', `/hr/employees/${emp.id}/salary`), 'own salary')
check(ownSalary[0]?.gross === 150000, 'employee can see their own salary', ownSalary[0]?.gross)

// ---- multi-step leave: line manager → HR ----
const types = await ok(call(empTok, 'GET', '/hr/leave/types'), 'types')
const typeId = code => types.find(t => t.code === code).id
// Fri 6 Nov – Mon 9 Nov 2026 with Sunday off = 3 working days
const req = await ok(call(empTok, 'POST', '/hr/leave/requests', { leaveTypeId: typeId('AL'), fromDate: '2026-11-06', toDate: '2026-11-09', isHalfDay: false, reason: 'Family event' }), 'apply')
check(req.days === 3, 'leave days exclude the weekly off (Sunday)', req.days)
check(req.approvals.length === 2 && req.approvals[0].status === 'Pending' && req.approvals[1].status === 'Waiting', 'two-step chain created', req.approvals.map(a => a.status))

const hrEarly = await call(hrTok, 'POST', `/hr/leave/requests/${req.id}/decision`, { approve: true })
check(hrEarly.status === 403, 'HR cannot approve before the line manager')
const selfApprove = await call(empTok, 'POST', `/hr/leave/requests/${req.id}/decision`, { approve: true })
check(selfApprove.status === 403, 'employee cannot approve their own leave')

const mgrTok = await login(mail('mgr'))
const inbox = await ok(call(mgrTok, 'GET', '/hr/leave/inbox'), 'mgr inbox')
check(inbox.some(r => r.id === req.id), "request appears in the line manager's inbox")
const afterMgr = await ok(call(mgrTok, 'POST', `/hr/leave/requests/${req.id}/decision`, { approve: true, comment: 'OK' }), 'mgr approves')
check(afterMgr.status === 'Pending' && afterMgr.approvals[1].status === 'Pending', 'after manager approval, it moves to HR')
const twice = await call(mgrTok, 'POST', `/hr/leave/requests/${req.id}/decision`, { approve: true })
check(twice.status === 403, 'manager cannot also approve the HR step')
const done = await ok(call(hrTok, 'POST', `/hr/leave/requests/${req.id}/decision`, { approve: true }), 'hr approves')
check(done.status === 'Approved', 'HR approval completes the request')

const overlap = await call(empTok, 'POST', '/hr/leave/requests', { leaveTypeId: typeId('CL'), fromDate: '2026-11-09', toDate: '2026-11-09', isHalfDay: false })
check(overlap.status === 400, 'overlapping leave is rejected')
const tooMuch = await call(empTok, 'POST', '/hr/leave/requests', { leaveTypeId: typeId('SL'), fromDate: '2026-12-01', toDate: '2026-12-15', isHalfDay: false })
check(tooMuch.status === 400, 'leave beyond the balance is rejected (8 sick days)')

const rej = await ok(call(empTok, 'POST', '/hr/leave/requests', { leaveTypeId: typeId('CL'), fromDate: '2026-12-21', toDate: '2026-12-21', isHalfDay: true }), 'apply half')
const noReason = await call(mgrTok, 'POST', `/hr/leave/requests/${rej.id}/decision`, { approve: false })
check(noReason.status === 400, 'rejecting requires a reason')
const rejected = await ok(call(mgrTok, 'POST', `/hr/leave/requests/${rej.id}/decision`, { approve: false, comment: 'Year-end release' }), 'reject')
check(rejected.status === 'Rejected' && rejected.approvals[1].status === 'Skipped', 'rejection closes the chain')

// ---- September 2026: 2 days unpaid leave, 1 absent, 1 half day ----
const ul = await ok(call(empTok, 'POST', '/hr/leave/requests', { leaveTypeId: typeId('UL'), fromDate: '2026-09-07', toDate: '2026-09-08', isHalfDay: false }), 'unpaid leave')
await ok(call(mgrTok, 'POST', `/hr/leave/requests/${ul.id}/decision`, { approve: true }), 'ul mgr')
await ok(call(hrTok, 'POST', `/hr/leave/requests/${ul.id}/decision`, { approve: true }), 'ul hr')
await ok(call(hrTok, 'PUT', '/hr/attendance', { date: '2026-09-10', entries: [{ employeeId: emp.id, status: 'Absent' }] }), 'absent')
await ok(call(hrTok, 'PUT', '/hr/attendance', { date: '2026-09-11', entries: [{ employeeId: emp.id, status: 'HalfDay' }] }), 'half')
const sheet = await ok(call(hrTok, 'GET', `/hr/attendance?entityId=${root.id}&date=2026-09-07`), 'sheet')
check(sheet.find(r => r.employeeId === emp.id)?.dayType?.startsWith('On leave'), 'daily sheet shows approved leave')
const empAttend = await call(empTok, 'PUT', '/hr/attendance', { date: '2026-09-14', entries: [{ employeeId: emp.id, status: 'Present' }] })
check(empAttend.status === 403, 'employee cannot mark attendance')

// ---- payroll ----
const runDetail = await ok(call(hrTok, 'POST', '/payroll/runs', { entityId: root.id, year: 2026, month: 9, includeSubEntities: true }), 'create run')
const slip = runDetail.payslips.find(p => p.employeeCode === 'EMP-002')
const mslip = runDetail.payslips.find(p => p.employeeCode === 'EMP-001')
const line = (s, code) => s.lines.find(l => l.code === code)?.amount
// Hand-computed: 150,000 gross split → Basic 100,000 / HRA 35,005 / Medical 10,000 / Utilities 4,995.
// Unpaid 3.5 of 30 days → earnings × 26.5/30 = 88,333 + 30,921 + 8,833 + 4,412 = 132,499.
// Taxable = 132,499 − medical exempt 8,833 = 123,666. Projected ×10 months = 1,236,660
//   → TY2027: 6,000 + 11% × 36,660 = 10,033 → 1,003/month.
// EOBI 1% of Rs 40,700 = 407. Net = 132,499 − 1,003 − 407 = 131,089.
check(slip?.unpaidDays === 3.5 && slip?.payableDays === 26.5, 'unpaid days = 2 unpaid leave + 1 absent + ½ half day', [slip?.unpaidDays, slip?.payableDays])
check(line(slip, 'BASIC') === 88333 && slip?.grossEarnings === 132499, 'earnings prorated by payable days', [line(slip, 'BASIC'), slip?.grossEarnings])
check(slip?.taxableIncome === 123666, 'medical allowance exempt up to 10% of basic', slip?.taxableIncome)
check(slip?.incomeTax === 1003, 'income tax per FBR TY2027 slabs, spread over remaining months', slip?.incomeTax)
check(line(slip, 'EOBI') === 407 && line(slip, 'EOBI_ER') === 2035, 'EOBI 1% / 5% of minimum wage', [line(slip, 'EOBI'), line(slip, 'EOBI_ER')])
check(slip?.netPay === 131089, 'net pay', slip?.netPay)
// Manager 300,000: taxable 280,000 × 10 = 2,800,000 → 116,000 + 20% × 600,000 = 236,000 → 23,600/month.
check(mslip?.incomeTax === 23600, 'manager tax 23,600 (20% slab)', mslip?.incomeTax)

await ok(call(hrTok, 'POST', `/payroll/runs/${runDetail.run.id}/adjustments`, { employeeId: emp.id, name: 'Performance bonus', kind: 'Earning', amount: 10000, isTaxable: true }), 'bonus')
const withBonus = (await ok(call(hrTok, 'GET', `/payroll/runs/${runDetail.run.id}`), 'run')).payslips.find(p => p.employeeCode === 'EMP-002')
check(withBonus.grossEarnings === 142499 && withBonus.incomeTax > 1003, 'taxable bonus raises gross and tax', [withBonus.grossEarnings, withBonus.incomeTax])

const selfApproveRun = await call(hrTok, 'POST', `/payroll/runs/${runDetail.run.id}/approve`)
check(selfApproveRun.status === 403, 'the preparer cannot approve their own payroll run')
const empPayslipsBefore = await ok(call(empTok, 'GET', '/me/payslips'), 'my payslips')
check(empPayslipsBefore.length === 0, 'draft payslips are not visible to the employee')
await ok(call(owner, 'POST', `/payroll/runs/${runDetail.run.id}/approve`), 'approve')
const locked = await call(hrTok, 'PUT', '/hr/attendance', { date: '2026-09-14', entries: [{ employeeId: emp.id, status: 'Absent' }] })
check(locked.status === 400, 'attendance is locked once payroll is approved')
await ok(call(hrTok, 'POST', `/payroll/runs/${runDetail.run.id}/post`), 'post')
const mine = await ok(call(empTok, 'GET', '/me/payslips'), 'my payslips')
check(mine.length === 1 && mine[0].netPay === withBonus.netPay, 'employee sees their posted payslip')

// October: tax uses September's actual figures (year-to-date method)
const nov = await ok(call(hrTok, 'POST', '/payroll/runs', { entityId: root.id, year: 2026, month: 10, includeSubEntities: true }), 'oct run')
const mnov = nov.payslips.find(p => p.employeeCode === 'EMP-001')
// Prior 280,000 taxable / 23,600 tax; projected 280,000 + 280,000 × 9 = 2,800,000 → 236,000; (236,000 − 23,600) / 9 = 23,600
check(mnov.incomeTax === 23600, 'year-to-date withholding stays consistent month to month', mnov.incomeTax)
const dup = await call(hrTok, 'POST', '/payroll/runs', { entityId: root.id, year: 2026, month: 10, includeSubEntities: true })
check(dup.status === 400, 'duplicate run for the same month is rejected')

console.log(failures === 0 ? '\nALL HR & PAYROLL CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
