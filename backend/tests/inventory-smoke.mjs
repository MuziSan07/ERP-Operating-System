// End-to-end test of Inventory & Procurement: PR → PO → GRN → three-way-matched bill, weighted average cost,
// batches with FEFO and expiry, transfers between branches, adjustments, and stock value = ledger value.
// Usage: node tests/inventory-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
const mail = n => `${n}.${run}@inv.test`.toLowerCase()
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
await ok(call(platform, 'POST', '/platform/tenants', { name: `Inv Test ${run}`, code: `INV${run}`, industry: 'Hotel', maxUsers: 20, currency: 'PKR',
  timeZone: 'Asia/Karachi', modules: ['hr', 'finance', 'inventory', 'procurement'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const root = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0]
const hotel = await ok(call(owner, 'POST', '/entities', { parentId: root.id, data: { name: 'Skardu Hotel', code: `SKD${run}`, industry: 'Hotel', isActive: true, currency: 'PKR', timeZone: 'Asia/Karachi' } }), 'hotel')

const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const role = n => roles.find(r => r.name === n).id
check(roles.some(r => r.name === 'Storekeeper') && roles.some(r => r.name === 'Procurement Officer'), 'Storekeeper and Procurement Officer default roles exist')
const user = async (n, roleName) => ok(call(owner, 'POST', '/users', { email: mail(n), fullName: n, password: pw, userType: 'Employee', primaryEntityId: root.id, roleId: role(roleName) }), n)
await user('buyer', 'Procurement Officer'); await user('store', 'Storekeeper')
const buyer = await login(mail('buyer')), store = await login(mail('store'))

const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = code => accounts.find(a => a.code === code)?.id
check(!!acc('2125') && !!acc('5110'), 'GRNI and price variance accounts seeded')

const main = await ok(call(owner, 'POST', '/inventory/warehouses', { entityId: root.id, code: 'MAIN', name: 'Main store', isActive: true }), 'main wh')
const hotelWh = await ok(call(owner, 'POST', '/inventory/warehouses', { entityId: hotel.id, code: 'HTL', name: 'Hotel store', isActive: true }), 'hotel wh')
const item = body => ok(call(buyer, 'POST', '/inventory/items', { type: 'Stock', unit: 'pcs', trackBatches: false, trackExpiry: false, reorderLevel: 0, isActive: true, ...body }), body.code)
const rice = await item({ code: 'RICE5', name: 'Rice 5kg bag', unit: 'bag', reorderLevel: 110 })
const para = await item({ code: 'PARA', name: 'Paracetamol 500mg box', unit: 'box', trackBatches: true, trackExpiry: true })
const ac = await item({ code: 'ACSVC', name: 'AC servicing', type: 'Service', unit: 'job' })
const vendor = await ok(call(buyer, 'POST', '/finance/contacts', { code: 'V-SUP', name: 'Northern Supplies', isVendor: true, isCustomer: false, paymentTermsDays: 15, isActive: true }), 'vendor')
const taxes = await ok(call(owner, 'GET', '/finance/tax-rates'), 'taxes')
const gst = taxes.find(t => t.code === 'GST18').id

// ---- purchase request ----
const pr = await ok(call(store, 'POST', '/procurement/requests', { entityId: root.id, date: '2026-10-01', purpose: 'Monthly kitchen and first-aid restock',
  lines: [{ itemId: rice.id, quantity: 100, estimatedUnitPrice: 1450 }] }), 'pr')
await ok(call(store, 'POST', `/procurement/requests/${pr.id}/submit`), 'submit')
const selfPr = await call(store, 'POST', `/procurement/requests/${pr.id}/decision`, { approve: true })
check(selfPr.status === 403, 'requester cannot approve their own purchase request')
const prOk = await ok(call(buyer, 'POST', `/procurement/requests/${pr.id}/decision`, { approve: true }), 'approve pr')
check(prOk.status === 'Approved', 'procurement officer approves the request')

// ---- purchase order ----
const po = await ok(call(buyer, 'POST', '/procurement/orders', { entityId: root.id, vendorId: vendor.id, warehouseId: main.id, date: '2026-10-01', lines: [
  { itemId: rice.id, quantity: 100, unitPrice: 1500, taxRateId: gst, purchaseRequestLineId: pr.lines[0].id },
  { itemId: para.id, quantity: 50, unitPrice: 400 },
  { itemId: ac.id, quantity: 1, unitPrice: 20000 }] }), 'po')
check(po.subtotal === 190000 && po.taxTotal === 27000 && po.total === 217000, 'PO totals 190,000 + GST 27,000', [po.subtotal, po.taxTotal])
const selfPo = await call(buyer, 'POST', `/procurement/orders/${po.id}/approve`)
check(selfPo.status === 403, 'the person who raised the PO cannot approve it')
const storePo = await call(store, 'POST', `/procurement/orders/${po.id}/approve`)
check(storePo.status === 403, 'storekeeper cannot approve purchase orders')
await ok(call(owner, 'POST', `/procurement/orders/${po.id}/approve`), 'approve po')
check((await ok(call(buyer, 'GET', `/procurement/requests/${pr.id}`), 'pr')).status === 'Ordered', 'purchase request marked Ordered')
const L = code => po.lines.find(l => l.itemCode === code).id

// ---- goods receipts ----
const noBatch = await call(store, 'POST', '/procurement/receipts', { purchaseOrderId: po.id, date: '2026-10-01', lines: [{ purchaseOrderLineId: L('PARA'), quantity: 30 }] })
check(noBatch.status === 400, 'batch-tracked item cannot be received without batch and expiry')
const grn1 = await ok(call(store, 'POST', '/procurement/receipts', { purchaseOrderId: po.id, date: '2026-10-01', deliveryNote: 'DN-1', lines: [
  { purchaseOrderLineId: L('RICE5'), quantity: 60 }, { purchaseOrderLineId: L('PARA'), quantity: 30, batchNo: 'B1', expiryDate: '2026-12-31' }] }), 'grn1')
check(grn1.totalValue === 102000, 'GRN 1 value 102,000 at PO prices', grn1.totalValue)
const over = await call(store, 'POST', '/procurement/receipts', { purchaseOrderId: po.id, date: '2026-10-02', lines: [{ purchaseOrderLineId: L('RICE5'), quantity: 41 }] })
check(over.status === 400, 'cannot receive more than ordered')
await ok(call(store, 'POST', '/procurement/receipts', { purchaseOrderId: po.id, date: '2026-10-02', lines: [
  { purchaseOrderLineId: L('RICE5'), quantity: 40 }, { purchaseOrderLineId: L('PARA'), quantity: 20, batchNo: 'B2', expiryDate: '2027-06-30' },
  { purchaseOrderLineId: L('ACSVC'), quantity: 1 }] }), 'grn2')
check((await ok(call(buyer, 'GET', `/procurement/orders/${po.id}`), 'po')).status === 'Received', 'PO fully received')
const grni = await ok(call(owner, 'GET', '/procurement/grni'), 'grni')
check(Math.round(grni.reduce((s, r) => s + r.unbilledValue, 0)) === 190000, 'goods received not invoiced = 190,000', grni.reduce((s, r) => s + r.unbilledValue, 0))

// ---- three-way matched bill ----
const { billId } = await ok(call(owner, 'POST', `/procurement/orders/${po.id}/bill`), 'bill from po')
const draft = await ok(call(owner, 'GET', `/finance/bills/${billId}`), 'draft bill')
check(draft.total === 217000 && draft.lines.every(l => !!l.purchaseOrderLineId), 'draft bill for everything received, linked to PO lines')
const edit = price => ({ entityId: draft.entityId, contactId: draft.contactId, date: '2026-10-02', reference: 'NS-4411', lines: draft.lines.map(l => ({
  description: l.description, accountId: l.accountId, quantity: l.quantity, unitPrice: l.description.startsWith('Rice') ? price : l.unitPrice, taxRateId: l.taxRateId,
  purchaseOrderLineId: l.purchaseOrderLineId })) })
await ok(call(owner, 'PUT', `/finance/bills/${billId}`, edit(1600)), 'edit 1600')
const tooHigh = await call(owner, 'POST', `/finance/bills/${billId}/approve`)
check(tooHigh.status === 400 && /Three-way match/.test(tooHigh.data?.title), 'bill price 6.7% above PO is rejected by the three-way match', tooHigh.data?.title)
await ok(call(owner, 'PUT', `/finance/bills/${billId}`, edit(1520)), 'edit 1520')
const bill = await ok(call(owner, 'POST', `/finance/bills/${billId}/approve`), 'approve bill')
check(bill.total === 219360, 'bill within 2% tolerance approved: 192,000 + GST 27,360', bill.total)
const bj = await ok(call(owner, 'GET', `/finance/journals/${bill.journalEntryId}`), 'bill journal')
const line = code => bj.lines.filter(l => l.accountCode === code).reduce((s, l) => s + l.baseDebit - l.baseCredit, 0)
check(line('2125') === 190000 && line('5110') === 2000 && line('1220') === 27360 && line('2110') === -219360,
  'bill clears GRNI 190,000, price variance 2,000, input GST 27,360, payable 219,360', bj.lines.map(l => [l.accountCode, l.baseDebit, l.baseCredit]))
const again = await call(owner, 'POST', `/procurement/orders/${po.id}/bill`)
check(again.status === 400, 'nothing left to bill on the PO')
check((await ok(call(buyer, 'GET', `/procurement/orders/${po.id}`), 'po')).status === 'Closed', 'PO closed once fully received and billed')

// ---- stock movements & weighted average ----
const issue = (date, lines, extra = {}) => call(store, 'POST', '/inventory/transactions', { type: 'Issue', warehouseId: main.id, date, lines, ...extra })
const iss1 = await ok(issue('2026-10-02', [{ itemId: para.id, quantity: 35 }]), 'issue para')
check(iss1.totalValue === 14000 && iss1.lines[0].unitCost === 400, 'issue 35 boxes at average 400 = 14,000')
const b = await ok(call(store, 'GET', `/inventory/batches?itemId=${para.id}`), 'batches')
check(b.length === 1 && b[0].batchNo === 'B2' && b[0].quantity === 15, 'FEFO: earliest-expiring batch B1 used up first, 15 left in B2', b.map(x => [x.batchNo, x.quantity]))

await ok(call(store, 'POST', '/inventory/transactions', { type: 'Adjustment', warehouseId: main.id, date: '2026-10-02', lines: [{ itemId: rice.id, quantity: 50, unitCost: 1800, notes: 'Count found extra stock' }] }), 'adj')
const iss2 = await ok(issue('2026-10-02', [{ itemId: rice.id, quantity: 30 }]), 'issue rice')
check(iss2.totalValue === 48000, 'weighted average after +50 @1,800 is 1,600: issue 30 = 48,000', iss2.totalValue)
const trf = await ok(call(store, 'POST', '/inventory/transactions', { type: 'Transfer', warehouseId: main.id, toWarehouseId: hotelWh.id, date: '2026-10-02', lines: [{ itemId: rice.id, quantity: 20 }] }), 'transfer')
const tj = await ok(call(owner, 'GET', `/finance/journals/${trf.journalEntryId}`), 'transfer journal')
check(trf.totalValue === 32000 && tj.lines.some(l => l.entityId === hotel.id && l.debit === 32000), 'transfer to the hotel moves 32,000 of inventory to the hotel branch')
const neg = await issue('2026-10-02', [{ itemId: rice.id, quantity: 500 }])
check(neg.status === 400, 'cannot issue more than in stock')

// Expired batch: opening stock B0 expiring end of October, then an issue in November skips it.
await ok(call(store, 'POST', '/inventory/transactions', { type: 'Opening', warehouseId: main.id, date: '2026-10-02', lines: [{ itemId: para.id, quantity: 10, unitCost: 380, batchNo: 'B0', expiryDate: '2026-10-31' }] }), 'opening')
const tooMuch = await issue('2026-11-05', [{ itemId: para.id, quantity: 16 }])
check(tooMuch.status === 400 && /unexpired/.test(tooMuch.data?.title), 'expired batch B0 cannot be issued', tooMuch.data?.title)
const iss3 = await ok(issue('2026-11-05', [{ itemId: para.id, quantity: 5 }]), 'issue unexpired')
check(iss3.lines[0].batchNo === 'B2' && iss3.totalValue === 1960, 'issue takes unexpired B2 at average 392 = 1,960', [iss3.lines[0].batchNo, iss3.totalValue])
const b0 = (await ok(call(store, 'GET', `/inventory/batches?itemId=${para.id}`), 'batches')).find(x => x.batchNo === 'B0')
await ok(call(store, 'POST', '/inventory/transactions', { type: 'Adjustment', warehouseId: main.id, date: '2026-11-05', lines: [{ itemId: para.id, quantity: -10, batchId: b0.batchId, notes: 'Expired — destroyed' }] }), 'write-off')

// ---- reconciliation ----
// Main rice 100 (160,000) + hotel rice 20 (32,000) + main para 10 (3,920) = 195,920 — must equal the inventory account.
const val = await ok(call(owner, 'GET', '/inventory/valuation'), 'valuation')
check(val.stockValue === 195920 && val.difference === 0, 'stock ledger value 195,920 equals the general ledger inventory balance', [val.stockValue, val.ledgerValue])
const low = await ok(call(store, 'GET', '/inventory/stock?belowReorder=true'), 'reorder')
check(low.some(r => r.itemCode === 'RICE5' && r.warehouseName === 'Main store'), 'rice at 100 bags is below its reorder level of 110')
const tb = await ok(call(owner, 'GET', '/finance/reports/trial-balance?asOf=2026-11-30'), 'tb')
check(tb.totalDebit === tb.totalCredit, 'trial balance still balances')

// ---- void the bill: quantities return to the PO ----
await ok(call(owner, 'POST', `/finance/bills/${billId}/void`, {}), 'void bill')
const poAfter = await ok(call(buyer, 'GET', `/procurement/orders/${po.id}`), 'po after void')
check(poAfter.status === 'Received' && poAfter.lines.every(l => l.quantityBilled === 0), 'voiding the bill reopens the PO for billing')

console.log(failures === 0 ? '\nALL INVENTORY & PROCUREMENT CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
