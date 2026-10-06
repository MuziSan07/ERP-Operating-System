// Adds NGO demo data to "Mountain Care Foundation" (run the other demo seeds first).
// grants@demo.erpos.local (Grants Manager), programs@demo.erpos.local (Program Officer) and fundraising@demo.erpos.local
// (Fundraising Officer) use the demo password.
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken
const day = n => new Date(Date.now() + 5 * 3600_000 + n * 86400_000).toISOString().slice(0, 10)
const owner = await login('owner@demo.erpos.local')
if ((await call(owner, 'GET', '/ngo/funds')).length > 0) { console.log('NGO demo data already present.'); process.exit(0) }

const entities = await call(owner, 'GET', '/entities')
const NGO = entities.find(e => e.code === 'NGO').id
await call(owner, 'PUT', `/entities/${NGO}/modules`, { modules: ['hr', 'payroll', 'finance', 'ngo', 'projects'] })
const roles = await call(owner, 'GET', '/roles')
const emp = (code, name, email, role) => call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: NGO, employmentType: 'Permanent',
  status: 'Active', joinDate: '2025-09-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email, password: DEMO_PASSWORD,
  userType: 'Employee', roleId: roles.find(r => r.name === role).id, monthlyGross: 110000 })
await emp('GB-401', 'Sadia Grants', 'grants@demo.erpos.local', 'Grants Manager')
await emp('GB-402', 'Karim Programs', 'programs@demo.erpos.local', 'Program Officer')
await emp('GB-403', 'Nida Fundraising', 'fundraising@demo.erpos.local', 'Fundraising Officer')
const gm = await login('grants@demo.erpos.local'), po = await login('programs@demo.erpos.local'), fr = await login('fundraising@demo.erpos.local')

const accounts = await call(owner, 'GET', '/finance/accounts')
const acc = c => accounts.find(a => a.code === c).id
const fund = (code, name, kind, purpose) => call(gm, 'POST', '/ngo/funds', { code, name, kind, purpose, isActive: true })
const gen = await fund('GEN', 'General fund', 'Unrestricted')
const zkt = await fund('ZAKAT', 'Zakat fund', 'Zakat', 'Zakat for verified deserving families')
const winter = await fund('WINTER', 'Winter relief appeal 2026', 'Restricted', 'Quilts, stoves and fuel for families above 2,500 m')
await fund('ENDOW', 'Scholarship endowment', 'Endowment', 'Capital kept intact; returns fund scholarships')
const program = (code, name, sector, target) => call(gm, 'POST', '/ngo/programs', { entityId: NGO, code, name, sector, targetBeneficiaries: target, isActive: true })
const edu = await program('EDU', "Girls' education", 'Education', 300)
const drr = await program('DRR', 'Glacier flood preparedness', 'Emergency relief', 1200)
const liv = await program('LIV', "Women's livelihoods", 'Livelihoods', 150)

const donor = b => call(fr, 'POST', '/ngo/donors', b)
const kdt = await donor({ name: 'Karakoram Development Trust', type: 'Foundation', ntn: '7712345-1', city: 'Islamabad' })
const nlf = await donor({ name: 'Northern Light Foundation', type: 'Institutional', country: 'United Kingdom' })
const hamid = await donor({ name: 'Hamid Ali Khan', type: 'Individual', cnic: '71101-5556667-1', phone: '0355-1112233', city: 'Gilgit' })
const sana = await donor({ name: 'Sana Mirza', type: 'Individual', cnic: '35202-4445556-2', city: 'Lahore' })
const telco = await donor({ name: 'Peak Telecom (CSR)', type: 'Corporate', ntn: '3345678-9' })
const give = (d, f, amount, daysAgo, extra = {}) => call(fr, 'POST', '/ngo/donations', { entityId: NGO, date: day(-daysAgo), donorId: d.id, fundId: f.id, amount,
  method: 'BankTransfer', bankAccountId: acc('1120'), ...extra })
