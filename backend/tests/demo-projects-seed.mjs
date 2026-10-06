// Adds Projects demo data to "Techive Software" (run the other demo seeds first).
// pm@demo.erpos.local (Project Manager), usman@, fahad@ and ayesha@demo.erpos.local (Employees) use the demo password.
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken
const iso = d => d.toISOString().slice(0, 10)
const now = new Date(Date.now() + 5 * 3600_000)
const day = n => iso(new Date(now.getTime() + n * 86400_000))
const monday = new Date(now.getTime() - ((now.getUTCDay() + 6) % 7) * 86400_000)
const wk = (weeksAgo, i) => iso(new Date(monday.getTime() + (i - 7 * weeksAgo) * 86400_000))
const owner = await login('owner@demo.erpos.local')
if ((await call(owner, 'GET', '/projects')).length > 0) { console.log('Projects demo data already present.'); process.exit(0) }

const SOFT = (await call(owner, 'GET', '/entities')).find(e => e.code === 'SOFT').id
const roles = await call(owner, 'GET', '/roles')
const role = n => roles.find(r => r.name === n).id
const emp = (code, name, email, roleName, gross) => call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: SOFT, employmentType: 'Permanent',
  status: 'Active', joinDate: '2025-03-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email, password: DEMO_PASSWORD, userType: 'Employee',
  roleId: role(roleName), monthlyGross: gross })
await emp('GB-210', 'Mahnoor Iqbal', 'pm@demo.erpos.local', 'Project Manager', 420000)
await emp('GB-211', 'Fahad Siddiqui', 'fahad@demo.erpos.local', 'Employee', 280000)
await emp('GB-212', 'Ayesha Noor', 'ayesha@demo.erpos.local', 'Employee', 160000)
const pm = await login('pm@demo.erpos.local')
const tok = { mahnoor: pm, usman: await login('usman@demo.erpos.local'), fahad: await login('fahad@demo.erpos.local'), ayesha: await login('ayesha@demo.erpos.local') }
const people = await call(pm, 'GET', '/projects/people')
const P = n => people.find(p => p.name === n).employeeId
const [mahnoor, usman, fahad, ayesha] = ['Mahnoor Iqbal', 'Usman Tariq', 'Fahad Siddiqui', 'Ayesha Noor'].map(P)

const client = b => call(pm, 'POST', '/projects/clients', { paymentTermsDays: 30, ...b })
const mart = await client({ name: 'Bahria Mart (Pvt) Ltd', email: 'accounts@bahriamart.test', city: 'Lahore', ntn: '4455667-1' })
const sky = await client({ name: 'SkyCare Clinics', email: 'it@skycare.test', city: 'Islamabad', ntn: '7788990-2' })
const nordic = await client({ name: 'Nordic Apps AB', email: 'finance@nordicapps.test', city: 'Stockholm', country: 'Sweden', paymentTermsDays: 15 })
const tax = await call(owner, 'GET', '/finance/tax-rates')
const pst = tax.find(t => t.code === 'PST-PB').id, exempt = tax.find(t => t.code === 'EXEMPT').id

const project = async body => {
  const p = await call(pm, 'POST', '/projects', { entityId: SOFT, budgetHours: 0, contractAmount: 0, defaultBillRate: 0, milestones: [], ...body })
  return body.start === false ? p : call(pm, 'POST', `/projects/${p.id}/status/Active`)
}
const web = await project({ code: 'MART', name: 'Bahria Mart online store', clientId: mart.id, billingType: 'TimeAndMaterials', startDate: day(-45), endDate: day(75),
  budgetHours: 420, defaultBillRate: 6000, taxRateId: pst, managerEmployeeId: mahnoor, description: 'Next.js storefront, ERP stock sync, JazzCash/Easypaisa checkout',
  members: [{ employeeId: mahnoor, role: 'Project manager', billRate: 9000 }, { employeeId: usman, role: 'Lead developer' }, { employeeId: fahad, role: 'Developer' },
    { employeeId: ayesha, role: 'QA engineer', billRate: 3500 }] })
let app = await project({ code: 'SKY', name: 'SkyCare patient app', clientId: sky.id, billingType: 'FixedPrice', startDate: day(-40), endDate: day(70), budgetHours: 600,
  contractAmount: 1800000, taxRateId: pst, managerEmployeeId: mahnoor, description: 'Flutter app: appointments, e-prescriptions, lab reports',
  members: [{ employeeId: mahnoor, role: 'Project manager' }, { employeeId: usman, role: 'Backend' }, { employeeId: fahad, role: 'Mobile' }],
  milestones: [{ name: 'Discovery & UX', dueDate: day(-20), amount: 360000 }, { name: 'MVP release', dueDate: day(20), amount: 900000 }, { name: 'Store launch & handover', dueDate: day(60), amount: 540000 }] })
