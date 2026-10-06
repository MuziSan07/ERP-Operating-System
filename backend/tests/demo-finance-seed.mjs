// Adds finance demo data to "GB Techive Group" (run demo-seed.mjs and demo-hr-seed.mjs first).
// Logins: see demo-seed.mjs. accounts@demo.erpos.local is an Accountant (same demo password).
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken
const owner = await login('owner@demo.erpos.local')
if ((await call(owner, 'GET', '/finance/invoices?pageSize=1')).total > 0) { console.log('Finance demo data already present.'); process.exit(0) }

const entities = await call(owner, 'GET', '/entities')
const E = code => entities.find(e => e.code === code).id
const accounts = await call(owner, 'GET', '/finance/accounts')
const acc = code => accounts.find(a => a.code === code)?.id
const taxes = await call(owner, 'GET', '/finance/tax-rates')
const tax = code => taxes.find(t => t.code === code).id
const roles = await call(owner, 'GET', '/roles')

await call(owner, 'POST', '/hr/employees', { data: { employeeCode: 'GB-002', fullName: 'Bilal Accountant', entityId: E('GBDEMO'), employmentType: 'Permanent', status: 'Active',
  joinDate: '2025-07-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email: 'accounts@demo.erpos.local', password: DEMO_PASSWORD,
  userType: 'Admin', roleId: roles.find(r => r.name === 'Accountant').id, monthlyGross: 160000 })

await call(owner, 'PUT', '/finance/settings', { ...(await call(owner, 'GET', '/finance/settings')), ntn: '7654321-0', strn: '3277876543210' })
const usdBank = acc('1125') ?? (await call(owner, 'POST', '/finance/accounts', { code: '1125', name: 'Bank - USD account (Meezan)', type: 'Asset', subType: 'Bank', parentId: acc('1100'), isGroup: false, currency: 'USD', isActive: true })).id
for (const [date, rate] of [['2026-09-01', 281.2], ['2026-09-15', 281.9], ['2026-10-01', 282.4]])
  await call(owner, 'PUT', '/finance/exchange-rates', { currency: 'USD', date, rate })

const contact = body => call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 30, isActive: true, isVendor: false, isCustomer: false, ...body })
const pia = await contact({ code: 'C-PIA', name: 'PIA Corporate Travel', isCustomer: true, ntn: '0786543-2', city: 'Karachi' })
const ngo = await contact({ code: 'C-UNDP', name: 'UNDP Pakistan (Gilgit project)', isCustomer: true, currency: 'USD', city: 'Islamabad', paymentTermsDays: 15 })
const hotelGuest = await contact({ code: 'C-AGA', name: 'Aga Khan Foundation', isCustomer: true, city: 'Gilgit' })
const kElectric = await contact({ code: 'V-GEPCO', name: 'Gilgit Electricity Dept.', isVendor: true, paymentTermsDays: 10 })
const supplies = await contact({ code: 'V-METRO', name: 'Metro Cash & Carry', isVendor: true, strn: '1700000000001' })

const doc = async (path, body) => { const d = await call(owner, 'POST', `/finance/${path}`, body); return call(owner, 'POST', `/finance/${path}/${d.id}/approve`) }
const inv1 = await doc('invoices', { entityId: E('TRV-ISB'), contactId: pia.id, date: '2026-09-04', reference: 'PO-55102', lines: [
  { description: 'Corporate group tour — Hunza (12 pax)', accountId: acc('4100'), quantity: 12, unitPrice: 45000, taxRateId: tax('PST-ICT') }] })
await doc('invoices', { entityId: E('HTL-SKD'), contactId: hotelGuest.id, date: '2026-09-18', lines: [
  { description: 'Conference hall and rooms, 3 nights', accountId: acc('4100'), quantity: 1, unitPrice: 380000, taxRateId: tax('PST-PB') }] })
const usd = await doc('invoices', { entityId: E('NGO'), contactId: ngo.id, date: '2026-09-10', lines: [
  { description: 'Field logistics support — September', accountId: acc('4300'), quantity: 1, unitPrice: 4200 }] })
await doc('bills', { entityId: E('HTL-SKD'), contactId: kElectric.id, date: '2026-09-28', reference: 'GEPCO-0926', lines: [
  { description: 'Electricity September', accountId: acc('6300'), quantity: 1, unitPrice: 145000, taxRateId: tax('GST18') }] })
await doc('bills', { entityId: E('HTL-SKD'), contactId: supplies.id, date: '2026-09-12', lines: [
  { description: 'Housekeeping supplies', accountId: acc('6500'), quantity: 1, unitPrice: 62000, taxRateId: tax('GST18') }] })

await call(owner, 'POST', '/finance/journals', { entityId: E('GBDEMO'), date: '2026-07-01', description: 'Opening capital', reference: 'OPEN', lines: [
  { accountId: acc('1120'), debit: 25000000, credit: 0 }, { accountId: acc('3100'), debit: 0, credit: 25000000 }] }).then(j => call(owner, 'POST', `/finance/journals/${j.id}/post`))
await call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E('TRV-ISB'), contactId: pia.id, date: '2026-09-25', bankAccountId: acc('1120'),
  amount: 300000, reference: 'IBFT 88231', allocations: [{ documentId: inv1.id, amount: 300000 }] })
await call(owner, 'POST', '/finance/payments', { kind: 'Receipt', entityId: E('NGO'), contactId: ngo.id, date: '2026-09-24', bankAccountId: usdBank,
  exchangeRate: 283.1, amount: 4200, reference: 'SWIFT UNDP-0924', allocations: [{ documentId: usd.id, amount: 4200 }] })

// Post September payroll (prepared by HR in demo-hr-seed) so it reaches the ledger.
const runs = await call(owner, 'GET', '/payroll/runs')
const sep = runs.find(r => r.year === 2026 && r.month === 9 && r.status === 'Draft')
if (sep) {
  await call(owner, 'POST', `/payroll/runs/${sep.id}/approve`)
  await call(owner, 'POST', `/payroll/runs/${sep.id}/post`)
  await call(owner, 'POST', `/finance/payments/payroll/${sep.id}`, { bankAccountId: acc('1120'), date: '2026-10-01' })
}
console.log('Finance demo data added.')