await give(hamid, zkt, 150000, 40, { reference: 'Zakat 1447' })
await give(sana, zkt, 85000, 3, { method: 'Online', reference: 'JazzCash 88231' })
await give(telco, winter, 500000, 20, { reference: 'CSR/2026/77' })
await give(sana, winter, 25000, 2, { method: 'Cash', bankAccountId: acc('1110') })
await give(telco, gen, 300000, 60)
await give(hamid, gen, 20000, 1, { method: 'Cheque', reference: 'HBL 004512' })

// Grant 1: PKR, a third of the way through.
let g1 = await call(gm, 'POST', '/ngo/grants', { entityId: NGO, title: "Girls' schools in Ghizer", donorId: kdt.id, agreementRef: 'KDT/ED/26-03', programId: edu.id,
  startDate: day(-120), endDate: day(245), reportingFrequency: 'Quarterly', flexibilityPercent: 10,
  budgetLines: [{ code: 'PER', description: 'Teachers (12) and field coordinator', category: 'Personnel', amount: 1800000, expenseAccountId: acc('6100') },
    { code: 'STP', description: 'Attendance stipends for families', category: 'Assistance', amount: 900000, expenseAccountId: acc('6950') },
    { code: 'MAT', description: 'Books, uniforms and stationery', category: 'Activities', amount: 300000, expenseAccountId: acc('6960') }],
  tranches: [{ dueDate: day(-110), amount: 1500000 }, { dueDate: day(60), amount: 1500000, condition: 'On approval of the 6-month progress report' }] })
g1 = await call(gm, 'POST', `/ngo/grants/${g1.id}/activate`)
g1 = await call(gm, 'POST', `/ngo/grants/${g1.id}/tranches`, { trancheId: g1.tranches[0].id, date: day(-105), amount: 1500000, bankAccountId: acc('1120') })
const vendor = await call(owner, 'POST', '/finance/contacts', { code: 'V-GILBOOK', name: 'Gilgit Book Depot', isVendor: true, isCustomer: false, isActive: true, paymentTermsDays: 15 })
const charge = body => call(gm, 'POST', '/ngo/expenses', { function: 'Program', ...body })
await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[0].id, date: day(-30), description: 'Teacher salaries, 3 months (payroll)', amount: 450000, allocationOnly: true })
await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[0].id, date: day(-1), description: 'Teacher salaries, last month (payroll)', amount: 150000, allocationOnly: true })
await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[2].id, date: day(-90), description: 'Textbooks and uniforms for 280 girls', amount: 186000, vendorId: vendor.id })
await call(gm, 'POST', `/ngo/grants/${g1.id}/reports/${g1.reports[0].id}/submit`, { submittedOn: day(-2), notes: 'Sent by email' })

// Grant 2: USD.
let g2 = await call(gm, 'POST', '/ngo/grants', { entityId: NGO, title: 'Community flood early warning — Hunza & Nagar', donorId: nlf.id, agreementRef: 'NLF-PK-0193', programId: drr.id,
  currency: 'USD', agreementRate: 280, startDate: day(-45), endDate: day(320), reportingFrequency: 'SemiAnnual', flexibilityPercent: 15,
  budgetLines: [{ code: 'TRN', description: 'Village response team training', category: 'Activities', amount: 10000, expenseAccountId: acc('6960') },
    { code: 'KIT', description: 'Sirens, radios and first-aid kits', category: 'Equipment', amount: 15000, expenseAccountId: acc('6960') }],
  tranches: [{ dueDate: day(-40), amount: 15000 }, { dueDate: day(150), amount: 10000 }] })
g2 = await call(gm, 'POST', `/ngo/grants/${g2.id}/activate`)
g2 = await call(gm, 'POST', `/ngo/grants/${g2.id}/tranches`, { trancheId: g2.tranches[0].id, date: day(-38), amount: 15000, rate: 282.5, bankAccountId: acc('1120') })
await charge({ grantId: g2.id, budgetLineId: g2.budgetLines[0].id, date: day(-20), description: 'Training of 14 village teams', amount: 395500, paidFromAccountId: acc('1120') })
await charge({ grantId: g2.id, budgetLineId: g2.budgetLines[1].id, date: day(-10), description: '40 handheld radios', amount: 1130000, paidFromAccountId: acc('1120') })

