// Adds hotel demo data to "GB Techive Group" (run the other demo seeds first).
// frontdesk2@demo.erpos.local (Front Desk) and housekeeping@demo.erpos.local (Housekeeping) use the demo password.
const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
async function call(token, method, path, body) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async email => (await call(null, 'POST', '/auth/login', { email, password: DEMO_PASSWORD })).accessToken
const day = offset => new Date(Date.now() + 5 * 3600_000 + offset * 86400_000).toISOString().slice(0, 10)
const owner = await login('owner@demo.erpos.local')
const entities = await call(owner, 'GET', '/entities')
const E = code => entities.find(e => e.code === code).id
const SKD = E('HTL-SKD')
if ((await call(owner, 'GET', `/hotel/room-types?entityId=${SKD}`)).length > 0) { console.log('Hotel demo data already present.'); process.exit(0) }

const roles = await call(owner, 'GET', '/roles')
const role = n => roles.find(r => r.name === n).id
const emp = (code, name, email, roleName) => call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: SKD, employmentType: 'Permanent',
  status: 'Active', joinDate: '2026-03-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false }, email, password: DEMO_PASSWORD, userType: 'Employee', roleId: role(roleName), monthlyGross: 60000 })
await emp('GB-120', 'Rehana Reception', 'frontdesk2@demo.erpos.local', 'Front Desk')
await emp('GB-121', 'Karam Housekeeping', 'housekeeping@demo.erpos.local', 'Housekeeping')
const desk = await login('frontdesk2@demo.erpos.local')

const taxes = await call(owner, 'GET', '/finance/tax-rates')
const pst = taxes.find(t => t.code === 'PST-PB').id
const type = (code, name, rate, adults) => call(owner, 'POST', '/hotel/room-types', { entityId: SKD, code, name, baseRate: rate, maxAdults: adults, maxChildren: 1, taxRateId: pst, isActive: true })
const std = await type('STD', 'Standard (valley view)', 14000, 2)
const dlx = await type('DLX', 'Deluxe (K2 view)', 22000, 2)
const ste = await type('STE', 'Family Suite', 38000, 4)
const rooms = {}
for (const [n, t] of [['101', std], ['102', std], ['103', std], ['104', std], ['201', dlx], ['202', dlx], ['203', dlx], ['204', dlx], ['205', dlx], ['301', ste], ['302', ste], ['303', ste]])
  rooms[n] = await call(owner, 'POST', '/hotel/rooms', { entityId: SKD, number: n, roomTypeId: t.id, floor: n[0], isActive: true })

const accounts = await call(owner, 'GET', '/finance/accounts')
const bank = accounts.find(a => a.code === '1120').id
const book = (guest, type, from, to, extra = {}) => call(desk, 'POST', '/hotel/reservations', { entityId: SKD, newGuest: guest, source: 'Phone', arrivalDate: from, departureDate: to, adults: 2,
  children: 0, tentative: false, rooms: [{ roomTypeId: type.id }], ...extra })
const assign = (res, room) => call(desk, 'POST', `/hotel/reservations/${res.id}/assign-room`, { reservationRoomId: res.rooms[0].id, roomId: rooms[room].id })

// In house: arrived today, staying 3 nights.
const a = await book({ fullName: 'Dr. Ayesha Malik', phone: '0321-5551234', cnic: '61101-2345678-2', isVip: true }, dlx, day(0), day(3), { source: 'Corporate' })
await call(desk, 'POST', `/hotel/reservations/${a.id}/deposits`, { amount: 30000, bankAccountId: bank, reference: 'Card 7781' })
await assign(a, '201'); await call(desk, 'POST', `/hotel/reservations/${a.id}/check-in`)
await call(desk, 'POST', `/hotel/reservations/${a.id}/charges`, { type: 'Restaurant', description: 'Dinner — trout & chapli kebab, 2 covers', quantity: 1, unitPrice: 6800, taxRateId: pst })
const b = await book({ fullName: 'Mr. & Mrs. Hamza Qureshi', phone: '0333-7778899', city: 'Lahore' }, ste, day(0), day(2))
await assign(b, '301'); await call(desk, 'POST', `/hotel/reservations/${b.id}/check-in`)
await call(desk, 'POST', `/hotel/reservations/${b.id}/charges`, { type: 'Laundry', description: 'Laundry — 8 pieces', quantity: 1, unitPrice: 2400 })

// Stayed and left: checked in today, checking out today (1 night billed).
const c = await book({ fullName: 'Ms. Sophie Laurent', passportNo: 'FR12AB345', nationality: 'French' }, std, day(0), day(1), { source: 'Ota', externalReference: 'BKG-88213' })
await assign(c, '101'); await call(desk, 'POST', `/hotel/reservations/${c.id}/check-in`)
await call(desk, 'POST', `/hotel/reservations/${c.id}/check-out`, { date: day(1), bankAccountId: bank, amountPaid: 16240, paymentReference: 'Cash' })

// Arrivals and future bookings.
await book({ fullName: 'Imran Siddiqui', phone: '0300-4445566' }, dlx, day(0), day(2))
const contacts = await call(owner, 'GET', '/finance/contacts?customers=true')
const pia = contacts.find(x => x.code === 'C-PIA')
await call(desk, 'POST', '/hotel/reservations', { entityId: SKD, newGuest: { fullName: 'PIA crew (Capt. Bilal)' }, billToContactId: pia?.id, source: 'Corporate', arrivalDate: day(1), departureDate: day(3),
  adults: 4, children: 0, tentative: false, rooms: [{ roomTypeId: std.id, rate: 11000 }, { roomTypeId: std.id, rate: 11000 }] })
await book({ fullName: 'Northern Trek group', phone: '0345-1212121' }, ste, day(4), day(8), { tentative: true, source: 'TravelAgent' })
await call(owner, 'PUT', `/hotel/rooms/${rooms['104'].id}/housekeeping`, { status: 'OutOfOrder', note: 'Bathroom tiles under repair' })
await call(owner, 'PUT', `/hotel/rooms/${rooms['202'].id}/housekeeping`, { status: 'Dirty' })
await call(desk, 'POST', '/hotel/night-audit', { entityId: SKD, date: day(0) })
console.log('Hotel demo data added.')
