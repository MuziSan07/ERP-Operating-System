// End-to-end test of Logistics: rate cards, consignments (prepaid / to-pay / account, COD), trips with capacity and
// document checks, dispatch → arrival → delivery with POD, trip expenses, COD remittance and monthly freight billing.
// Usage: node tests/logistics-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@log.test`.toLowerCase()
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
await ok(call(platform, 'POST', '/platform/tenants', { name: `Logistics Test ${run}`, code: `LOG${run}`, industry: 'Logistics', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['logistics', 'finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
check(roles.some(r => r.name === 'Dispatcher'), 'Dispatcher default role exists')
await ok(call(owner, 'POST', '/users', { email: mail('disp'), fullName: 'Dispatcher', password: pw, userType: 'Employee', primaryEntityId: E, roleId: roles.find(r => r.name === 'Dispatcher').id }), 'disp')
const disp = await login(mail('disp'))
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c)?.id
check(['4120', '5130', '5140', '6450', '2210'].every(acc), 'freight revenue, vehicle costs and COD payable accounts seeded')
const contact = b => ok(call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 30, isActive: true, isCustomer: false, isVendor: false, ...b }), b.code)
const gul = await contact({ code: 'C-GUL', name: 'Gul Ahmed Textiles', isCustomer: true })
const ali = await contact({ code: 'C-ALI', name: 'Ali Crafts (online seller)', isCustomer: true })
const cash = await contact({ code: 'C-CASH', name: 'Cash customers', isCustomer: true })
const owner2 = await contact({ code: 'V-BROKER', name: 'Mian Goods Transport (broker)', isVendor: true })
const lube = await contact({ code: 'V-SHELL', name: 'Shell Lube Centre', isVendor: true })

// ---- master data ----
const route = await ok(call(disp, 'POST', '/logistics/routes', { code: 'LHE-KHI', origin: 'Lahore', destination: 'Karachi', distanceKm: 1250, standardHours: 26,
  ratePerKg: 18, minimumCharge: 1500, fullTruckRate: 180000, fuelSurchargePercent: 10, isActive: true }), 'route')
const truck = await ok(call(disp, 'POST', '/logistics/vehicles', { entityId: E, registrationNo: 'les 1234', type: 'Truck', makeModel: 'Hino 500', capacityKg: 10000, isHired: false,
  fitnessExpiry: day(200), insuranceExpiry: day(150), routePermitExpiry: day(300), tokenTaxExpiry: day(100), odometer: 119500, status: 'Available' }), 'truck')
check(truck.registrationNo === 'LES-1234', 'registration number normalised')
const hired = await ok(call(disp, 'POST', '/logistics/vehicles', { entityId: E, registrationNo: 'LXR-999', type: 'MiniTruck', capacityKg: 3000, isHired: true, ownerVendorId: owner2.id,
  fitnessExpiry: day(-1), insuranceExpiry: day(10), odometer: 50000, status: 'Available' }), 'hired')
check(hired.documentAlerts.length === 2, 'hired truck flags expired fitness and insurance expiring soon', hired.documentAlerts)
const aslam = await ok(call(disp, 'POST', '/logistics/drivers', { entityId: E, fullName: 'Muhammad Aslam', cnic: '35201-1111111-1', licenseNo: 'htv-998877', licenseCategory: 'HTV', licenseExpiry: day(400), dailyAllowance: 2000, isActive: true }), 'aslam')
const bashir = await ok(call(disp, 'POST', '/logistics/drivers', { entityId: E, fullName: 'Bashir Ahmed', licenseNo: 'LTV-112233', licenseCategory: 'LTV', licenseExpiry: day(-5), dailyAllowance: 1500, isActive: true }), 'bashir')

// ---- consignments ----
const q = await ok(call(disp, 'POST', '/logistics/quote', { routeId: route.id, service: 'PartLoad', weightKg: 50, otherCharges: 0 }), 'quote')
check(q.freight === 1500 && q.fuelSurcharge === 150 && q.basis === 'Minimum charge', 'quote: 50 kg falls back to the 1,500 minimum + 10% fuel', q)
const cnBody = (customer, kg, mode, extra = {}) => ({ entityId: E, customerId: customer.id, bookingDate: day(0), shipperName: customer.name, consigneeName: 'Receiver Karachi',
  consigneePhone: '0321-0000000', originCity: 'Lahore', destinationCity: 'Karachi', routeId: route.id, service: 'PartLoad', pieces: 10, weightKg: kg, declaredValue: 100000,
  paymentMode: mode, otherCharges: 0, codAmount: 0, ...extra })
const cn1 = await ok(call(disp, 'POST', '/logistics/shipments', cnBody(gul, 2400, 'Account', { goodsDescription: 'Lawn fabric rolls' })), 'cn1')
check(cn1.freight === 43200 && cn1.total === 47520 && !!cn1.promisedDate, 'account CN: 2,400 kg × 18 + 10% fuel = 47,520 with a promised date')
const cn2 = await ok(call(disp, 'POST', '/logistics/shipments', cnBody(ali, 50, 'ToPay', { codAmount: 12000, consigneeName: 'Online buyer — Saddar' })), 'cn2')
const noPay = await call(disp, 'POST', '/logistics/shipments', cnBody(cash, 300, 'Prepaid'))
check(noPay.status === 400, 'prepaid booking needs the account the freight was paid into')
const cn3 = await ok(call(disp, 'POST', '/logistics/shipments', cnBody(cash, 300, 'Prepaid', { paidIntoAccountId: acc('1110') })), 'cn3')
check(cn3.total === 5940 && !!cn3.invoiceNumber, 'prepaid CN 5,940 invoiced and paid at booking')
const cn4 = await ok(call(disp, 'POST', '/logistics/shipments', cnBody(gul, 8000, 'Account')), 'cn4')

// ---- trips ----
const trip = await ok(call(disp, 'POST', '/logistics/trips', { entityId: E, vehicleId: truck.id, driverId: aslam.id, routeId: route.id, plannedDate: day(0) }), 'trip')
await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/load`, { shipmentIds: [cn1.id, cn2.id, cn3.id] }), 'load')
const overload = await call(disp, 'POST', `/logistics/trips/${trip.id}/load`, { shipmentIds: [cn4.id] })
check(overload.status === 400 && /Overload/.test(overload.data?.title), 'loading 8,000 kg more on a 10,000 kg truck (2,750 kg aboard) is refused', overload.data?.title)
const cn5 = await ok(call(disp, 'POST', '/logistics/shipments', cnBody(gul, 100, 'Account')), 'cn5')
const trip2 = await ok(call(disp, 'POST', '/logistics/trips', { entityId: E, vehicleId: hired.id, driverId: bashir.id, routeId: route.id, plannedDate: day(0) }), 'trip2')
await ok(call(disp, 'POST', `/logistics/trips/${trip2.id}/load`, { shipmentIds: [cn5.id] }), 'load2')
const unfit = await call(disp, 'POST', `/logistics/trips/${trip2.id}/dispatch`, {})
check(unfit.status === 400 && /Fitness/.test(unfit.data?.title), 'a vehicle with an expired fitness certificate cannot be dispatched', unfit.data?.title)
await ok(call(disp, 'PUT', `/logistics/vehicles/${hired.id}`, { ...hired, fitnessExpiry: day(90) }), 'renew fitness')
const unlicensed = await call(disp, 'POST', `/logistics/trips/${trip2.id}/dispatch`, {})
check(unlicensed.status === 400 && /licence/.test(unlicensed.data?.title), 'a driver with an expired licence cannot be dispatched', unlicensed.data?.title)

const dispatched = await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/dispatch`, { odometerStart: 120000 }), 'dispatch')
check(dispatched.status === 'Dispatched' && dispatched.shipments.every(s => s.status === 'InTransit'), 'trip dispatched; consignments in transit')
check((await ok(call(disp, 'GET', '/logistics/vehicles'), 'vehicles')).find(v => v.id === truck.id).status === 'OnTrip', 'truck marked on trip')
const early = await call(disp, 'POST', `/logistics/shipments/${cn2.id}/deliver`, { receivedBy: 'X', codCollected: 12000, collectedIntoAccountId: acc('1110') })
check(early.status === 400, "can't deliver before the trip arrives")

