// End-to-end test of the Hotel module: availability, overbooking guard, room assignment, housekeeping gate,
// deposits, folio charges with stock, night audit, checkout → sales tax invoice, and occupancy/ADR/RevPAR.
// Usage: node tests/hotel-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@htl.test`.toLowerCase()
let failures = 0

// Front-desk "today" is Pakistan time (UTC+5), same as the API.
const day = offset => new Date(Date.now() + 5 * 3600_000 + offset * 86400_000).toISOString().slice(0, 10)
const TODAY = day(0), D1 = day(1), D2 = day(2), D3 = day(3)

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
await ok(call(platform, 'POST', '/platform/tenants', { name: `Hotel Test ${run}`, code: `HTL${run}`, industry: 'Hotel', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['hotel', 'finance', 'inventory'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const H = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const role = n => roles.find(r => r.name === n).id
check(roles.some(r => r.name === 'Front Desk') && roles.some(r => r.name === 'Housekeeping'), 'Front Desk and Housekeeping default roles exist')
for (const [n, r] of [['desk', 'Front Desk'], ['hk', 'Housekeeping']])
  await ok(call(owner, 'POST', '/users', { email: mail(n), fullName: n, password: pw, userType: 'Employee', primaryEntityId: H, roleId: role(r) }), n)
const desk = await login(mail('desk')), hk = await login(mail('hk'))

const taxes = await ok(call(owner, 'GET', '/finance/tax-rates'), 'taxes')
const pst = taxes.find(t => t.code === 'PST-PB').id
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c).id
const deluxe = await ok(call(owner, 'POST', '/hotel/room-types', { entityId: H, code: 'DLX', name: 'Deluxe', baseRate: 20000, maxAdults: 2, maxChildren: 1, taxRateId: pst, isActive: true }), 'deluxe')
const suite = await ok(call(owner, 'POST', '/hotel/room-types', { entityId: H, code: 'STE', name: 'Suite', baseRate: 35000, maxAdults: 3, maxChildren: 2, taxRateId: pst, isActive: true }), 'suite')
const room = async (n, t) => ok(call(owner, 'POST', '/hotel/rooms', { entityId: H, number: n, roomTypeId: t.id, floor: n[0], isActive: true }), n)
const r101 = await room('101', deluxe), r102 = await room('102', deluxe)
await room('201', suite)

// ---- availability & booking ----
const avail = await ok(call(desk, 'GET', `/hotel/availability?entityId=${H}&from=${TODAY}&to=${D3}`), 'avail')
check(avail.roomTypes.find(t => t.roomTypeName === 'Deluxe').minAvailable === 2, 'two Deluxe rooms available')
const resA = await ok(call(desk, 'POST', '/hotel/reservations', { entityId: H, newGuest: { fullName: 'Ali Khan', phone: '0300-1234567', cnic: '35202-1234567-1', isVip: true },
  source: 'Phone', arrivalDate: TODAY, departureDate: D2, adults: 3, children: 0, tentative: false, rooms: [{ roomTypeId: deluxe.id }, { roomTypeId: deluxe.id }] }), 'res A')
check(resA.nights === 2 && resA.rooms.every(r => r.rate === 20000) && resA.estimatedStayTotal === 80000, 'reservation: 2 Deluxe × 2 nights at the rack rate (80,000)')
const over = await call(desk, 'POST', '/hotel/reservations', { entityId: H, newGuest: { fullName: 'Late Caller' }, source: 'Phone', arrivalDate: D1, departureDate: D2,
  adults: 1, children: 0, tentative: false, rooms: [{ roomTypeId: deluxe.id }] })
check(over.status === 400, 'overbooking a sold-out room type is refused')
const company = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-TREK', name: 'Hunza Treks', isCustomer: true, isVendor: false, paymentTermsDays: 30, isActive: true }), 'company')
const resB = await ok(call(desk, 'POST', '/hotel/reservations', { entityId: H, newGuest: { fullName: 'Tour Leader' }, billToContactId: company.id, source: 'TravelAgent',
  arrivalDate: D1, departureDate: D3, adults: 2, children: 0, tentative: false, rooms: [{ roomTypeId: suite.id, rate: 30000 }] }), 'res B')
check(resB.billToName === 'Hunza Treks' && resB.rooms[0].rate === 30000, 'travel-agent booking billed to the company at a negotiated rate')
const early = await call(desk, 'POST', `/hotel/reservations/${resB.id}/check-in`)
check(early.status === 400, "can't check in before the arrival date")

// ---- deposit, assignment, housekeeping gate, check-in ----
await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/deposits`, { amount: 20000, bankAccountId: acc('1120'), reference: 'Card 4421' }), 'deposit')
const noRoom = await call(desk, 'POST', `/hotel/reservations/${resA.id}/check-in`)
check(noRoom.status === 400, 'check-in needs rooms assigned')
await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/assign-room`, { reservationRoomId: resA.rooms[0].id, roomId: r101.id }), 'assign 101')
await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/assign-room`, { reservationRoomId: resA.rooms[1].id, roomId: r102.id }), 'assign 102')
await ok(call(hk, 'PUT', `/hotel/rooms/${r102.id}/housekeeping`, { status: 'Dirty', note: 'Late checkout from last guest' }), 'dirty')
const dirty = await call(desk, 'POST', `/hotel/reservations/${resA.id}/check-in`)
check(dirty.status === 400 && /dirty/i.test(dirty.data?.title), "a dirty room blocks check-in", dirty.data?.title)
await ok(call(hk, 'PUT', `/hotel/rooms/${r102.id}/housekeeping`, { status: 'Inspected' }), 'clean')
const hkBook = await call(hk, 'POST', '/hotel/reservations', { entityId: H, newGuest: { fullName: 'X' }, source: 'WalkIn', arrivalDate: D2, departureDate: D3, adults: 1, children: 0, tentative: false, rooms: [{ roomTypeId: deluxe.id }] })
check(hkBook.status === 403, 'housekeeping staff cannot make reservations')
const inA = await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/check-in`), 'check in')
check(inA.status === 'CheckedIn', 'guest checked in')
const fd = await ok(call(desk, 'GET', `/hotel/front-desk?entityId=${H}`), 'front desk')
check(fd.occupied === 2 && fd.occupancyPercent === 66.7 && fd.inHouse.length === 1, 'front desk: 2 of 3 rooms occupied (66.7%)', [fd.occupied, fd.occupancyPercent])

