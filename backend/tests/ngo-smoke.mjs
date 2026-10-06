// End-to-end test of the NGO module: funds (unrestricted, restricted, Zakat, endowment), donations and receipts, grants
// (budget, tranches, flexibility, reporting, multi-currency, closing), beneficiaries (dedupe, Zakat eligibility, repeat
// assistance), functional expenses and the ledger.
// Usage: node tests/ngo-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@ngo.test`.toLowerCase()
const day = n => new Date(Date.now() + 5 * 3600_000 + n * 86400_000).toISOString().slice(0, 10)
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
await ok(call(platform, 'POST', '/platform/tenants', { name: `NGO Test ${run}`, code: `NGO${run}`, industry: 'Ngo', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['ngo', 'finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
check(['Grants Manager', 'Program Officer', 'Fundraising Officer'].every(n => roles.some(r => r.name === n)), 'NGO default roles exist')
const user = (n, role) => ok(call(owner, 'POST', '/users', { email: mail(n), fullName: n, password: pw, userType: 'Employee', primaryEntityId: E, roleId: roles.find(r => r.name === role).id }), n)
await user('gm', 'Grants Manager'); await user('po', 'Program Officer'); await user('fr', 'Fundraising Officer')
const gm = await login(mail('gm')), po = await login(mail('po')), fr = await login(mail('fr'))
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c)?.id
check(['2220', '2230', '4310', '6950', '6960'].every(acc), 'deferred restricted income, Zakat, released income and assistance accounts seeded')

const fund = (code, name, kind) => ok(call(gm, 'POST', '/ngo/funds', { code, name, kind, isActive: true }), code)
const gen = await fund('GEN', 'General fund', 'Unrestricted')
const zkt = await fund('ZKT', 'Zakat fund', 'Zakat')
const meals = await fund('MEALS', 'School meals appeal', 'Restricted')
const endow = await fund('ENDOW', 'Scholarship endowment', 'Endowment')
const program = await ok(call(gm, 'POST', '/ngo/programs', { entityId: E, code: 'EDU', name: 'Education', sector: 'Education', targetBeneficiaries: 500, isActive: true }), 'program')

// ---- donors & donations ----
const badCnic = await call(fr, 'POST', '/ngo/donors', { name: 'X', type: 'Individual', cnic: '12345' })
check(badCnic.status === 400, 'donor CNIC format is validated')
const ali = await ok(call(fr, 'POST', '/ngo/donors', { name: 'Ali Raza', type: 'Individual', cnic: '35202-9876543-1', phone: '0300-1234567', city: 'Lahore' }), 'ali')
const engro = await ok(call(fr, 'POST', '/ngo/donors', { name: 'Engro Foundation', type: 'Foundation', ntn: '1234567-8' }), 'engro')
const grt = await ok(call(fr, 'POST', '/ngo/donors', { name: 'Global Relief Trust', type: 'Institutional', country: 'UK' }), 'grt')
const give = (donor, f, amount, extra = {}) => call(fr, 'POST', '/ngo/donations', { entityId: E, date: day(0), donorId: donor.id, fundId: f.id, amount,
  method: 'BankTransfer', bankAccountId: acc('1120'), ...extra })
const d1 = await ok(give(ali, zkt, 50000, { reference: 'Zakat 1448' }), 'd1')
check(d1.number.startsWith('DON-') && d1.amountInWords === 'Rupees Fifty Thousand Only', 'donation receipt numbered with the amount in words', d1.amountInWords)
await ok(give(engro, gen, 200000), 'd2')
const cashToBank = await give(ali, meals, 30000, { method: 'Cash' })
check(cashToBank.status === 400, 'cash donations must go into a cash account')
await ok(give(ali, meals, 30000, { method: 'Cash', bankAccountId: acc('1110') }), 'd3')
await ok(give(engro, endow, 1000000), 'd4')

// ---- grant (PKR) ----
const grantBody = tranches => ({ entityId: E, title: 'Girls education — Muzaffargarh', donorId: engro.id, agreementRef: 'EF/2026/014', programId: program.id,
  startDate: day(-30), endDate: day(335), reportingFrequency: 'Quarterly', flexibilityPercent: 10,
  budgetLines: [
    { code: 'PER', description: 'Teachers and field staff', category: 'Personnel', amount: 600000, expenseAccountId: acc('6100') },
    { code: 'ASS', description: 'Cash stipends to families', category: 'Assistance', amount: 300000, expenseAccountId: acc('6950') },
    { code: 'ACT', description: 'Learning materials', category: 'Activities', amount: 100000, expenseAccountId: acc('6960') }],
  tranches })
const frGrant = await call(fr, 'POST', '/ngo/grants', grantBody([{ dueDate: day(-25), amount: 1000000 }]))
check(frGrant.status === 403, 'fundraising officers cannot create grants')
let g1 = await ok(call(gm, 'POST', '/ngo/grants', grantBody([{ dueDate: day(-25), amount: 600000 }, { dueDate: day(150), amount: 300000 }])), 'g1')
check(g1.status === 'Proposal' && g1.amount === 1000000 && g1.fundCode === g1.number, 'grant drafted with its own restricted fund')
const mismatch = await call(gm, 'POST', `/ngo/grants/${g1.id}/activate`)
check(mismatch.status === 400 && /add up/.test(mismatch.data?.title), 'tranches must add up to the budget before approval', mismatch.data?.title)
g1 = await ok(call(gm, 'PUT', `/ngo/grants/${g1.id}`, grantBody([{ id: g1.tranches[0].id, dueDate: day(-25), amount: 600000 }, { id: g1.tranches[1].id, dueDate: day(150), amount: 400000 }])), 'fix')
const poApprove = await call(po, 'POST', `/ngo/grants/${g1.id}/activate`)
check(poApprove.status === 403, 'program officers cannot approve grants')
g1 = await ok(call(gm, 'POST', `/ngo/grants/${g1.id}/activate`), 'activate')
const final = g1.reports.at(-1)
check(g1.status === 'Active' && g1.reports.length >= 4 && final.title === 'Final report' && final.dueDate === day(395), 'approved; quarterly reports scheduled, final report due 60 days after the end',
  g1.reports.map(r => [r.title, r.dueDate]))
g1 = await ok(call(gm, 'POST', `/ngo/grants/${g1.id}/tranches`, { trancheId: g1.tranches[0].id, date: day(0), amount: 600000, bankAccountId: acc('1120') }), 'tranche')
check(g1.receivedBase === 600000 && g1.unspentBase === 600000, 'tranche 1 received into deferred income')

const charge = body => call(gm, 'POST', '/ngo/expenses', { date: day(0), function: 'Program', ...body })
await ok(charge({ grantId: g1.id, budgetLineId: g1.budgetLines[0].id, description: 'Staff time Jul–Sep (payroll)', amount: 450000, allocationOnly: true }), 'alloc')
const printer = await ok(call(owner, 'POST', '/finance/contacts', { code: 'V-PRINT', name: 'Multan Printers', isVendor: true, isCustomer: false, isActive: true, paymentTermsDays: 15 }), 'vendor')
await ok(charge({ grantId: g1.id, budgetLineId: g1.budgetLines[2].id, description: 'Workbooks for 300 girls', amount: 105000, vendorId: printer.id }), 'bill')
const over = await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[2].id, description: 'More books', amount: 6000, paidFromAccountId: acc('1110') })
check(over.status === 400 && /flexibility/.test(over.data?.title), 'budget line can go 10% over (110,000) but not to 111,000', over.data?.title)
const early = await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[2].id, description: 'Pre-award cost', amount: 1000, paidFromAccountId: acc('1110'), date: day(-40) })
check(early.status === 400, 'costs before the grant start date are refused')

