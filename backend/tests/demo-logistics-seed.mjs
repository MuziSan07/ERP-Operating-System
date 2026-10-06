// Adds Logistics demo data to "Northern Logistics" (run the other demo seeds first).
// dispatcher@demo.erpos.local (Dispatcher) uses the demo password.
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
if ((await call(owner, 'GET', '/logistics/routes')).length > 0) { console.log('Logistics demo data already present.'); process.exit(0) }

const entities = await call(owner, 'GET', '/entities')
const LOGI = entities.find(e => e.code === 'LOGI').id
const roles = await call(owner, 'GET', '/roles')
await call(owner, 'POST', '/hr/employees', { data: { employeeCode: 'GB-310', fullName: 'Danish Dispatcher', entityId: LOGI, employmentType: 'Permanent', status: 'Active',
  joinDate: '2025-07-15', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email: 'dispatcher@demo.erpos.local', password: DEMO_PASSWORD,
  userType: 'Employee', roleId: roles.find(r => r.name === 'Dispatcher').id, monthlyGross: 85000 })
const disp = await login('dispatcher@demo.erpos.local')

const accounts = await call(owner, 'GET', '/finance/accounts')
const acc = c => accounts.find(a => a.code === c).id
const contact = b => call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 30, isActive: true, isCustomer: false, isVendor: false, ...b })
const nishat = await contact({ code: 'C-NISHAT', name: 'Nishat Mills', isCustomer: true, ntn: '0225678-1' })
const fauji = await contact({ code: 'C-FFC', name: 'Fauji Fertilizer depot', isCustomer: true })
const daraz = await contact({ code: 'C-SELLER', name: 'Hamza Online Store (COD seller)', isCustomer: true, phone: '0333-1112223' })
const walkin = await contact({ code: 'C-WALKIN', name: 'Walk-in customers', isCustomer: true })
const broker = await contact({ code: 'V-GTS', name: 'Gujranwala Goods Transport (broker)', isVendor: true })
const pso = await contact({ code: 'V-PSO', name: 'PSO fuel station (credit)', isVendor: true, paymentTermsDays: 15 })
const workshop = await contact({ code: 'V-HINO', name: 'Hino Pak service centre', isVendor: true })

const route = b => call(disp, 'POST', '/logistics/routes', { isActive: true, ...b })
const rLhe = await route({ code: 'RWP-LHE', origin: 'Rawalpindi', destination: 'Lahore', distanceKm: 380, standardHours: 8, ratePerKg: 9, minimumCharge: 800, fullTruckRate: 65000, fuelSurchargePercent: 8 })
const rKhi = await route({ code: 'RWP-KHI', origin: 'Rawalpindi', destination: 'Karachi', distanceKm: 1420, standardHours: 30, ratePerKg: 20, minimumCharge: 1800, fullTruckRate: 210000, fuelSurchargePercent: 10 })
const rPew = await route({ code: 'RWP-PEW', origin: 'Rawalpindi', destination: 'Peshawar', distanceKm: 175, standardHours: 4, ratePerKg: 6, minimumCharge: 600, fullTruckRate: 38000, fuelSurchargePercent: 8 })

const vehicle = b => call(disp, 'POST', '/logistics/vehicles', { entityId: LOGI, isHired: false, status: 'Available', ...b })
const hino = await vehicle({ registrationNo: 'RIA-4521', type: 'Truck', makeModel: 'Hino 500 FM', capacityKg: 12000, fitnessExpiry: day(180), insuranceExpiry: day(240), routePermitExpiry: day(300), tokenTaxExpiry: day(90), odometer: 184200 })
const isuzu = await vehicle({ registrationNo: 'RIB-0917', type: 'MiniTruck', makeModel: 'Isuzu NPR', capacityKg: 4000, fitnessExpiry: day(120), insuranceExpiry: day(12), routePermitExpiry: day(200), tokenTaxExpiry: day(60), odometer: 96350 })
await vehicle({ registrationNo: 'RIC-3300', type: 'Container40', makeModel: 'Volvo FH prime mover', capacityKg: 26000, fitnessExpiry: day(-8), insuranceExpiry: day(150), routePermitExpiry: day(150), tokenTaxExpiry: day(150), odometer: 412000 })
const shehzore = await vehicle({ registrationNo: 'LXR-7781', type: 'Pickup', makeModel: 'Hyundai Shehzore', capacityKg: 1500, isHired: true, ownerVendorId: broker.id, fitnessExpiry: day(90), insuranceExpiry: day(90), odometer: 61000 })

