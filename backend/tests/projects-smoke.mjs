// End-to-end test of Projects & services: clients, time-and-materials and fixed-price projects, cost rates from salaries,
// task board, weekly timesheets with approval (four-eyes), hour billing, milestone billing, utilization and the ledger.
// Usage: node tests/projects-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@prj.test`.toLowerCase()
const iso = d => d.toISOString().slice(0, 10)
const today = new Date(Date.now() + 5 * 3600_000)
const day = n => iso(new Date(today.getTime() + n * 86400_000))
// Last week's Monday … Friday (always in the past, always one timesheet week).
const lastMonday = new Date(today.getTime() - (((today.getUTCDay() + 6) % 7) + 7) * 86400_000)
const wd = i => iso(new Date(lastMonday.getTime() + i * 86400_000))
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

// ---- setup ----
const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', { name: `Software House ${run}`, code: `PRJ${run}`, industry: 'SoftwareServices', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['projects', 'finance', 'hr', 'payroll'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const role = n => roles.find(r => r.name === n)?.id
check(!!role('Project Manager'), 'Project Manager default role exists')
const emp = (code, name, n, roleName, gross) => ok(call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: E, employmentType: 'Permanent',
  status: 'Active', joinDate: '2025-01-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email: mail(n), password: pw, userType: 'Employee',
  roleId: role(roleName), monthlyGross: gross }), name)
const pmEmp = await emp('E-01', 'Asad Manager', 'pm', 'Project Manager', 264000)
const dev1Emp = await emp('E-02', 'Bilal Developer', 'dev1', 'Employee', 176000)
const dev2Emp = await emp('E-03', 'Hira Tester', 'dev2', 'Employee', 88000)
const dev3Emp = await emp('E-04', 'Usman Outsider', 'dev3', 'Employee', 120000)
await ok(call(owner, 'POST', '/users', { email: mail('acct'), fullName: 'Accountant', password: pw, userType: 'Employee', primaryEntityId: E, roleId: role('Accountant') }), 'acct')
const pm = await login(mail('pm')), dev1 = await login(mail('dev1')), dev2 = await login(mail('dev2')), dev3 = await login(mail('dev3')), acct = await login(mail('acct'))
const id = e => e.id ?? e.employee?.id
const people = await ok(call(pm, 'GET', '/projects/people'), 'people')
const rate = n => people.find(p => p.name === n)?.costRate
check(rate('Asad Manager') === 1500 && rate('Bilal Developer') === 1000 && rate('Hira Tester') === 500, 'hourly cost rates from salary ÷ 176 hours',
  people.map(p => [p.name, p.costRate]))

const client = await ok(call(pm, 'POST', '/projects/clients', { name: 'Acme Retail (Pvt) Ltd', email: 'ap@acme.test', city: 'Lahore', ntn: '1122334-5', paymentTermsDays: 30 }), 'client')
const pst = (await ok(call(owner, 'GET', '/finance/tax-rates'), 'tax')).find(t => t.code === 'PST-PB').id

// ---- time & materials project ----
const webBody = members => ({ entityId: E, code: 'WEB', name: 'Acme e-commerce site', clientId: client.id, billingType: 'TimeAndMaterials', startDate: day(-20),
  budgetHours: 40, contractAmount: 0, defaultBillRate: 5000, taxRateId: pst, managerEmployeeId: id(pmEmp), members, milestones: [] })
const team = [{ employeeId: id(pmEmp), role: 'Project manager', billRate: 8000 }, { employeeId: id(dev1Emp), role: 'Developer' }, { employeeId: id(dev2Emp), role: 'QA', billRate: 3000 }]
const devCreate = await call(dev1, 'POST', '/projects', webBody(team))
check(devCreate.status === 403, 'employees cannot create projects')
let web = await ok(call(pm, 'POST', '/projects', webBody(team)), 'web')
check(web.status === 'Planned' && web.members.find(m => m.name === 'Bilal Developer').effectiveBillRate === 5000 && web.members.find(m => m.name === 'Bilal Developer').costRate === 1000,
  'project created; members inherit the default bill rate and their salary cost rate')