// ---- beneficiaries & assistance ----
const ben = (name, cnic, extra = {}) => call(po, 'POST', '/ngo/beneficiaries', { entityId: E, fullName: name, cnic, gender: 'Female', district: 'Muzaffargarh',
  householdSize: 7, zakatEligible: false, programId: program.id, isActive: true, ...extra })
const fatima = await ok(ben('Fatima Bibi', '32304-1111111-2', { zakatEligible: true, vulnerabilities: 'Widow' }), 'fatima')
const ahmed = await ok(ben('Ahmed Khan', '32304-2222222-3', { gender: 'Male' }), 'ahmed')
const dup = await ben('Fatima B.', '32304-1111111-2')
check(dup.status === 400 && dup.data?.title.includes(fatima.registrationNo), 'duplicate CNIC is refused and names the existing registration', dup.data?.title)
const kit = await ok(call(po, 'POST', `/ngo/beneficiaries/${fatima.id}/assistance`, { date: day(0), type: 'InKind', description: 'School kit', quantity: 1, value: 2500, allowRepeat: false }), 'kit')
check(kit.assistance.length === 1 && kit.assistanceValue === 2500, 'program officer records in-kind help')
const poCash = await call(po, 'POST', `/ngo/beneficiaries/${fatima.id}/assistance`, { date: day(0), type: 'Cash', description: 'Stipend', value: 5000, fundId: gen.id, paidFromAccountId: acc('1110') })
check(poCash.status === 403, 'cash assistance needs spending permission (program officers cannot pay out)')
const assist = (b, body) => call(owner, 'POST', `/ngo/beneficiaries/${b.id}/assistance`, { date: day(0), type: 'Cash', allowRepeat: false, ...body })
const notEligible = await assist(ahmed, { description: 'Zakat — rent', value: 20000, fundId: zkt.id, paidFromAccountId: acc('1120') })
check(notEligible.status === 400 && /Zakat-eligible/.test(notEligible.data?.title), 'Zakat only reaches verified-eligible beneficiaries')
await ok(assist(fatima, { description: 'Zakat — rent support', value: 20000, fundId: zkt.id, paidFromAccountId: acc('1120') }), 'zakat')
const repeat = await assist(fatima, { description: 'Zakat — again', value: 25000, fundId: zkt.id, paidFromAccountId: acc('1120') })
check(repeat.status === 400 && /allow repeat/.test(repeat.data?.title), 'the same cash help within 30 days is flagged')
const short = await assist(fatima, { description: 'Zakat — again', value: 35000, fundId: zkt.id, paidFromAccountId: acc('1120'), allowRepeat: true })
check(short.status === 400 && /30,000/.test(short.data?.title), 'Zakat fund cannot go below zero (30,000 left)', short.data?.title)
const zakatAdmin = await charge({ fundId: zkt.id, entityId: E, accountId: acc('6500'), function: 'ManagementGeneral', description: 'Office rent', amount: 1000, paidFromAccountId: acc('1120') })
check(zakatAdmin.status === 400, 'Zakat cannot pay for administration')
await ok(assist(ahmed, { description: 'Monthly school stipend', value: 15000, grantId: g1.id, budgetLineId: g1.budgetLines[1].id, paidFromAccountId: acc('1110') }), 'stipend')