const driver = b => call(disp, 'POST', '/logistics/drivers', { entityId: LOGI, isActive: true, ...b })
const naveed = await driver({ fullName: 'Naveed Ahmed', cnic: '37405-1234567-3', phone: '0301-5550101', licenseNo: 'RWP-HTV-55102', licenseCategory: 'HTV', licenseExpiry: day(500), dailyAllowance: 2500 })
const sajid = await driver({ fullName: 'Sajid Mehmood', cnic: '37405-7654321-1', phone: '0302-5550202', licenseNo: 'RWP-LTV-77345', licenseCategory: 'LTV', licenseExpiry: day(20), dailyAllowance: 2000 })
const imran = await driver({ fullName: 'Imran Khan Afridi', phone: '0313-5550303', licenseNo: 'PEW-LTV-11920', licenseCategory: 'LTV', licenseExpiry: day(300), dailyAllowance: 1800 })

const cn = (customer, r, kg, mode, extra = {}) => call(disp, 'POST', '/logistics/shipments', { entityId: LOGI, customerId: customer.id, bookingDate: day(extra.daysAgo ? -extra.daysAgo : 0),
  shipperName: customer.name, shipperPhone: '051-5551234', consigneeName: 'Receiver', originCity: r.origin, destinationCity: r.destination, routeId: r.id, service: 'PartLoad',
  pieces: Math.max(1, Math.round(kg / 40)), weightKg: kg, declaredValue: kg * 400, paymentMode: mode, otherCharges: 0, codAmount: 0, ...extra })

// Karachi run: delivered last week.
const k1 = await cn(nishat, rKhi, 5200, 'Account', { daysAgo: 6, consigneeName: 'Nishat Karachi warehouse', consigneeAddress: 'SITE Area, Karachi', goodsDescription: 'Yarn cones' })
const k2 = await cn(daraz, rKhi, 35, 'ToPay', { daysAgo: 6, consigneeName: 'Ayesha Siddiqui', consigneePhone: '0321-8889990', consigneeAddress: 'Gulshan-e-Iqbal Block 7', codAmount: 8500, goodsDescription: 'Kitchen appliances' })
const k3 = await cn(walkin, rKhi, 420, 'Prepaid', { daysAgo: 6, shipperName: 'Mr. Raza', consigneeName: 'Raza Traders', paidIntoAccountId: acc('1110'), goodsDescription: 'Auto parts' })
const t1 = await call(disp, 'POST', '/logistics/trips', { entityId: LOGI, vehicleId: hino.id, driverId: naveed.id, routeId: rKhi.id, plannedDate: day(-6) })
await call(disp, 'POST', `/logistics/trips/${t1.id}/load`, { shipmentIds: [k1.id, k2.id, k3.id] })
await call(disp, 'POST', `/logistics/trips/${t1.id}/dispatch`, { odometerStart: 184200 })
await call(disp, 'POST', `/logistics/trips/${t1.id}/expenses`, { type: 'Fuel', description: 'Diesel 260 L', amount: 74000, vendorId: pso.id, date: day(-6) })
await call(disp, 'POST', `/logistics/trips/${t1.id}/expenses`, { type: 'Tolls', description: 'M-2 / M-4 / M-5', amount: 7800, paidFromAccountId: acc('1110'), date: day(-5) })
await call(disp, 'POST', `/logistics/trips/${t1.id}/expenses`, { type: 'DriverAllowance', description: '3 days', amount: 7500, paidFromAccountId: acc('1110'), date: day(-4) })
await call(disp, 'POST', `/logistics/trips/${t1.id}/arrive`, { odometerEnd: 185640 })
await call(disp, 'POST', `/logistics/shipments/${k1.id}/deliver`, { receivedBy: 'Store keeper — Asif' })
await call(disp, 'POST', `/logistics/shipments/${k2.id}/deliver`, { receivedBy: 'Ayesha Siddiqui', codCollected: 8500, collectedIntoAccountId: acc('1110') })
// k3 still at the Karachi hub, out for delivery.
await call(disp, 'POST', `/logistics/shipments/${k3.id}/status`, { status: 'OutForDelivery', location: 'Karachi', remarks: 'With rider Shoaib' })

