// Security checks inside one organization: no self-escalation down the entity tree, no organization-wide role edits from
// a branch, no cross-branch payments, brute-force lockout, refresh-token theft response, sessions ended on password
// change and deactivation.
// Usage: node tests/security-smoke.mjs [baseUrl]
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1xYz`
const mail = n => `${n}.${run}@sec.test`.toLowerCase()
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
const loginFull = async (email, password = pw) => ok(call(null, 'POST', '/auth/login', { email, password }), `login ${email}`)
const login = async (email, password = pw) => (await loginFull(email, password)).accessToken

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', { name: `Security ${run}`, code: `SEC${run}`, industry: 'General', maxUsers: 30, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['finance'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const root = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0]
const data = (name, code) => ({ name, code, industry: 'General', isActive: true, currency: 'PKR', timeZone: 'Asia/Karachi' })
const region = await ok(call(owner, 'POST', '/entities', { parentId: root.id, data: data('North Region', `NR${run}`), modules: ['finance'] }), 'region')
const branch = await ok(call(owner, 'POST', '/entities', { parentId: region.id, data: data('Gilgit Branch', `GB${run}`), modules: ['finance'] }), 'branch')
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const role = n => roles.find(r => r.name === n).id
const addUser = async (n, type, roleName, entity) => ok(call(owner, 'POST', '/users', { email: mail(n), fullName: n, password: pw, userType: type, primaryEntityId: entity.id, roleId: roleName ? role(roleName) : undefined }), n)

// A regional admin whose Administrator role covers the region only, not the branch below it.
const ra = await addUser('ra', 'Admin', null, region)
await ok(call(owner, 'POST', `/users/${ra.id}/assignments`, { roleId: role('Administrator'), entityId: region.id, includeDescendants: false }), 'ra role')
const raTok = await login(mail('ra'))
const clerk = await addUser('clerk', 'Employee', null, region)

// 1. Self-escalation down the tree.
const selfWiden = await call(raTok, 'POST', `/users/${ra.id}/assignments`, { roleId: role('Manager'), entityId: region.id, includeDescendants: true })
check(selfWiden.status === 403, 'an admin cannot widen their own role to sub-branches', [selfWiden.status, selfWiden.data?.title])
const selfOverride = await call(raTok, 'POST', `/users/${ra.id}/overrides`, { permissionCode: 'finance.payments.create', entityId: region.id, isGranted: true, includeDescendants: true })
check(selfOverride.status === 403, 'nobody but the Super Admin edits their own overrides', [selfOverride.status, selfOverride.data?.title])
const widenOther = await call(raTok, 'POST', `/users/${clerk.id}/assignments`, { roleId: role('Accountant'), entityId: region.id, includeDescendants: true })
check(widenOther.status === 403, "an admin can't grant a role over sub-branches where they lack its permissions", [widenOther.status, widenOther.data?.title])
const narrowOther = await call(raTok, 'POST', `/users/${clerk.id}/assignments`, { roleId: role('Accountant'), entityId: region.id, includeDescendants: false })
check(narrowOther.status === 200, '…but can grant it at the region itself', narrowOther.status)

// 2. Organization-wide roles can only be edited at the top.
const emp = roles.find(r => r.name === 'Employee')
const roleEdit = await call(raTok, 'PUT', `/roles/${emp.id}`, { name: 'Employee', description: emp.description, permissions: [] })
check(roleEdit.status === 403, 'a regional admin cannot rewrite organization-wide roles', [roleEdit.status, roleEdit.data?.title])

// 3. A payment can only settle documents of its own entity.
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c).id
const cust = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-1', name: 'Customer', isCustomer: true, isVendor: false, isActive: true, paymentTermsDays: 30 }), 'customer')
const inv = await ok(call(owner, 'POST', '/finance/invoices', { entityId: branch.id, contactId: cust.id, date: day(0), lines: [{ description: 'Service', accountId: acc('4100'), quantity: 1, unitPrice: 5000 }] }), 'inv')
await ok(call(owner, 'POST', `/finance/invoices/${inv.id}/approve`), 'approve')
const crossPay = await call(raTok, 'POST', '/finance/payments', { kind: 'Receipt', entityId: region.id, contactId: cust.id, date: day(0), bankAccountId: acc('1120'), amount: 5000,
  allocations: [{ documentId: inv.id, amount: 5000 }] })
check(crossPay.status >= 400 && crossPay.status < 500, "a region user can't mark a branch's invoice as paid", [crossPay.status, crossPay.data?.title])

// 4. Brute force: five wrong passwords lock the account for a while, even against the right password.
const victim = await addUser('victim', 'Employee', 'Employee', region)
for (let i = 0; i < 5; i++) await call(null, 'POST', '/auth/login', { email: mail('victim'), password: 'wrong-password-1' })
const locked = await call(null, 'POST', '/auth/login', { email: mail('victim'), password: pw })
check(locked.status === 401 && /locked|too many/i.test(locked.data?.title ?? ''), 'five failed logins lock the account', [locked.status, locked.data?.title])

// 5. A stolen, already-used refresh token ends the whole session family.
const s1 = await loginFull(mail('clerk'))
const s2 = await ok(call(null, 'POST', '/auth/refresh', { refreshToken: s1.refreshToken }), 'refresh')
const reuse = await call(null, 'POST', '/auth/refresh', { refreshToken: s1.refreshToken })
const after = await call(null, 'POST', '/auth/refresh', { refreshToken: s2.refreshToken })
check(reuse.status === 401 && after.status === 401, 'reusing an old refresh token revokes the newer one too', [reuse.status, after.status])

// 6. Changing the password ends other sessions.
const s3 = await loginFull(mail('clerk'))
const newPw = `New${run}9xYzQ`
await ok(call(s3.accessToken, 'POST', '/auth/change-password', { currentPassword: pw, newPassword: newPw }), 'change password')
const stale = await call(null, 'POST', '/auth/refresh', { refreshToken: s3.refreshToken })
check(stale.status === 401, 'refresh tokens stop working after a password change', stale.status)

// 7. Deactivating a user cuts access immediately, not after the token expires.
const s4 = await loginFull(mail('clerk'), newPw)
const before = await call(s4.accessToken, 'GET', '/finance/accounts')
await ok(call(owner, 'PUT', `/users/${clerk.id}`, { fullName: 'clerk', userType: 'Employee', primaryEntityId: region.id, isActive: false }), 'deactivate')
const cut = await call(s4.accessToken, 'GET', '/finance/accounts')
const cutRefresh = await call(null, 'POST', '/auth/refresh', { refreshToken: s4.refreshToken })
check(before.status === 200 && cut.status === 403 && cutRefresh.status === 401, 'a deactivated user loses access on their next request', [before.status, cut.status, cutRefresh.status])

console.log(failures === 0 ? '\nALL SECURITY CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
