// End-to-end test of Travel & Tours: packages, departures (seats, guides, costs), bookings with passengers,
// tickets and visas, deposits, invoicing, supplier bills, manifest and profitability.
// Usage: node tests/travel-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@trv.test`.toLowerCase()
const day = n => new Date(Date.now() + n * 86400_000).toISOString().slice(0, 10)
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
await ok(call(platform, 'POST', '/platform/tenants', { name: `Travel Test ${run}`, code: `TRV${run}`, industry: 'Tourism', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['travel', 'tourism', 'finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const role = n => roles.find(r => r.name === n).id
check(roles.some(r => r.name === 'Travel Consultant') && roles.some(r => r.name === 'Tour Operator'), 'Travel Consultant and Tour Operator default roles exist')
for (const [n, r] of [['consultant', 'Travel Consultant'], ['operator', 'Tour Operator']])
  await ok(call(owner, 'POST', '/users', { email: mail(n), fullName: n, password: pw, userType: 'Employee', primaryEntityId: E, roleId: role(r) }), n)
const consultant = await login(mail('consultant')), operator = await login(mail('operator'))
const pst = (await ok(call(owner, 'GET', '/finance/tax-rates'), 'taxes')).find(t => t.code === 'PST-PB').id
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c).id
const contact = body => ok(call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 15, isActive: true, isCustomer: false, isVendor: false, ...body }), body.code)
const coach = await contact({ code: 'V-COACH', name: 'Northern Coaches', isVendor: true })
const pia = await contact({ code: 'V-PIA', name: 'PIA', isVendor: true })
const embassy = await contact({ code: 'V-VFS', name: 'Visa facilitation centre', isVendor: true })
const ahmed = await contact({ code: 'C-AHMED', name: 'Ahmed family', isCustomer: true, phone: '0300-1111111' })
const sara = await contact({ code: 'C-SARA', name: 'Sara Corporate Group', isCustomer: true })

// ---- package, departures, guides ----
const pkgBody = { entityId: E, code: 'HNZ5', name: 'Hunza Cherry Blossom', destination: 'Hunza & Nagar', durationDays: 5, adultPrice: 60000, childPrice: 40000,
  singleSupplement: 12000, taxRateId: pst, isActive: true, inclusions: 'Transport, hotels, breakfast & dinner, guide', exclusions: 'Air fare, lunches',
  itinerary: [{ dayNumber: 1, title: 'Islamabad → Chilas', overnight: 'Chilas' }, { dayNumber: 2, title: 'Chilas → Hunza', overnight: 'Karimabad' },
    { dayNumber: 3, title: 'Altit & Baltit forts', overnight: 'Karimabad' }, { dayNumber: 4, title: 'Attabad Lake & Passu', overnight: 'Karimabad' }, { dayNumber: 5, title: 'Return', overnight: null }] }
const blocked = await call(consultant, 'POST', '/tours/packages', pkgBody)
check(blocked.status === 403, 'travel consultants cannot create tour packages')
const pkg = await ok(call(operator, 'POST', '/tours/packages', pkgBody), 'package')
check(pkg.itinerary.length === 5, 'package with a 5-day itinerary')
const dep = await ok(call(operator, 'POST', '/tours/departures', { tourPackageId: pkg.id, startDate: day(20), capacity: 10 }), 'departure')
check(dep.endDate === day(24) && dep.adultPrice === 60000 && dep.seatsLeft === 10, 'departure ends after 5 days, 10 seats at package price')
const dup = await call(operator, 'POST', '/tours/departures', { tourPackageId: pkg.id, startDate: day(20), capacity: 10 })
check(dup.status === 400, 'cannot create the same departure twice')
const pkg2 = await ok(call(operator, 'POST', '/tours/packages', { ...pkgBody, code: 'SKD3', name: 'Skardu Explorer', destination: 'Skardu', durationDays: 3, itinerary: [] }), 'pkg2')
const dep2 = await ok(call(operator, 'POST', '/tours/departures', { tourPackageId: pkg2.id, startDate: day(22), capacity: 12 }), 'dep2')
const karim = await ok(call(operator, 'POST', '/tours/guides', { fullName: 'Karim Baig', phone: '0355-1234567', languages: 'Urdu, English, Burushaski', dailyRate: 5000, isActive: true }), 'guide')
await ok(call(operator, 'POST', `/tours/departures/${dep.id}/guides`, { guideId: karim.id, role: 'Lead guide' }), 'assign guide')
const clash = await call(operator, 'POST', `/tours/departures/${dep2.id}/guides`, { guideId: karim.id })
check(clash.status === 400, 'a guide cannot lead two overlapping departures')
const withCost = await ok(call(operator, 'POST', `/tours/departures/${dep.id}/costs`, { description: 'Coaster with driver, 5 days', vendorId: coach.id, amount: 150000 }), 'cost')
const billed = await ok(call(operator, 'POST', `/tours/departures/${dep.id}/costs/${withCost.costs[0].id}/bill`), 'bill cost')
check(!!billed.costs[0].billId, 'departure cost drafted as a vendor bill')

// ---- bookings ----
const booking1 = await ok(call(consultant, 'POST', '/travel/bookings', { entityId: E, customerId: ahmed.id, notes: 'Vegetarian meals',
  passengers: [{ fullName: 'Ahmed Raza', type: 'Adult', cnic: '35202-1111111-1', passportNo: 'AB1234567', passportExpiry: day(60) },
    { fullName: 'Fatima Raza', type: 'Adult', cnic: '35202-2222222-2' }, { fullName: 'Ali Raza', type: 'Child' }],
  items: [{ type: 'TourSeats', tourDepartureId: dep.id, adults: 2, children: 1, quantity: 1, unitCost: 0, unitPrice: 0 },
    { type: 'Flight', airline: 'PIA', route: 'ISB-GIL', serviceDate: day(20), supplierId: pia.id, quantity: 3, unitCost: 14000, unitPrice: 16000, pnr: 'x7k2pq' }] }), 'booking 1')
check(booking1.sellTotal === 208000 && booking1.costTotal === 42000 && booking1.margin === 166000, 'booking: tour 160,000 + tickets 48,000; cost 42,000; margin 166,000', [booking1.sellTotal, booking1.costTotal])
check(booking1.items.find(i => i.type === 'Flight').pnr === 'X7K2PQ' && booking1.warnings.some(w => w.includes('Ahmed Raza') && w.includes('6 months')), 'PNR stored; passport expiring within 6 months of travel is flagged', booking1.warnings)
const opBook = await call(operator, 'POST', '/travel/bookings', { entityId: E, customerId: ahmed.id, passengers: [{ fullName: 'X', type: 'Adult' }], items: [{ type: 'Other', description: 'x', quantity: 1, unitCost: 0, unitPrice: 1 }] })
check(opBook.status === 403, 'tour operators cannot create customer bookings')

const booking2 = await ok(call(consultant, 'POST', '/travel/bookings', { entityId: E, customerId: sara.id, passengers: [{ fullName: 'Sara Khan', type: 'Adult' }],
  items: [{ type: 'TourSeats', tourDepartureId: dep.id, adults: 8, children: 0, quantity: 1, unitCost: 0, unitPrice: 0 }] }), 'booking 2')
await ok(call(consultant, 'POST', `/travel/bookings/${booking1.id}/confirm`), 'confirm 1')
const full = await call(consultant, 'POST', `/travel/bookings/${booking2.id}/confirm`)
check(full.status === 400 && /seat/.test(full.data?.title), 'confirming 8 more seats when only 7 are left is refused', full.data?.title)
await ok(call(consultant, 'PUT', `/travel/bookings/${booking2.id}`, { entityId: E, customerId: sara.id, passengers: booking2.passengers.map(p => ({ ...p })),
  items: [{ type: 'TourSeats', tourDepartureId: dep.id, adults: 7, children: 0, quantity: 1, unitCost: 0, unitPrice: 0 }] }), 'reduce')
await ok(call(consultant, 'POST', `/travel/bookings/${booking2.id}/confirm`), 'confirm 2')
const depFull = await ok(call(operator, 'GET', `/tours/departures/${dep.id}`), 'dep')
// Revenue 160,000 + 420,000 = 580,000; cost coach 150,000 + guide 5 × 5,000 = 175,000.
check(depFull.seatsLeft === 0 && depFull.revenue === 580000 && depFull.totalCost === 175000 && depFull.margin === 405000,
  'departure sold out; revenue 580,000, cost 175,000, margin 405,000', [depFull.seatsLeft, depFull.revenue, depFull.totalCost])
const manifest = await ok(call(operator, 'GET', `/tours/departures/${dep.id}/manifest`), 'manifest')
check(manifest.length === 4 && manifest.filter(m => m.passportWarning).length === 1, 'manifest lists 4 passengers, one passport warning')

// ---- money ----
await ok(call(consultant, 'POST', `/travel/bookings/${booking1.id}/deposits`, { amount: 50000, bankAccountId: acc('1120'), reference: 'IBFT 5510' }), 'deposit')
const invoiced = await ok(call(consultant, 'POST', `/travel/bookings/${booking1.id}/invoice`), 'invoice')
const inv = await ok(call(owner, 'GET', `/finance/invoices/${invoiced.invoiceId}`), 'inv')
// Tour 160,000 + 16% = 25,600; tickets 48,000 → 233,600; advance 50,000 applied.
check(inv.total === 233600 && inv.balance === 183600 && inv.status === 'PartiallyPaid', 'invoice 233,600 with the 50,000 advance applied', [inv.total, inv.balance])
const bills = await ok(call(consultant, 'POST', `/travel/bookings/${booking1.id}/supplier-bills`), 'supplier bills')
const piaBill = await ok(call(owner, 'GET', `/finance/bills/${bills.billIds[0]}`), 'pia bill')
check(bills.suppliers === 1 && piaBill.total === 42000 && piaBill.status === 'Draft', 'one draft PIA bill for 42,000 awaiting approval')
const cancelInvoiced = await call(consultant, 'POST', `/travel/bookings/${booking1.id}/cancel`)
check(cancelInvoiced.status === 400, 'an invoiced booking cannot be cancelled from Travel')
await ok(call(consultant, 'POST', `/travel/bookings/${booking2.id}/cancel`), 'cancel 2')
check((await ok(call(operator, 'GET', `/tours/departures/${dep.id}`), 'dep')).seatsLeft === 7, 'cancelling frees the 7 seats')

// ---- visas ----
const umrah = await ok(call(consultant, 'POST', '/travel/bookings', { entityId: E, customerId: ahmed.id, travelDate: day(45),
  passengers: [{ fullName: 'Ahmed Raza', type: 'Adult', passportNo: 'AB7654321', passportExpiry: day(900), nationality: 'Pakistani' }, { fullName: 'Fatima Raza', type: 'Adult', nationality: 'Pakistani' }],
  items: [{ type: 'Visa', country: 'Saudi Arabia', supplierId: embassy.id, quantity: 1, unitCost: 25000, unitPrice: 30000, passengerIndex: 0 },
    { type: 'Visa', country: 'Saudi Arabia', supplierId: embassy.id, quantity: 1, unitCost: 25000, unitPrice: 30000, passengerIndex: 1 }] }), 'visa booking')
check(umrah.warnings.some(w => w.includes('Fatima Raza') && w.includes('passport number missing')), 'visa booking flags a passenger without a passport', umrah.warnings)
const dash = await ok(call(consultant, 'GET', '/travel/dashboard'), 'dashboard')
check(dash.pendingVisas.filter(v => v.bookingNumber === umrah.number).length === 2, 'both visas show as pending on the dashboard')
const visaItem = umrah.items.find(i => i.passengerName === 'Ahmed Raza')
const afterVisa = await ok(call(consultant, 'PUT', `/travel/bookings/${umrah.id}/visas/${visaItem.id}`, { status: 'Approved' }), 'visa approved')
check(afterVisa.items.find(i => i.id === visaItem.id).visaStatus === 'Approved', 'visa status updated to Approved')

const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb')
check(tb.rows.find(r => r.code === '1200')?.debit === 183600 && !tb.rows.find(r => r.code === '2200') && tb.totalDebit === tb.totalCredit,
  'ledger: receivable 183,600, advance cleared, books balance')

console.log(failures === 0 ? '\nALL TRAVEL & TOURS CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
