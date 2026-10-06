// Adds Travel & Tours demo data to "GB Techive Group" (run the other demo seeds first).
// consultant@demo.erpos.local (Travel Consultant) and operator@demo.erpos.local (Tour Operator) use the demo password.
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken
const day = n => new Date(Date.now() + n * 86400_000).toISOString().slice(0, 10)
const owner = await login('owner@demo.erpos.local')
if ((await call(owner, 'GET', '/tours/packages')).length > 0) { console.log('Travel demo data already present.'); process.exit(0) }

const entities = await call(owner, 'GET', '/entities')
const E = code => entities.find(e => e.code === code).id
const TRV = E('TRAVEL')
// Payroll wasn't enabled for the travel company in the Phase 1 demo.
await call(owner, 'PUT', `/entities/${TRV}/modules`, { modules: ['hr', 'payroll', 'finance', 'travel', 'tourism'] })
const roles = await call(owner, 'GET', '/roles')
const role = n => roles.find(r => r.name === n).id
const emp = (code, name, email, roleName) => call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: TRV, employmentType: 'Permanent',
  status: 'Active', joinDate: '2025-08-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email, password: DEMO_PASSWORD, userType: 'Employee', roleId: role(roleName), monthlyGross: 95000 })
await emp('GB-130', 'Zara Consultant', 'consultant@demo.erpos.local', 'Travel Consultant')
await emp('GB-131', 'Omar Operator', 'operator@demo.erpos.local', 'Tour Operator')
const consultant = await login('consultant@demo.erpos.local'), operator = await login('operator@demo.erpos.local')

const pst = (await call(owner, 'GET', '/finance/tax-rates')).find(t => t.code === 'PST-ICT').id
const contact = b => call(owner, 'POST', '/finance/contacts', { paymentTermsDays: 15, isActive: true, isCustomer: false, isVendor: false, ...b })
const coach = await contact({ code: 'V-NATCO', name: 'NATCO Coaches', isVendor: true })
const piaV = await contact({ code: 'V-PIAAIR', name: 'PIA (ticketing)', isVendor: true })
const vfs = await contact({ code: 'V-SAUDIVISA', name: 'Saudi visa facilitation', isVendor: true })
const fam = await contact({ code: 'C-MALIK', name: 'Malik family', isCustomer: true, phone: '0300-9876543' })
const corp = await contact({ code: 'C-ENGRO', name: 'Engro Corp — staff retreat', isCustomer: true, ntn: '0712345-6' })
const umrahC = await contact({ code: 'C-HAJIRA', name: 'Hajira Bibi', isCustomer: true, phone: '0312-5550000' })

const pkg = (code, name, destination, days, adult, child, itinerary) => call(operator, 'POST', '/tours/packages', { entityId: TRV, code, name, destination, durationDays: days,
  adultPrice: adult, childPrice: child, singleSupplement: Math.round(adult * 0.2), taxRateId: pst, isActive: true,
  inclusions: 'AC transport, hotels on twin sharing, breakfast & dinner, guide, entry tickets', exclusions: 'Air fare, lunches, personal expenses', itinerary })
const hunza = await pkg('HUNZA5', 'Hunza Autumn Colours', 'Hunza & Nagar', 5, 65000, 45000, [
  { dayNumber: 1, title: 'Islamabad → Besham', overnight: 'Besham' }, { dayNumber: 2, title: 'Besham → Karimabad via KKH', overnight: 'Karimabad' },
  { dayNumber: 3, title: 'Baltit & Altit forts, Eagle\'s Nest sunset', overnight: 'Karimabad' }, { dayNumber: 4, title: 'Attabad Lake, Passu cones, Khunjerab (optional)', overnight: 'Karimabad' },
  { dayNumber: 5, title: 'Return to Islamabad', overnight: null }])
const skardu = await pkg('SKARDU4', 'Skardu & Deosai', 'Skardu', 4, 72000, 50000, [
  { dayNumber: 1, title: 'Fly Islamabad → Skardu, Shangrila lake', overnight: 'Skardu' }, { dayNumber: 2, title: 'Deosai plains by jeep', overnight: 'Skardu' },
  { dayNumber: 3, title: 'Shigar fort & cold desert', overnight: 'Skardu' }, { dayNumber: 4, title: 'Fly back', overnight: null }])
await pkg('FAIRY3', 'Fairy Meadows Trek', 'Fairy Meadows & Nanga Parbat', 3, 38000, 30000, [])

