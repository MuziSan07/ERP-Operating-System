// Adds inventory & procurement demo data to "GB Techive Group" (run the other demo seeds first).
// storekeeper@demo.erpos.local (Storekeeper) and buyer@demo.erpos.local (Procurement Officer) use the demo password.
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
if ((await call(owner, 'GET', '/inventory/warehouses')).length > 0) { console.log('Inventory demo data already present.'); process.exit(0) }

const entities = await call(owner, 'GET', '/entities')
const E = code => entities.find(e => e.code === code).id
const roles = await call(owner, 'GET', '/roles')
const role = n => roles.find(r => r.name === n).id
const taxes = await call(owner, 'GET', '/finance/tax-rates')
const gst = taxes.find(t => t.code === 'GST18').id
const emp = (code, name, email, roleName, gross) => call(owner, 'POST', '/hr/employees', { data: { employeeCode: code, fullName: name, entityId: E('HOTELS'),
  employmentType: 'Permanent', status: 'Active', joinDate: '2026-01-01', eobiMember: true, providentFundMember: false, socialSecurityMember: false },
  email, password: DEMO_PASSWORD, userType: 'Employee', roleId: role(roleName), monthlyGross: gross })
await emp('GB-110', 'Saleem Storekeeper', 'storekeeper@demo.erpos.local', 'Storekeeper', 65000)
await emp('GB-111', 'Bushra Buyer', 'buyer@demo.erpos.local', 'Procurement Officer', 90000)
const store = await login('storekeeper@demo.erpos.local'), buyer = await login('buyer@demo.erpos.local')

const wh = (entity, code, name) => call(owner, 'POST', '/inventory/warehouses', { entityId: E(entity), code, name, isActive: true })
const skdStore = await wh('HTL-SKD', 'SKD-MAIN', 'Skardu Resort — Main store')
const skdKitchen = await wh('HTL-SKD', 'SKD-KIT', 'Skardu Resort — Kitchen')
await wh('HTL-HNZ', 'HNZ-MAIN', 'Hunza Inn — Store')

const cat = async (code, name) => (await call(owner, 'POST', '/inventory/categories', { code, name })).id
const food = await cat('FOOD', 'Food & kitchen'), linen = await cat('LINEN', 'Linen & amenities'), med = await cat('MED', 'First aid & medicines')
const item = b => call(buyer, 'POST', '/inventory/items', { type: 'Stock', trackBatches: false, trackExpiry: false, reorderLevel: 0, isActive: true, ...b })
const rice = await item({ code: 'RICE-25', name: 'Basmati rice 25kg', unit: 'bag', categoryId: food, reorderLevel: 8, standardCost: 9500 })
const oil = await item({ code: 'OIL-5L', name: 'Cooking oil 5L', unit: 'tin', categoryId: food, reorderLevel: 10, standardCost: 2900, purchaseTaxRateId: gst })
const towel = await item({ code: 'TWL-BATH', name: 'Bath towel (white)', unit: 'pcs', categoryId: linen, reorderLevel: 40, standardCost: 1200, purchaseTaxRateId: gst })
const para = await item({ code: 'MED-PARA', name: 'Paracetamol 500mg (box of 20)', unit: 'box', categoryId: med, trackBatches: true, trackExpiry: true, reorderLevel: 5, standardCost: 160 })
await item({ code: 'SVC-GEN', name: 'Generator servicing', type: 'Service', unit: 'job', standardCost: 35000 })

const vendors = await call(owner, 'GET', '/finance/contacts?vendors=true')
const metro = vendors.find(v => v.code === 'V-METRO')

const pr = await call(store, 'POST', '/procurement/requests', { entityId: E('HTL-SKD'), date: '2026-09-20', requiredBy: '2026-09-30', purpose: 'October season restock — kitchen, linen, first aid',
  lines: [{ itemId: rice.id, quantity: 20, estimatedUnitPrice: 9500 }, { itemId: oil.id, quantity: 30, estimatedUnitPrice: 2900 },
    { itemId: towel.id, quantity: 100, estimatedUnitPrice: 1200 }, { itemId: para.id, quantity: 20, estimatedUnitPrice: 160 }] })
await call(store, 'POST', `/procurement/requests/${pr.id}/submit`)
await call(buyer, 'POST', `/procurement/requests/${pr.id}/decision`, { approve: true, comment: 'Within budget' })

const po = await call(buyer, 'POST', '/procurement/orders', { entityId: E('HTL-SKD'), vendorId: metro.id, warehouseId: skdStore.id, date: '2026-09-21', expectedDate: '2026-09-28',
  terms: 'Delivery to Skardu included. Payment 30 days.', lines: pr.lines.map(l => ({ itemId: l.itemId, quantity: l.quantity,
    unitPrice: { 'RICE-25': 9400, 'OIL-5L': 2850, 'TWL-BATH': 1150, 'MED-PARA': 155 }[l.itemCode], taxRateId: l.itemCode === 'OIL-5L' || l.itemCode === 'TWL-BATH' ? gst : null,
    purchaseRequestLineId: l.id })) })
await call(owner, 'POST', `/procurement/orders/${po.id}/approve`)
const line = code => po.lines.find(l => l.itemCode === code).id
await call(store, 'POST', '/procurement/receipts', { purchaseOrderId: po.id, date: '2026-09-27', deliveryNote: 'METRO-DN-5521', lines: [
  { purchaseOrderLineId: line('RICE-25'), quantity: 20 }, { purchaseOrderLineId: line('OIL-5L'), quantity: 30 },
  { purchaseOrderLineId: line('TWL-BATH'), quantity: 60 }, { purchaseOrderLineId: line('MED-PARA'), quantity: 20, batchNo: 'PX2611', expiryDate: '2026-11-30' }] })
const { billId } = await call(owner, 'POST', `/procurement/orders/${po.id}/bill`)
await call(owner, 'POST', `/finance/bills/${billId}/approve`)

await call(store, 'POST', '/inventory/transactions', { type: 'Transfer', warehouseId: skdStore.id, toWarehouseId: skdKitchen.id, date: '2026-09-28', reference: 'Kitchen requisition',
  lines: [{ itemId: rice.id, quantity: 6 }, { itemId: oil.id, quantity: 10 }] })
await call(store, 'POST', '/inventory/transactions', { type: 'Issue', warehouseId: skdKitchen.id, date: '2026-09-30', reference: 'September kitchen usage',
  lines: [{ itemId: rice.id, quantity: 4 }, { itemId: oil.id, quantity: 7 }] })
await call(store, 'POST', '/inventory/transactions', { type: 'Issue', warehouseId: skdStore.id, date: '2026-10-01', reference: 'First-aid kits', lines: [{ itemId: para.id, quantity: 6 }] })
console.log('Inventory demo data added.')