await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/expenses`, { type: 'Fuel', description: 'Diesel 210 L', amount: 60000, paidFromAccountId: acc('1120') }), 'fuel')
await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/expenses`, { type: 'Tolls', description: 'M-2/M-5 tolls', amount: 4500, paidFromAccountId: acc('1110') }), 'tolls')
await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/expenses`, { type: 'DriverAllowance', amount: 6000, paidFromAccountId: acc('1110') }), 'allowance')
const arrived = await ok(call(disp, 'POST', `/logistics/trips/${trip.id}/arrive`, { odometerEnd: 121250 }), 'arrive')
// Freight 47,520 + 1,650 + 5,940 = 55,110; expenses 70,500 → margin −15,390 (a light load on a long haul).
check(arrived.freight === 55110 && arrived.expensesTotal === 70500 && arrived.margin === -15390 && arrived.shipments.every(s => s.status === 'AtHub'),
  'trip margin: freight 55,110 − expenses 70,500 = −15,390; consignments at hub', [arrived.freight, arrived.expensesTotal, arrived.margin])

// ---- delivery, COD, billing ----
const shortCod = await call(disp, 'POST', `/logistics/shipments/${cn2.id}/deliver`, { receivedBy: 'Bilal', codCollected: 10000, collectedIntoAccountId: acc('1110') })
check(shortCod.status === 400, 'the full COD amount must be collected')
const delivered = await ok(call(disp, 'POST', `/logistics/shipments/${cn2.id}/deliver`, { receivedBy: 'Bilal (buyer)', codCollected: 12000, collectedIntoAccountId: acc('1110') }), 'deliver')
check(delivered.status === 'Delivered' && delivered.codCollected === 12000 && !!delivered.invoiceNumber, 'delivered with POD; COD 12,000 and to-pay freight collected')
const tracked = await ok(call(disp, 'GET', `/logistics/track/${cn2.number}`), 'track')
check(tracked.events.map(e => e.status).join('>') === 'Booked>InTransit>AtHub>Delivered', 'tracking timeline: booked → in transit → at hub → delivered', tracked.events.map(e => e.status))
await ok(call(disp, 'POST', `/logistics/shipments/${cn1.id}/deliver`, { receivedBy: 'Gul Ahmed warehouse' }), 'deliver cn1')

const dispRemit = await call(disp, 'POST', '/logistics/cod/remit', { customerId: ali.id, bankAccountId: acc('1120') })
check(dispRemit.status === 403, 'dispatchers cannot remit COD')
const remit = await ok(call(owner, 'POST', '/logistics/cod/remit', { customerId: ali.id, bankAccountId: acc('1120'), reference: 'IBFT COD-001' }), 'remit')
check(remit.shipments === 1 && remit.amount === 12000, 'COD 12,000 remitted to the seller')
const bill = await ok(call(disp, 'POST', '/logistics/billing', { customerId: gul.id }), 'bill')
// CN1 47,520 + CN4 (8,000 × 18 + 10%) 158,400 + CN5 (100 kg → 1,800 + 180) 1,980 = 207,900
check(bill.shipments === 3 && bill.total === 207900, 'monthly invoice to Gul Ahmed: 3 consignments, 207,900', bill)
const again = await call(disp, 'POST', '/logistics/billing', { customerId: gul.id })
check(again.status === 400, 'nothing left to bill afterwards')

await ok(call(disp, 'POST', '/logistics/maintenance', { vehicleId: truck.id, date: day(0), description: 'Engine oil & filters', odometer: 121300, cost: 18500, vendorId: lube.id }), 'maintenance')
const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${day(1)}`), 'tb')
const row = c => tb.rows.find(r => r.code === c)
// Cash: +5,940 prepaid +1,650 to-pay +12,000 COD −4,500 tolls −6,000 allowance = 9,090
check(row('1110')?.debit === 9090 && !row('2210') && row('5130')?.debit === 70500 && row('4120')?.credit === 215490 && tb.totalDebit === tb.totalCredit,
  'ledger: cash 9,090, COD payable cleared, running costs 70,500, freight revenue 215,490, books balance', tb.rows.map(r => [r.code, r.debit, r.credit]))
const dash = await ok(call(disp, 'GET', '/logistics/dashboard'), 'dashboard')
check(dash.alerts.some(a => a.subject === 'Bashir Ahmed') && dash.alerts.some(a => a.subject === 'LXR-999' && a.document === 'Insurance'), 'dashboard alerts: expired licence and insurance due')

console.log(failures === 0 ? '\nALL LOGISTICS CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