const notActive = await call(dev1, 'POST', '/timesheets/entries', { projectId: web.id, date: wd(0), hours: 8, billable: true })
check(notActive.status === 400, "time can't be logged before the project is active")
web = await ok(call(pm, 'POST', `/projects/${web.id}/status/Active`), 'activate')

const task = await ok(call(pm, 'POST', '/projects/tasks', { projectId: web.id, title: 'Checkout page', status: 'Todo', priority: 'High', assigneeEmployeeId: id(dev1Emp), estimateHours: 16, dueDate: day(-1) }), 'task')
check(task.key === 'WEB-1' && task.overdue, 'task numbered WEB-1 and flagged overdue')
const outsider = await call(pm, 'POST', '/projects/tasks', { projectId: web.id, title: 'X', status: 'Todo', priority: 'Low', assigneeEmployeeId: id(dev3Emp), estimateHours: 1 })
check(outsider.status === 400, 'tasks can only be assigned to the project team')
const moved = await ok(call(dev1, 'POST', `/projects/tasks/${task.id}/move`, { status: 'InProgress', sortOrder: 0 }), 'move')
check(moved.status === 'InProgress', 'the assignee can move their own card')
const otherMove = await call(dev2, 'POST', `/projects/tasks/${task.id}/move`, { status: 'Done', sortOrder: 0 })
check(otherMove.status === 403, "others can't move someone else's card without task permission")

// ---- timesheets ----
const log = (token, body) => call(token, 'POST', '/timesheets/entries', { projectId: web.id, billable: true, ...body })
const d1 = []
for (const b of [{ date: wd(0), hours: 8, taskId: task.id, description: 'Checkout UI' }, { date: wd(1), hours: 8, taskId: task.id }, { date: wd(2), hours: 6 },
  { date: wd(2), hours: 2, billable: false, description: 'Internal training' }]) d1.push(await ok(log(dev1, b), 'dev1 time'))
const tooMuch = await log(dev1, { date: wd(0), hours: 17 })
check(tooMuch.status === 400 && /24/.test(tooMuch.data?.title), 'a day holds at most 24 hours', tooMuch.data?.title)
const future = await log(dev1, { date: day(1), hours: 1 })
check(future.status === 400, "future time can't be logged")
const odd = await log(dev1, { date: wd(3), hours: 1.1 })
check(odd.status === 400, 'hours go in quarter-hour steps')
const notMember = await log(dev3, { date: wd(0), hours: 4 })
check(notMember.status === 400 && /team/.test(notMember.data?.title), "people not on the team can't log time")
const d2 = [await ok(log(dev2, { date: wd(0), hours: 4 }), 'dev2 a'), await ok(log(dev2, { date: wd(1), hours: 4 }), 'dev2 b')]
const pmEntry = await ok(log(pm, { date: wd(0), hours: 2, description: 'Sprint planning' }), 'pm time')

let week = await ok(call(dev1, 'GET', `/timesheets/me?date=${wd(0)}`), 'week')
check(week.totalHours === 24 && week.billableHours === 22 && week.canSubmit, "dev1's week: 24 h logged, 22 billable")
week = await ok(call(dev1, 'POST', `/timesheets/me/submit?date=${wd(0)}`), 'submit1')
check(week.entries.every(e => e.status === 'Submitted') && !week.canSubmit, 'week submitted')
const lockedEdit = await call(dev1, 'PUT', `/timesheets/entries/${d1[0].id}`, { projectId: web.id, date: wd(0), hours: 9, billable: true })
check(lockedEdit.status === 400, "submitted time can't be edited")
await ok(call(dev2, 'POST', `/timesheets/me/submit?date=${wd(0)}`), 'submit2')
await ok(call(pm, 'POST', `/timesheets/me/submit?date=${wd(0)}`), 'submit3')