// ---- other funds ----
await ok(charge({ fundId: meals.id, entityId: E, programId: program.id, accountId: acc('6960'), description: 'Meals for 40 children', amount: 12000, paidFromAccountId: acc('1110') }), 'meals')
const mealsOver = await charge({ fundId: meals.id, entityId: E, accountId: acc('6960'), description: 'More meals', amount: 20000, paidFromAccountId: acc('1110') })
check(mealsOver.status === 400 && /18,000/.test(mealsOver.data?.title), 'restricted fund spending is capped at what is left (18,000)', mealsOver.data?.title)
await ok(charge({ fundId: gen.id, entityId: E, accountId: acc('6500'), function: 'ManagementGeneral', description: 'Office running costs', amount: 40000, paidFromAccountId: acc('1120') }), 'mg')
await ok(charge({ fundId: gen.id, entityId: E, accountId: acc('6500'), function: 'Fundraising', description: 'Ramadan appeal printing', amount: 10000, paidFromAccountId: acc('1120') }), 'fr')
const endowSpend = await charge({ fundId: endow.id, entityId: E, accountId: acc('6500'), description: 'Use capital', amount: 1000, paidFromAccountId: acc('1120') })
check(endowSpend.status === 400, 'endowment capital cannot be spent')

g1 = await ok(call(gm, 'GET', `/ngo/grants/${g1.id}`), 'g1')
const act = g1.budgetLines.find(l => l.code === 'ACT')
check(g1.spentBase === 570000 && g1.unspentBase === 30000 && g1.burnPercent === 57 && act.actual === 105000 && act.overBudget && g1.warnings.some(w => w.includes('ACT')),
  'grant: spent 450,000 + 105,000 + 15,000 = 570,000 (57%); ACT 5% over budget is flagged', [g1.spentBase, g1.unspentBase, g1.burnPercent, act])

// ---- USD grant ----
let g2 = await ok(call(gm, 'POST', '/ngo/grants', { entityId: E, title: 'Flood relief cash transfers', donorId: grt.id, currency: 'USD', agreementRate: 280,
  startDate: day(-10), endDate: day(170), reportingFrequency: 'EndOnly', flexibilityPercent: 0,
  budgetLines: [{ code: 'CASH', description: 'Unconditional cash transfers', category: 'Assistance', amount: 10000, expenseAccountId: acc('6950') }],
  tranches: [{ dueDate: day(0), amount: 10000 }] }), 'g2')
