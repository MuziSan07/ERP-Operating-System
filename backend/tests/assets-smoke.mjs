// Fixed assets: register (paid now / on a vendor bill / taken on with opening depreciation), monthly depreciation runs
// (straight line, full months, catch-up, no duplicates, no future months), disposal with loss, movement schedule, ledger.
// Usage: node tests/assets-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1xYz`
const mail = n => `${n}.${run}@fa.test`.toLowerCase()
const now = new Date(Date.now() + 5 * 3600_000)
// Month arithmetic on the 1st of the month: m(0) = this month, m(-1) = last month …
const m = k => { const d = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + k, 1)); return { y: d.getUTCFullYear(), mo: d.getUTCMonth() + 1, first: d.toISOString().slice(0, 10) } }
const today = now.toISOString().slice(0, 10)
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
await ok(call(platform, 'POST', '/platform/tenants', { name: `Assets ${run}`, code: `FA${run}`, industry: 'General', maxUsers: 10, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
await ok(call(owner, 'POST', '/users', { email: mail('emp'), fullName: 'Emp', password: pw, userType: 'Employee', primaryEntityId: E, roleId: roles.find(r => r.name === 'Employee').id }), 'emp')
const emp = await login(mail('emp'))
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c)?.id
const cats = await ok(call(owner, 'GET', '/finance/assets/categories'), 'categories')
const cat = c => cats.find(x => x.code === c)
check(cats.length === 6 && cat('IT').usefulLifeMonths === 36 && cat('VEH').usefulLifeMonths === 60 && !!acc('4920'), 'six default asset categories and the disposal gain/loss account')
const empList = await call(emp, 'GET', '/finance/assets')
check(empList.status === 403, 'employees cannot see the asset register')

const vendor = await ok(call(owner, 'POST', '/finance/contacts', { code: 'V-HONDA', name: 'Honda Atlas Cars', isVendor: true, isCustomer: false, isActive: true, paymentTermsDays: 30 }), 'vendor')
const register = body => call(owner, 'POST', '/finance/assets', { entityId: E, salvageValue: 0, openingAccumulatedDepreciation: 0, ...body })
const A = await ok(register({ name: 'Dell laptops (12)', categoryId: cat('IT').id, acquisitionDate: m(-3).first, cost: 360000, acquisition: 'PaidNow', paidFromAccountId: acc('1120') }), 'A')
check(A.code.startsWith('FA-') && A.monthlyCharge === 10000, 'laptops 360,000 over 36 months = 10,000 a month', A.monthlyCharge)
const B = await ok(register({ name: 'Honda City delivery car', categoryId: cat('VEH').id, acquisitionDate: m(-1).first, cost: 3000000, salvageValue: 600000,
  acquisition: 'OnCredit', vendorId: vendor.id, serialNo: 'LEA-21-4455' }), 'B')
check(B.monthlyCharge === 40000, 'car (3,000,000 − 600,000 salvage) / 60 = 40,000 a month', B.monthlyCharge)
const C = await ok(register({ name: 'HP LaserJet printer', categoryId: cat('IT').id, acquisitionDate: '2024-10-01', cost: 120000, acquisition: 'AlreadyInBooks',
  openingAccumulatedDepreciation: 80000, depreciatedThrough: m(-2).first }), 'C')
check(C.bookValue === 40000 && C.monthlyCharge === 3333.33, 'printer taken on at 120,000 with 80,000 already depreciated', [C.bookValue, C.monthlyCharge])
const badSalvage = await register({ name: 'X', categoryId: cat('IT').id, acquisitionDate: today, cost: 1000, salvageValue: 1000, acquisition: 'AlreadyInBooks' })
check(badSalvage.status === 400, 'salvage value must be below cost')

// Last month: laptops catch up three months (30,000), car one month (40,000), printer one month (3,333.33).
const r1 = await ok(call(owner, 'POST', '/finance/assets/depreciation', { entityId: E, year: m(-1).y, month: m(-1).mo }), 'run 1')
const line = (r, id) => r.lines.find(l => l.assetId === id)
check(r1.total === 73333.33 && line(r1, A.id).months === 3 && line(r1, A.id).amount === 30000 && line(r1, B.id).amount === 40000 && !!r1.journalNumber,
  'last month: 30,000 (3 months catch-up) + 40,000 + 3,333.33 = 73,333.33 posted', [r1.total, r1.lines.map(l => [l.assetCode, l.months, l.amount])])
const dup = await call(owner, 'POST', '/finance/assets/depreciation', { entityId: E, year: m(-1).y, month: m(-1).mo })
check(dup.status === 400, 'a month can only be depreciated once')
const future = await call(owner, 'POST', '/finance/assets/depreciation', { entityId: E, year: m(1).y, month: m(1).mo })
check(future.status === 400, 'future months are refused')
const r2 = await ok(call(owner, 'POST', '/finance/assets/depreciation', { entityId: E, year: m(0).y, month: m(0).mo }), 'run 2')
check(r2.total === 53333.33, 'this month: 10,000 + 40,000 + 3,333.33 = 53,333.33', r2.total)
const C2 = await ok(call(owner, 'GET', `/finance/assets/${C.id}`), 'C2')
check(C2.accumulatedDepreciation === 86666.66 && C2.bookValue === 33333.34 && C2.history.length === 2, 'printer: 86,666.66 depreciated, 33,333.34 book value, two charges in its history',
  [C2.accumulatedDepreciation, C2.bookValue, C2.history.length])

// A late-registered asset can't be disposed of before its depreciation is brought up to date.
const D = await ok(register({ name: 'Projector', categoryId: cat('IT').id, acquisitionDate: m(-2).first, cost: 50000, acquisition: 'AlreadyInBooks' }), 'D')
const early = await call(owner, 'POST', `/finance/assets/${D.id}/dispose`, { date: today, proceeds: 0, reason: 'Broken' })
check(early.status === 400 && /depreciation/i.test(early.data?.title), 'disposal waits until depreciation is up to date', early.data?.title)

// Sell the laptops: book value 360,000 − 40,000 = 320,000 against 300,000 received → 20,000 loss.
const sold = await ok(call(owner, 'POST', `/finance/assets/${A.id}/dispose`, { date: today, proceeds: 300000, receivedIntoAccountId: acc('1120'), reason: 'Sold to staff' }), 'dispose')
check(sold.status === 'Disposed' && sold.bookValue === 0 && sold.disposalProceeds === 300000, 'laptops sold for 300,000')
const again = await call(owner, 'POST', `/finance/assets/${A.id}/dispose`, { date: today, proceeds: 1, receivedIntoAccountId: acc('1120') })
check(again.status === 400, 'an asset is disposed of once')

const fyStart = m(0).mo >= 7 ? `${m(0).y}-07-01` : `${m(0).y - 1}-07-01`
const sched = await ok(call(owner, 'GET', `/finance/assets/schedule?from=${fyStart}&to=${today}`), 'schedule')
const it = sched.rows.find(r => r.category === 'Computers & IT equipment'), veh = sched.rows.find(r => r.category === 'Vehicles')
check(it.openingCost === 120000 && it.additions === 410000 && it.disposals === 360000 && it.closingCost === 170000 && it.openingDepreciation === 80000 &&
  it.charge === 46666.66 && it.depreciationOnDisposals === 40000 && it.closingBookValue === 83333.34 && veh.closingBookValue === 2920000 && sched.total.charge === 126666.66,
  'schedule: IT 120,000 b/f + 410,000 additions − 360,000 sold; charge 46,666.66; car book value 2,920,000', [it, veh?.closingBookValue, sched.total.charge])

// This month's depreciation is dated at month end, so read the books as at the end of the month.
const monthEnd = new Date(Date.UTC(m(1).y, m(1).mo - 1, 0)).toISOString().slice(0, 10)
const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${monthEnd}`), 'tb')
const row = c => tb.rows.find(r => r.code === c) ?? { debit: 0, credit: 0 }
check(row('1510').debit === 3000000 && row('1520').credit === 86666.66 && row('6700').debit === 126666.66 && row('4920').debit === 20000 &&
  row('1120').credit === 60000 && row('2110').credit === 3000000 && tb.totalDebit === tb.totalCredit,
  'ledger: cost 3,000,000, accumulated 86,666.66, depreciation 126,666.66, loss 20,000, bank −60,000, books balance',
  ['1510', '1520', '6700', '4920', '1120', '2110'].map(c => [c, row(c).debit, row(c).credit]))

console.log(failures === 0 ? '\nALL FIXED ASSET CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