const pending = await ok(call(pm, 'GET', '/timesheets/approvals'), 'approvals')
check(pending.length === 6 && !pending.some(e => e.employeeName === 'Asad Manager'), "PM's approval queue: 6 entries, not their own")
const selfApprove = await call(pm, 'POST', '/timesheets/approve', { entryIds: [pmEntry.id] })
check(selfApprove.status === 400, "nobody approves their own time")
const devApprove = await call(dev1, 'POST', '/timesheets/approve', { entryIds: [d2[0].id] })
check(devApprove.status === 403, 'employees cannot approve time')
await ok(call(pm, 'POST', '/timesheets/approve', { entryIds: [...d1.map(e => e.id), d2[0].id] }), 'approve')
const noReason = await call(pm, 'POST', '/timesheets/reject', { entryIds: [d2[1].id] })
check(noReason.status === 400, 'rejecting needs a reason')
await ok(call(pm, 'POST', '/timesheets/reject', { entryIds: [d2[1].id], reason: 'Regression testing was only 3 hours per the sprint log' }), 'reject')
await ok(call(owner, 'POST', '/timesheets/approve', { entryIds: [pmEntry.id] }), 'owner approves pm')
let w2 = await ok(call(dev2, 'GET', `/timesheets/me?date=${wd(0)}`), 'w2')
const rejected = w2.entries.find(e => e.id === d2[1].id)
check(rejected.status === 'Rejected' && /3 hours/.test(rejected.rejectReason) && w2.totalHours === 4, 'the rejected entry comes back with the reason and stops counting')
await ok(call(dev2, 'PUT', `/timesheets/entries/${d2[1].id}`, { projectId: web.id, date: wd(1), hours: 3, billable: true, description: 'Regression testing' }), 'fix')
await ok(call(dev2, 'POST', `/timesheets/me/submit?date=${wd(0)}`), 'resubmit')
await ok(call(pm, 'POST', '/timesheets/approve', { entryIds: [d2[1].id] }), 'approve fix')

// ---- hour billing ----
const devInvoice = await call(dev1, 'POST', `/projects/${web.id}/invoice`, {})
check(devInvoice.status === 403, 'employees cannot invoice clients')
const inv = await ok(call(acct, 'POST', `/projects/${web.id}/invoice`, {}), 'invoice')
// Bilal 22 h × 5,000 = 110,000 · Hira 7 h × 3,000 = 21,000 · Asad 2 h × 8,000 = 16,000 → 147,000 + 16% PST = 170,520.
check(inv.hours === 31 && inv.entries === 6 && inv.total === 170520 && !!inv.invoiceNumber, 'invoice: 31 billable hours = 147,000 + 16% PST = 170,520', inv)
const again = await call(acct, 'POST', `/projects/${web.id}/invoice`, {})
check(again.status === 400, 'invoiced time is not billed twice')
web = await ok(call(pm, 'GET', `/projects/${web.id}`), 'web')
// Cost: 24 h × 1,000 + 7 h × 500 + 2 h × 1,500 = 30,500.
check(web.hoursLogged === 33 && web.hoursPercent === 82.5 && web.billed === 147000 && web.unbilled === 0 && web.cost === 30500 && web.margin === 116500,
  'project: 33 of 40 h (82.5%), billed 147,000, cost 30,500, margin 116,500', [web.hoursLogged, web.hoursPercent, web.billed, web.cost, web.margin])
const del = await call(pm, 'DELETE', `/projects/tasks/${task.id}`)
check(del.status === 400, "a task with logged time can't be deleted")

// ---- fixed-price project ----
const appBody = ms => ({ entityId: E, code: 'APP', name: 'Acme loyalty mobile app', clientId: client.id, billingType: 'FixedPrice', startDate: day(-20), budgetHours: 300,
  contractAmount: 600000, defaultBillRate: 0, taxRateId: pst, managerEmployeeId: id(pmEmp),
  members: [{ employeeId: id(pmEmp) }, { employeeId: id(dev1Emp) }], milestones: ms })