// ---- folio charges: minibar from stock, laundry ----
const water = await ok(call(owner, 'POST', '/inventory/items', { code: 'WATER', name: 'Mineral water 500ml', type: 'Stock', unit: 'btl', trackBatches: false, trackExpiry: false, reorderLevel: 0, isActive: true }), 'water')
const minibar = await ok(call(owner, 'POST', '/inventory/warehouses', { entityId: H, code: 'MINIBAR', name: 'Minibar store', isActive: true }), 'wh')
await ok(call(owner, 'POST', '/inventory/transactions', { type: 'Opening', warehouseId: minibar.id, date: TODAY, lines: [{ itemId: water.id, quantity: 50, unitCost: 40 }] }), 'opening')
await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/charges`, { type: 'Minibar', description: 'Mineral water', quantity: 4, unitPrice: 150, taxRateId: pst, itemId: water.id, warehouseId: minibar.id }), 'minibar')
await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/charges`, { type: 'Laundry', description: 'Laundry — 6 pieces', quantity: 1, unitPrice: 1500 }), 'laundry')
const stock = await ok(call(owner, 'GET', `/inventory/stock?warehouseId=${minibar.id}`), 'stock')
check(stock[0]?.quantity === 46, 'minibar charge issued 4 bottles from stock', stock[0]?.quantity)