// Grant 3: proposal.
await call(gm, 'POST', '/ngo/grants', { entityId: NGO, title: "Women's apricot processing enterprise", donorId: kdt.id, programId: liv.id, startDate: day(30), endDate: day(395),
  reportingFrequency: 'Quarterly', flexibilityPercent: 10,
  budgetLines: [{ code: 'EQP', description: 'Solar dryers', category: 'Equipment', amount: 1200000, expenseAccountId: acc('6960') },
    { code: 'TRG', description: 'Business skills training', category: 'Activities', amount: 400000, expenseAccountId: acc('6960') }],
  tranches: [{ dueDate: day(35), amount: 1000000 }, { dueDate: day(200), amount: 600000 }] })

// Beneficiaries.
const people = [
  ['Gul Bano', '71101-1000001-2', 'Female', 'Ghizer', 6, true, edu, 'Widow, three daughters in school'],
  ['Shamim Akhtar', '71101-1000002-4', 'Female', 'Ghizer', 8, false, edu, null],
  ['Zainab Bibi', '71101-1000003-6', 'Female', 'Ghizer', 5, true, edu, 'Disabled husband'],
  ['Parveen Shah', '71101-1000004-8', 'Female', 'Ghizer', 7, false, edu, null],
  ['Ali Madad', '71201-2000001-1', 'Male', 'Hunza', 9, false, drr, 'House in flood path'],
  ['Sher Baz', '71201-2000002-3', 'Male', 'Nagar', 6, true, drr, 'Lost livestock in 2025 flood'],
  ['Rehana Begum', '71201-2000003-5', 'Female', 'Hunza', 4, true, liv, 'Female-headed household'],
  ['Noor Jahan', '71201-2000004-7', 'Female', 'Hunza', 5, false, liv, null],
]
const bens = []
for (const [name, cnic, gender, district, hh, zakat, prog, vul] of people)
  bens.push(await call(po, 'POST', '/ngo/beneficiaries', { entityId: NGO, fullName: name, cnic, gender, district, householdSize: hh, zakatEligible: zakat,
    programId: prog.id, vulnerabilities: vul, isActive: true, enrolledOn: day(-100) }))
const help = (token, b, body) => call(token, 'POST', `/ngo/beneficiaries/${b.id}/assistance`, { date: day(-5), allowRepeat: false, ...body })
for (const b of bens.slice(0, 4))
  await help(owner, b, { type: 'Cash', description: 'Attendance stipend — term 1', value: 6000, grantId: g1.id, budgetLineId: g1.budgetLines[1].id, paidFromAccountId: acc('1120') })
await help(owner, bens[0], { type: 'Cash', description: 'Zakat — winter fuel', value: 15000, fundId: zkt.id, paidFromAccountId: acc('1110'), date: day(-2), allowRepeat: true })
await help(owner, bens[5], { type: 'Cash', description: 'Zakat — livestock replacement', value: 40000, fundId: zkt.id, paidFromAccountId: acc('1120'), date: day(-3) })
for (const b of [bens[4], bens[5], bens[6], bens[7]])
  await help(po, b, { type: 'InKind', description: 'Winter kit (quilt, stove)', quantity: 1, value: 9500, fundId: winter.id, date: day(-4) })
await charge({ fundId: winter.id, entityId: NGO, programId: drr.id, accountId: acc('6950'), date: day(-6), description: '120 winter kits (quilts, gas stoves)', amount: 380000, paidFromAccountId: acc('1120') })
await charge({ fundId: gen.id, entityId: NGO, accountId: acc('6500'), function: 'ManagementGeneral', date: day(-15), description: 'Gilgit office rent and utilities', amount: 65000, paidFromAccountId: acc('1120') })
await charge({ fundId: gen.id, entityId: NGO, accountId: acc('6500'), function: 'Fundraising', date: day(-8), description: 'Winter appeal SMS campaign', amount: 18000, paidFromAccountId: acc('1120') })
console.log('NGO demo data added: 4 funds, 3 programs, 5 donors, 6 donations, 3 grants (one in USD), 8 beneficiaries.')
