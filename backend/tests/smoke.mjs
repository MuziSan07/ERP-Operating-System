// End-to-end smoke test of the core permission model against a running API.
// Usage: node tests/smoke.mjs [baseUrl]   (needs the dev seed admin from appsettings.Development.json)
import { readFileSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1x`
let failures = 0

async function call(token, method, path, body) {
  const res = await fetch(base + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  return { status: res.status, data: text ? JSON.parse(text) : null }
}
const ok = async (p, label) => {
  const r = await p
  if (r.status >= 400) throw new Error(`${label}: ${r.status} ${JSON.stringify(r.data)}`)
  return r.data
}
function check(cond, label) {
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}`)
  if (!cond) failures++
}
const login = async (email, password) => (await ok(call(null, 'POST', '/auth/login', { email, password }), `login ${email}`)).accessToken
const entityData = (name, code, industry = 'General') =>
  ({ name, code, industry, isActive: true, currency: 'USD', timeZone: 'UTC' })

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)

const tenant = await ok(call(platform, 'POST', '/platform/tenants', {
  name: `Smoke Group ${run}`, code: `SMK${run}`, industry: 'Tourism', maxUsers: 20, currency: 'USD', timeZone: 'UTC',
  modules: ['hr', 'finance', 'hotel', 'travel', 'logistics'],
  superAdminName: 'Smoke Owner', superAdminEmail: `owner.${run}@smoke.test`.toLowerCase(), superAdminPassword: pw,
}), 'create tenant')
check(tenant.code === `SMK${run}`, 'platform admin creates organization')

const owner = await login(`owner.${run}@smoke.test`.toLowerCase(), pw)
const me = await ok(call(owner, 'GET', '/auth/me'), 'me')
const root = me.entities[0]
check(me.entities.length === 1 && root.permissions.includes('hotel.rooms.create'), 'super admin has all permissions at root')
check(!root.permissions.includes('ngo.donors.view'), 'disabled module (ngo) gives no permissions')

const hotel = await ok(call(owner, 'POST', '/entities', { parentId: root.id, data: entityData('Hotel Division', `HTL${run}`, 'Hotel'), modules: ['hr', 'hotel'] }), 'create hotel')
const branch = await ok(call(owner, 'POST', '/entities', { parentId: hotel.id, data: entityData('Hotel Branch A', `HTLA${run}`, 'Hotel') }), 'create branch')
const logistics = await ok(call(owner, 'POST', '/entities', { parentId: root.id, data: entityData('Logistics Co', `LOG${run}`, 'Logistics'), modules: ['logistics'] }), 'create logistics')
check(branch.depth === 2 && branch.modules.includes('hotel') && !branch.modules.includes('finance'), 'sub-sub-entity inherits parent modules only')

const bad = await call(owner, 'PUT', `/entities/${branch.id}/modules`, { modules: ['hotel', 'logistics'] })
check(bad.status === 400, 'cannot enable a module the parent does not have')

const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const adminRole = roles.find(r => r.name === 'Administrator')
const employeeRole = roles.find(r => r.name === 'Employee')

const hotelAdmin = await ok(call(owner, 'POST', '/users', {
  email: `hoteladmin.${run}@smoke.test`.toLowerCase(), fullName: 'Hotel Admin', password: pw, userType: 'Admin',
  primaryEntityId: hotel.id, roleId: adminRole.id,
}), 'create hotel admin')

const ha = await login(hotelAdmin.email, pw)
const haMe = await ok(call(ha, 'GET', '/auth/me'), 'hotel admin me')
const ids = haMe.entities.map(e => e.id)
check(ids.includes(hotel.id) && ids.includes(branch.id), 'parent assignment covers sub-entities')
check(!ids.includes(root.id) && !ids.includes(logistics.id), 'admin cannot see sibling or parent entities')

const forbidden = await call(ha, 'POST', '/entities', { parentId: logistics.id, data: entityData('Sneaky', `SNK${run}`) })
check(forbidden.status === 403, 'admin cannot create under an entity outside their branch')

const branchEmp = await ok(call(ha, 'POST', '/users', {
  email: `emp.${run}@smoke.test`.toLowerCase(), fullName: 'Branch Employee', password: pw, userType: 'Employee',
  primaryEntityId: branch.id, roleId: employeeRole.id,
}), 'admin creates employee in sub-entity')
check(!!branchEmp.id, 'admin creates users in their sub-entities')

const superTry = await call(ha, 'POST', '/users', {
  email: `x.${run}@smoke.test`.toLowerCase(), fullName: 'X', password: pw, userType: 'SuperAdmin', primaryEntityId: hotel.id,
})
check(superTry.status === 403, 'admin cannot create a super admin')

// Per-user override: grant one extra permission, then deny one the role gives.
await ok(call(ha, 'POST', `/users/${branchEmp.id}/overrides`, { permissionCode: 'hotel.reservations.create', entityId: branch.id, isGranted: true }), 'grant override')
const access = await ok(call(ha, 'POST', `/users/${branchEmp.id}/overrides`, { permissionCode: 'hr.leave.create', entityId: branch.id, isGranted: false }), 'deny override')
const eff = access.effective[branch.id] ?? []
check(eff.includes('hotel.reservations.create'), 'grant override adds a permission')
check(!eff.includes('hr.leave.create') && eff.includes('core.entities.view'), 'deny override removes a role permission')

// Privilege escalation: an employee-level user cannot be used to hand out more than they have.
const emp = await login(branchEmp.email, pw)
const esc = await call(emp, 'POST', `/users/${branchEmp.id}/assignments`, { roleId: adminRole.id, entityId: branch.id })
check(esc.status === 403, 'employee cannot assign themselves Administrator')

// Tenant isolation: the other organization's data is invisible.
const other = await ok(call(platform, 'POST', '/platform/tenants', {
  name: `Other NGO ${run}`, code: `OTH${run}`, industry: 'Ngo', maxUsers: 5, currency: 'USD', timeZone: 'UTC',
  superAdminName: 'Other Owner', superAdminEmail: `other.${run}@smoke.test`.toLowerCase(), superAdminPassword: pw,
}), 'create other tenant')
const otherTok = await login(`other.${run}@smoke.test`.toLowerCase(), pw)
const peek = await call(otherTok, 'GET', `/entities/${hotel.id}`)
check(peek.status === 403 || peek.status === 404, 'organizations cannot read each other\'s entities')
const otherUsers = await ok(call(otherTok, 'GET', '/users'), 'other users')
check(otherUsers.items.every(u => !u.email.includes('hoteladmin')), 'organizations cannot list each other\'s users')
check(!!other.id, 'second organization created')

// Move the branch under logistics: modules not allowed there get switched off.
const moved = await ok(call(owner, 'POST', `/entities/${branch.id}/move`, { newParentId: logistics.id }), 'move')
check(moved.parentId === logistics.id && !moved.modules.includes('hotel'), 'moving re-parents and trims modules')

const audit = await ok(call(owner, 'GET', '/audit?pageSize=5'), 'audit')
check(audit.total > 0, 'audit trail records changes')

console.log(failures === 0 ? '\nALL CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