g2 = await ok(call(gm, 'POST', `/ngo/grants/${g2.id}/activate`), 'activate g2')
check(g2.reports.length === 1, 'end-of-grant reporting: just the final report')
g2 = await ok(call(gm, 'POST', `/ngo/grants/${g2.id}/tranches`, { trancheId: g2.tranches[0].id, date: day(0), amount: 10000, rate: 285, bankAccountId: acc('1120') }), 'usd tranche')
await ok(charge({ grantId: g2.id, budgetLineId: g2.budgetLines[0].id, description: 'Transfers to 95 families', amount: 285000, paidFromAccountId: acc('1120') }), 'g2 spend')
g2 = await ok(call(gm, 'GET', `/ngo/grants/${g2.id}`), 'g2')
check(g2.receivedBase === 2850000 && g2.averageRate === 285 && g2.budgetLines[0].actual === 1000 && g2.spent === 1000,
  'USD grant: $10,000 received at 285 = Rs 2,850,000; Rs 285,000 spent reports as $1,000', [g2.receivedBase, g2.averageRate, g2.budgetLines[0].actual])

// ---- reports ----
const funds = await ok(call(gm, 'GET', '/ngo/funds'), 'funds')
const bal = code => funds.find(f => f.code === code)?.balance
check(bal('ZKT') === 30000 && bal('MEALS') === 18000 && bal('GEN') === 150000 && bal('ENDOW') === 1000000 && bal(g1.number) === 30000,
  'fund balances: Zakat 30,000 · meals 18,000 · general 150,000 · endowment 1,000,000 · grant 30,000', funds.map(f => [f.code, f.balance]))
const fx = await ok(call(gm, 'GET', '/ngo/reports/functional'), 'functional')
// Program: 570,000 grant + 285,000 USD grant + 20,000 Zakat + 12,000 meals = 887,000; M&G 40,000; fundraising 10,000.
check(fx.programCost === 887000 && fx.managementGeneral === 40000 && fx.fundraising === 10000 && fx.programRatio === 94.7,
  'functional expenses: program 887,000 of 937,000 = 94.7%', [fx.programCost, fx.managementGeneral, fx.fundraising, fx.programRatio])
const donors = await ok(call(gm, 'GET', '/ngo/reports/donors'), 'donors')
const eng = donors.find(d => d.donorName === 'Engro Foundation')
check(eng.donations === 1200000 && eng.gifts === 2 && eng.grantsCommitted === 1000000 && eng.grantsReceived === 600000, 'donor summary for Engro', eng)

// ---- close the PKR grant ----
for (const r of g1.reports) await ok(call(gm, 'POST', `/ngo/grants/${g1.id}/reports/${r.id}/submit`, { submittedOn: day(0) }), 'submit')
const noRefund = await call(gm, 'POST', `/ngo/grants/${g1.id}/close`, {})
check(noRefund.status === 400 && /30,000/.test(noRefund.data?.title), 'closing with 30,000 unspent needs a refund account')
g1 = await ok(call(gm, 'POST', `/ngo/grants/${g1.id}/close`, { refundFromAccountId: acc('1120') }), 'close')
check(g1.status === 'Closed', 'grant closed and the unspent balance refunded')
const afterClose = await charge({ grantId: g1.id, budgetLineId: g1.budgetLines[0].id, description: 'Late cost', amount: 100, paidFromAccountId: acc('1110') })
check(afterClose.status === 400, 'a closed grant takes no more costs')

const dash = await ok(call(gm, 'GET', '/ngo/dashboard'), 'dashboard')
check(dash.activeGrants === 1 && dash.donationsThisMonth === 1280000 && dash.activeBeneficiaries === 2 && dash.assistedThisMonth === 2,
  'dashboard: 1 active grant, 1,280,000 donated this month, 2 beneficiaries assisted', [dash.activeGrants, dash.donationsThisMonth, dash.assistedThisMonth])

const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb')
const row = c => tb.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }
// 2220: 600,000 + 2,850,000 + 30,000 in; 570,000 + 30,000 refund + 285,000 + 12,000 out → 2,583,000.
check(row('2220').credit === 2583000 && row('2230').credit === 30000 && row('4310').credit === 887000 && row('4300').credit === 200000 &&
  row('3300').credit === 1000000 && row('6950').debit === 320000 && row('1120').debit === 4315000 && row('1110').debit === 3000 && tb.totalDebit === tb.totalCredit,
  'ledger: deferred 2,583,000 · Zakat 30,000 · released 887,000 · bank 4,315,000 · books balance',
  ['2220', '2230', '4310', '4300', '3300', '6950', '1120', '1110'].map(c => [c, row(c).debit, row(c).credit]))

console.log(failures === 0 ? '\nALL NGO CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