// Lahore run: on the road now.
const l1 = await cn(fauji, rLhe, 2600, 'Account', { daysAgo: 1, consigneeName: 'FFC Lahore depot', goodsDescription: 'Fertilizer bags (52)' })
const l2 = await cn(daraz, rLhe, 18, 'ToPay', { daysAgo: 1, consigneeName: 'Bilal Ahmed', consigneePhone: '0300-4445556', consigneeAddress: 'DHA Phase 5, Lahore', codAmount: 4200 })
const t2 = await call(disp, 'POST', '/logistics/trips', { entityId: LOGI, vehicleId: isuzu.id, driverId: imran.id, routeId: rLhe.id, plannedDate: day(0) })
await call(disp, 'POST', `/logistics/trips/${t2.id}/load`, { shipmentIds: [l1.id, l2.id] })
await call(disp, 'POST', `/logistics/trips/${t2.id}/dispatch`, { odometerStart: 96350 })
await call(disp, 'POST', `/logistics/trips/${t2.id}/expenses`, { type: 'Fuel', amount: 18000, paidFromAccountId: acc('1110') })

// Peshawar: planned on the hired pickup, plus fresh bookings waiting to be loaded.
const p1 = await cn(nishat, rPew, 900, 'Account', { consigneeName: 'Nishat Peshawar outlet' })
await cn(walkin, rPew, 60, 'Prepaid', { shipperName: 'Ms. Sana', consigneeName: 'Sana Boutique', paidIntoAccountId: acc('1110') })
await cn(daraz, rKhi, 12, 'ToPay', { consigneeName: 'Usman Tariq', consigneeAddress: 'North Nazimabad, Karachi', codAmount: 3100 })
await cn(nishat, rLhe, 3400, 'Account', { consigneeName: 'Nishat Lahore warehouse', goodsDescription: 'Greige fabric' })
const t3 = await call(disp, 'POST', '/logistics/trips', { entityId: LOGI, vehicleId: shehzore.id, driverId: sajid.id, routeId: rPew.id, plannedDate: day(1) })
await call(disp, 'POST', `/logistics/trips/${t3.id}/load`, { shipmentIds: [p1.id] })
await call(disp, 'POST', `/logistics/trips/${t3.id}/expenses`, { type: 'VehicleHire', description: 'Shehzore hire RWP-PEW', amount: 9000, vendorId: broker.id })

await call(disp, 'POST', '/logistics/maintenance', { vehicleId: hino.id, date: day(-2), description: 'Oil, filters & brake pads after Karachi run', odometer: 185700, cost: 32500, vendorId: workshop.id, nextServiceDate: day(45) })
await call(owner, 'POST', '/logistics/cod/remit', { customerId: daraz.id, bankAccountId: acc('1120'), reference: 'IBFT weekly COD' })
console.log('Logistics demo data added: 3 routes, 4 vehicles, 3 drivers, 10 consignments, 3 trips.')