// ---- night audit ----
const audit = await ok(call(desk, 'POST', '/hotel/night-audit', { entityId: H, date: TODAY }), 'audit')
check(audit.reservationsCharged === 1 && audit.roomRevenue === 40000, 'night audit charges 2 rooms × 20,000', audit)
const again = await ok(call(desk, 'POST', '/hotel/night-audit', { entityId: H, date: TODAY }), 'audit again')
check(again.roomRevenue === 0, 'running night audit twice does not double-charge')

// ---- checkout ----
// Rooms 4 nights × 20,000 = 80,000 + 16% = 12,800; minibar 600 + 96; laundry 1,500 → 94,996. Deposit 20,000 → 74,996 due.
const unpaid = await call(desk, 'POST', `/hotel/reservations/${resA.id}/check-out`, { date: D2 })
check(unpaid.status === 400 && /balance/i.test(unpaid.data?.title), "walk-in guest can't leave with an unpaid balance", unpaid.data?.title)
const outA = await ok(call(desk, 'POST', `/hotel/reservations/${resA.id}/check-out`, { date: D2, bankAccountId: acc('1120'), amountPaid: 74996, paymentReference: 'Card 4421' }), 'check out')
check(outA.status === 'CheckedOut' && outA.chargesTotal === 82100 && outA.taxTotal === 12896, 'checkout charged the missing night: 82,100 + tax 12,896', [outA.chargesTotal, outA.taxTotal])
const inv = await ok(call(owner, 'GET', `/finance/invoices/${outA.invoiceId}`), 'invoice')
check(inv.total === 94996 && inv.status === 'Paid' && inv.lines.some(l => l.quantity === 2 && l.unitPrice === 20000), 'sales tax invoice 94,996 fully paid (deposit + card), room nights grouped', [inv.total, inv.status])
const rooms = await ok(call(desk, 'GET', `/hotel/rooms?entityId=${H}`), 'rooms')
check(rooms.filter(r => ['101', '102'].includes(r.number)).every(r => r.housekeeping === 'Dirty' && !r.occupied), 'checked-out rooms are vacant and dirty')

const tb = await ok(call(owner, 'GET', `/finance/reports/trial-balance?asOf=${D3}`), 'tb')
const row = c => tb.rows.find(r => r.code === c)
check(!row('2200') && !row('1200') && row('2190')?.credit === 12896 && tb.totalDebit === tb.totalCredit,
  'ledger: deposit and receivable cleared, services tax payable 12,896, books balance', tb.rows.map(r => [r.code, r.debit, r.credit]))

// ---- cancellation & reports ----
const cancelled = await ok(call(desk, 'POST', `/hotel/reservations/${resB.id}/cancel`), 'cancel B')
check(cancelled.status === 'Cancelled', 'reservation cancelled')
const avail2 = await ok(call(desk, 'GET', `/hotel/availability?entityId=${H}&from=${D1}&to=${D3}`), 'avail2')
check(avail2.roomTypes.find(t => t.roomTypeName === 'Suite').minAvailable === 1, 'cancelled room is available again')
const rep = await ok(call(owner, 'GET', `/hotel/report?entityId=${H}&from=${TODAY}&to=${D1}`), 'report')
check(rep.sold === 4 && rep.roomRevenue === 80000 && rep.adr === 20000 && rep.revPar === 13333.33 && rep.occupancy === 66.7,
  'report: occupancy 66.7%, ADR 20,000, RevPAR 13,333.33', [rep.sold, rep.adr, rep.revPar, rep.occupancy])
const tape = await ok(call(desk, 'GET', `/hotel/tape-chart?entityId=${H}&from=${TODAY}&days=7`), 'tape')
check(tape.rooms.length === 3, 'tape chart lists all rooms')
const deskJournals = await call(desk, 'GET', '/finance/reports/trial-balance')
check(deskJournals.status === 403, 'front desk has no access to financial reports')

console.log(failures === 0 ? '\nALL HOTEL CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
