// Adds HR & payroll demo data to the "GB Techive Group" demo organization (run demo-seed.mjs first).
// Usage: node tests/demo-hr-seed.mjs        Logins use DEMO_PASSWORD from demo-seed.mjs:
//   hr@demo.erpos.local        HR Officer for the whole group
//   ali@demo.erpos.local       Front office manager at Skardu Resort (line manager)
//   sana@demo.erpos.local      Receptionist reporting to Ali (has a pending leave request)
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'

async function call(token, method, path, body) {
  const res = await fetch(base + path, {
    method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken

const owner = await login('owner@demo.erpos.local')
const existing = (await call(owner, 'GET', '/hr/employees?pageSize=500')).items
if ((await call(owner, 'GET', '/payroll/runs')).length > 0) { console.log('HR demo data already present.'); process.exit(0) }

const entities = (await call(owner, 'GET', '/entities'))
const byCode = c => entities.find(e => e.code === c).id
const roles = await call(owner, 'GET', '/roles')
if (!roles.some(r => r.name === 'HR Officer')) {
  // Organizations created before Phase 2 don't have the default HR Officer role yet.
  const catalog = await call(owner, 'GET', '/catalog/permissions')
  const perms = catalog.filter(g => g.module === 'hr' || g.module === 'payroll').flatMap(g => g.permissions.map(p => p.code))
  roles.push(await call(owner, 'POST', '/roles', { name: 'HR Officer', description: 'Employees, attendance, leave and payroll', permissions: [...perms, 'core.entities.view', 'core.users.view'] }))
}
const role = n => roles.find(r => r.name === n).id

// Get-or-create so a partly failed run can be repeated.
const depts = await call(owner, 'GET', '/hr/departments')
const dept = async (entity, name, code) => depts.find(d => d.code === code && d.entityId === byCode(entity))
  ?? await call(owner, 'POST', '/hr/departments', { entityId: byCode(entity), name, code })
const front = await dept('HTL-SKD', 'Front Office', 'FO')
const eng = await dept('SOFT', 'Engineering', 'ENG')
const existingDes = await call(owner, 'GET', '/hr/designations')
const des = {}
for (const t of ['HR Officer', 'Front Office Manager', 'Receptionist', 'Software Engineer', 'Driver'])
  des[t] = (existingDes.find(d => d.title === t) ?? await call(owner, 'POST', '/hr/designations', { title: t })).id

const emp = (code, name, entity, extra) => ({
  employeeCode: code, fullName: name, entityId: byCode(entity), employmentType: 'Permanent', status: 'Active',
  joinDate: '2025-03-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false, ...extra,
})
const create = async (data, email, gross, userType = 'Employee', roleId) =>
  existing.find(e => e.employeeCode === data.employeeCode)
  ?? await call(owner, 'POST', '/hr/employees', { data, email, password: DEMO_PASSWORD, userType, roleId, monthlyGross: gross })

// Payroll wasn't switched on for the logistics company in the Phase 1 demo.
await call(owner, 'PUT', `/entities/${byCode('LOGI')}/modules`, { modules: ['hr', 'payroll', 'finance', 'inventory', 'logistics'] })

await create(emp('GB-001', 'Hira Khan', 'GBDEMO', { designationId: des['HR Officer'], gender: 'Female', iban: 'PK36SCBL0000001123456701', bankName: 'Standard Chartered' }),
  'hr@demo.erpos.local', 180000, 'Admin', role('HR Officer'))
const ali = await create(emp('GB-101', 'Ali Raza', 'HTL-SKD', { departmentId: front.id, designationId: des['Front Office Manager'], gender: 'Male', cnic: '71501-1234567-1', iban: 'PK36MEZN0000001123456702', bankName: 'Meezan Bank' }),
  'ali@demo.erpos.local', 220000, 'Manager', role('Manager'))
const sana = await create(emp('GB-102', 'Sana Baig', 'HTL-SKD', { departmentId: front.id, designationId: des['Receptionist'], managerId: ali.id, gender: 'Female', iban: 'PK36HABB0000001123456703', bankName: 'HBL' }),
  'sana@demo.erpos.local', 85000)
await create(emp('GB-103', 'Karim Shah', 'HTL-HNZ', { designationId: des['Receptionist'], managerId: ali.id, gender: 'Male', joinDate: '2026-09-15' }),
  'karim@demo.erpos.local', 70000)
await create(emp('GB-201', 'Usman Tariq', 'SOFT', { departmentId: eng.id, designationId: des['Software Engineer'], gender: 'Male', iban: 'PK36UNIL0000001123456704', bankName: 'UBL' }),
  'usman@demo.erpos.local', 350000)
await create(emp('GB-301', 'Naveed Ahmed', 'LOGI', { designationId: des['Driver'], gender: 'Male', employmentType: 'Contract', iban: 'PK36ALFH0000001123456705', bankName: 'Bank Alfalah' }),
  'naveed@demo.erpos.local', 45000)

await call(owner, 'POST', '/hr/attendance/holidays', { date: '2026-12-25', name: 'Quaid-e-Azam Day' })
await call(owner, 'POST', '/hr/attendance/holidays', { date: '2026-11-09', name: 'Iqbal Day' })
await call(owner, 'PUT', '/hr/attendance', { date: '2026-09-17', entries: [{ employeeId: sana.id, status: 'Absent', remarks: 'No call' }] })

const sanaTok = await login('sana@demo.erpos.local')
const types = await call(sanaTok, 'GET', '/hr/leave/types')
await call(sanaTok, 'POST', '/hr/leave/requests', { leaveTypeId: types.find(t => t.code === 'AL').id, fromDate: '2026-10-19', toDate: '2026-10-22', isHalfDay: false, reason: 'Sister\'s wedding in Gilgit' })

const hrTok = await login('hr@demo.erpos.local')
await call(hrTok, 'POST', '/payroll/runs', { entityId: byCode('GBDEMO'), year: 2026, month: 9, includeSubEntities: true, notes: 'September salaries' })
console.log('HR demo data added. Logins are listed at the top of tests/demo-hr-seed.mjs.')