const nrd = await project({ code: 'NORD', name: 'Nordic Apps — staff augmentation', clientId: nordic.id, billingType: 'TimeAndMaterials', startDate: day(-30), budgetHours: 320,
  defaultBillRate: 9000, taxRateId: exempt, managerEmployeeId: mahnoor, description: 'Exported IT services (zero-rated)',
  members: [{ employeeId: mahnoor, role: 'Account manager' }, { employeeId: fahad, role: 'React Native developer', billRate: 9500 }] })
await project({ code: 'HRP', name: 'Internal HR portal', billingType: 'NonBillable', startDate: day(14), budgetHours: 120, managerEmployeeId: mahnoor, start: false,
  members: [{ employeeId: mahnoor }, { employeeId: ayesha }] })

// Boards.
const task = (p, title, status, priority, who, est, due) => call(pm, 'POST', '/projects/tasks', { projectId: p.id, title, status, priority, assigneeEmployeeId: who, estimateHours: est, dueDate: due })
const t = {}
t.catalog = await task(web, 'Product catalogue & search', 'Done', 'High', usman, 40, day(-20))
t.cart = await task(web, 'Cart and checkout', 'InProgress', 'Urgent', usman, 48, day(5))
t.jazz = await task(web, 'JazzCash / Easypaisa integration', 'InProgress', 'High', fahad, 32, day(-2))
t.sync = await task(web, 'ERP stock sync job', 'Todo', 'Medium', fahad, 24, day(20))
t.qa = await task(web, 'Regression test plan', 'Review', 'Medium', ayesha, 16, day(3))
await task(web, 'Urdu localisation', 'Todo', 'Low', null, 12, day(40))
await task(web, 'Order tracking emails', 'Todo', 'Medium', ayesha, 8, day(25))
t.ux = await task(app, 'Wireframes & design system', 'Done', 'High', fahad, 60, day(-22))
t.api = await task(app, 'Appointments API', 'InProgress', 'High', usman, 50, day(10))
t.rx = await task(app, 'E-prescription screens', 'InProgress', 'Medium', fahad, 40, day(15))
await task(app, 'Lab report PDF viewer', 'Todo', 'Medium', fahad, 24, day(30))
t.nordic = await task(nrd, 'Sprint 7 tickets', 'InProgress', 'Medium', fahad, 80, day(10))

// Timesheets: two weeks ago (approved, then invoiced), last week (approved, not yet invoiced), this week (some drafts, one submission pending).
const log = (who, p, date, hours, extra = {}) => call(tok[who], 'POST', '/timesheets/entries', { projectId: p.id, date, hours, billable: true, ...extra })
const ids = []
for (const w of [2, 1]) {
  for (let i = 0; i < 5; i++) {
    ids.push((await log('usman', i < 3 ? web : app, wk(w, i), 7, { taskId: i < 3 ? (w === 2 ? t.catalog.id : t.cart.id) : t.api.id, description: i < 3 ? 'Storefront development' : 'Appointments API' })).id)
    ids.push((await log('fahad', i % 2 ? nrd : (w === 2 ? app : web), wk(w, i), 8, { taskId: i % 2 ? t.nordic.id : (w === 2 ? t.ux.id : t.jazz.id) })).id)
    if (i < 4) ids.push((await log('ayesha', web, wk(w, i), 6, { taskId: t.qa.id, description: 'Test cases and regression runs' })).id)
    if (i === 0) ids.push((await log('mahnoor', web, wk(w, i), 2, { description: 'Sprint review with client' })).id)
    if (i === 2) ids.push((await log('mahnoor', web, wk(w, i), 1.5, { billable: false, description: 'Internal stand-ups' })).id)
  }
  for (const who of ['usman', 'fahad', 'ayesha', 'mahnoor']) await call(tok[who], 'POST', `/timesheets/me/submit?date=${wk(w, 0)}`)
}
const pending = await call(pm, 'GET', '/timesheets/approvals')
await call(pm, 'POST', '/timesheets/approve', { entryIds: pending.map(e => e.id) })
const mine = (await call(owner, 'GET', '/timesheets?status=Submitted')).map(e => e.id)
if (mine.length) await call(owner, 'POST', '/timesheets/approve', { entryIds: mine })
await call(owner, 'POST', `/projects/${web.id}/invoice`, { upTo: wk(2, 6), invoiceDate: wk(1, 0) })
await call(owner, 'POST', `/projects/${nrd.id}/invoice`, { upTo: wk(2, 6), invoiceDate: wk(1, 0) })
app = await call(pm, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/complete`)
await call(owner, 'POST', `/projects/${app.id}/milestones/${app.milestones[0].id}/invoice`, {})
await log('usman', web, day(0), 4, { taskId: t.cart.id, description: 'Checkout validation' })
await log('ayesha', web, day(0), 5, { taskId: t.qa.id, description: 'Payment edge cases' })
await call(tok.ayesha, 'POST', `/timesheets/me/submit?date=${day(0)}`)
console.log('Projects demo data added: 3 clients, 4 projects, 12 tasks, three weeks of timesheets, 3 invoices.')