const badMs = await call(pm, 'POST', '/projects', appBody([{ name: 'Design', dueDate: day(10), amount: 150000 }, { name: 'Build', dueDate: day(40), amount: 300000 }]))
check(badMs.status === 400 && /600,000/.test(badMs.data?.title), 'milestones must add up to the fixed price')
let app = await ok(call(pm, 'POST', '/projects', appBody([{ name: 'UX design', dueDate: day(10), amount: 150000 }, { name: 'Build & test', dueDate: day(40), amount: 300000 },
  { name: 'Store launch', dueDate: day(70), amount: 150000 }])), 'app')
app = await ok(call(pm, 'POST', `/projects/${app.id}/status/Active`), 'activate app')
const fpTime = await ok(call(dev1, 'POST', '/timesheets/entries', { projectId: app.id, date: wd(3), hours: 10, billable: true, description: 'Wireframes' }), 'fp time')
check(fpTime.billable === false, 'time on a fixed-price project is never billable by the hour')
await ok(call(dev1, 'POST', `/timesheets/me/submit?date=${wd(0)}`), 'submit fp')
await ok(call(pm, 'POST', '/timesheets/approve', { entryIds: [fpTime.id] }), 'approve fp')
const early = await call(acct, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/invoice`, {})
check(early.status === 400, 'a milestone is invoiced only after it is complete')
app = await ok(call(pm, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/complete`), 'complete')
const msInv = await ok(call(acct, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/invoice`, {}), 'ms invoice')
check(msInv.total === 174000, 'milestone invoice: 150,000 + 16% = 174,000', msInv)
const msAgain = await call(acct, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/invoice`, {})
check(msAgain.status === 400, 'a milestone is invoiced once')
const fpHours = await call(acct, 'POST', `/projects/${app.id}/invoice`, {})
check(fpHours.status === 400, 'fixed-price projects are not billed by the hour')
app = await ok(call(pm, 'GET', `/projects/${app.id}`), 'app')
check(app.billed === 150000 && app.cost === 10000 && app.margin === 140000, 'fixed price: billed 150,000, cost 10 h × 1,000 = 10,000', [app.billed, app.cost, app.margin])

// ---- reports, close-out, ledger ----
const util = await ok(call(acct, 'GET', `/projects/reports/utilization?from=${wd(0)}&to=${wd(4)}`), 'utilization')
const bilal = util.rows.find(r => r.name === 'Bilal Developer'), hira = util.rows.find(r => r.name === 'Hira Tester')
check(bilal.capacity === 40 && bilal.hours === 34 && bilal.billableHours === 22 && bilal.utilization === 55 && hira.utilization === 17.5,
  'utilization last week: Bilal 22 billable of 40 h capacity = 55%, Hira 7/40 = 17.5%', [bilal, hira])
const dash = await ok(call(pm, 'GET', '/projects/dashboard'), 'dashboard')
check(dash.activeProjects === 2 && dash.pendingApprovals === 0 && dash.overdueTasks.some(t => t.key === 'WEB-1'), 'dashboard: 2 active projects, approvals cleared, overdue task listed')
await ok(call(pm, 'POST', `/projects/${web.id}/status/Completed`), 'complete web')
const afterClose = await log(dev1, { date: wd(4), hours: 1 })
check(afterClose.status === 400, 'a completed project takes no more time')

const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb')
const row = c => tb.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }
check(row('1200').debit === 344520 && row('4100').credit === 297000 && row('2190').credit === 47520 && tb.totalDebit === tb.totalCredit,
  'ledger: receivables 344,520 · service revenue 297,000 · PST 47,520 · books balance', ['1200', '4100', '2190'].map(c => [c, row(c).debit, row(c).credit]))

console.log(failures === 0 ? '\nALL PROJECTS CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