const dep = (p, start, cap) => call(operator, 'POST', '/tours/departures', { tourPackageId: p.id, startDate: start, capacity: cap })
const h1 = await dep(hunza, day(12), 18), h2 = await dep(hunza, day(33), 18), s1 = await dep(skardu, day(19), 12)
const guide = b => call(operator, 'POST', '/tours/guides', { isActive: true, ...b })
const karim = await guide({ fullName: 'Karim Baig', phone: '0355-1234567', languages: 'Urdu, English, Burushaski', licenseNo: 'GB-TG-0142', dailyRate: 6000 })
const nadia = await guide({ fullName: 'Nadia Hussain', phone: '0346-7654321', languages: 'Urdu, English, Balti', licenseNo: 'GB-TG-0219', dailyRate: 6500 })
await call(operator, 'POST', `/tours/departures/${h1.id}/guides`, { guideId: karim.id, role: 'Lead guide' })
await call(operator, 'POST', `/tours/departures/${s1.id}/guides`, { guideId: nadia.id, role: 'Lead guide' })
await call(operator, 'POST', `/tours/departures/${h1.id}/costs`, { description: 'Coaster with driver, 5 days', vendorId: coach.id, amount: 210000 })
await call(operator, 'POST', `/tours/departures/${h1.id}/costs`, { description: 'Hotels, 4 nights × 9 rooms', amount: 324000 })

const b1 = await call(consultant, 'POST', '/travel/bookings', { entityId: TRV, customerId: fam.id, notes: 'One vegetarian',
  passengers: [{ fullName: 'Tariq Malik', type: 'Adult', cnic: '37405-1234567-1' }, { fullName: 'Sana Malik', type: 'Adult', cnic: '37405-7654321-2' },
    { fullName: 'Hira Malik', type: 'Child' }, { fullName: 'Ali Malik', type: 'Child' }],
  items: [{ type: 'TourSeats', tourDepartureId: h1.id, adults: 2, children: 2, quantity: 1, unitCost: 0, unitPrice: 0 }] })
await call(consultant, 'POST', `/travel/bookings/${b1.id}/confirm`)
const accounts = await call(owner, 'GET', '/finance/accounts')
await call(consultant, 'POST', `/travel/bookings/${b1.id}/deposits`, { amount: 80000, bankAccountId: accounts.find(a => a.code === '1120').id, reference: 'JazzCash 7781' })

const b2 = await call(consultant, 'POST', '/travel/bookings', { entityId: TRV, customerId: corp.id,
  passengers: Array.from({ length: 8 }, (_, i) => ({ fullName: `Engro staff ${i + 1}`, type: 'Adult' })),
  items: [{ type: 'TourSeats', tourDepartureId: s1.id, adults: 8, children: 0, quantity: 1, unitCost: 0, unitPrice: 0 },
    { type: 'Flight', airline: 'PIA', route: 'ISB-KDU-ISB', serviceDate: day(19), supplierId: piaV.id, quantity: 8, unitCost: 38000, unitPrice: 42000, pnr: 'QX4T9Z' }] })
await call(consultant, 'POST', `/travel/bookings/${b2.id}/confirm`)
await call(consultant, 'POST', `/travel/bookings/${b2.id}/invoice`)
await call(consultant, 'POST', `/travel/bookings/${b2.id}/supplier-bills`)

await call(consultant, 'POST', '/travel/bookings', { entityId: TRV, customerId: umrahC.id, travelDate: day(40), notes: 'Umrah — 14 nights, Makkah 9 / Madinah 5',
  passengers: [{ fullName: 'Hajira Bibi', type: 'Adult', passportNo: 'BN1122334', passportExpiry: day(150), nationality: 'Pakistani' }, { fullName: 'Muhammad Aslam', type: 'Adult', passportNo: 'BN5566778', passportExpiry: day(1200), nationality: 'Pakistani' }],
  items: [{ type: 'Visa', country: 'Saudi Arabia', supplierId: vfs.id, quantity: 1, unitCost: 28000, unitPrice: 33000, passengerIndex: 0, visaStatus: 'Submitted' },
    { type: 'Visa', country: 'Saudi Arabia', supplierId: vfs.id, quantity: 1, unitCost: 28000, unitPrice: 33000, passengerIndex: 1, visaStatus: 'DocumentsCollected' },
    { type: 'Hotel', description: 'Makkah — Swissotel Al Maqam, 9 nights, 1 double', supplierId: vfs.id, quantity: 9, unitCost: 32000, unitPrice: 36000 }] })
await dep(hunza, day(55), 18).catch(() => null)
console.log('Travel demo data added.', h2.code)
