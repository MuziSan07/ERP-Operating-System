// Seeds a demo multi-industry organization for local development. Safe to re-run (skips if it exists).
// Usage: node tests/demo-seed.mjs
// Demo logins (development only — all share DEMO_PASSWORD):
//   owner@demo.erpos.local   Super Admin of the whole group
//   hotels@demo.erpos.local  Admin of "Hotels Division" and everything under it
//   frontdesk@demo.erpos.local  Employee at "Skardu Resort" with one extra granted permission
import { readFileSync } from 'node:fs'

export const DEMO_PASSWORD = 'DemoPass2026'
const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))

async function call(token, method, path, body) {
  const res = await fetch(base + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${text}`)
  return text ? JSON.parse(text) : null
}
const login = async (email, password) => (await call(null, 'POST', '/auth/login', { email, password })).accessToken
const data = (name, code, industry, city) => ({ name, code, industry, isActive: true, currency: 'PKR', timeZone: 'Asia/Karachi', city, country: 'Pakistan' })

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
const tenants = await call(platform, 'GET', '/platform/tenants')
if (tenants.some(t => t.code === 'GBDEMO')) {
  console.log('Demo organization already exists.')
  process.exit(0)
}

await call(platform, 'POST', '/platform/tenants', {
  name: 'GB Techive Group', code: 'GBDEMO', industry: 'General', maxUsers: 100, currency: 'PKR', timeZone: 'Asia/Karachi',
  country: 'Pakistan', contactEmail: 'owner@demo.erpos.local',
  modules: ['hr', 'payroll', 'finance', 'inventory', 'procurement', 'hotel', 'travel', 'tourism', 'logistics', 'ngo', 'projects'],
  superAdminName: 'Group Owner', superAdminEmail: 'owner@demo.erpos.local', superAdminPassword: DEMO_PASSWORD,
})

const owner = await login('owner@demo.erpos.local', DEMO_PASSWORD)
const root = (await call(owner, 'GET', '/auth/me')).entities[0]
const mk = (parent, d, modules) => call(owner, 'POST', '/entities', { parentId: parent.id, data: d, modules })

const hotels = await mk(root, data('Hotels Division', 'HOTELS', 'Hotel', 'Gilgit'), ['hr', 'payroll', 'finance', 'inventory', 'procurement', 'hotel'])
const skardu = await mk(hotels, data('Skardu Resort', 'HTL-SKD', 'Hotel', 'Skardu'))
await mk(skardu, data('Housekeeping', 'HTL-SKD-HK', 'Hotel', 'Skardu'), ['hr', 'hotel'])
await mk(hotels, data('Hunza Inn', 'HTL-HNZ', 'Hotel', 'Karimabad'))
const travel = await mk(root, data('Tours & Travel', 'TRAVEL', 'Tourism', 'Islamabad'), ['hr', 'finance', 'travel', 'tourism'])
await mk(travel, data('Islamabad Office', 'TRV-ISB', 'Travel', 'Islamabad'))
await mk(root, data('Northern Logistics', 'LOGI', 'Logistics', 'Rawalpindi'), ['hr', 'finance', 'inventory', 'logistics'])
await mk(root, data('Mountain Care Foundation', 'NGO', 'Ngo', 'Gilgit'), ['hr', 'finance', 'ngo', 'projects'])
await mk(root, data('Techive Software', 'SOFT', 'SoftwareServices', 'Lahore'), ['hr', 'payroll', 'finance', 'projects'])

const roles = await call(owner, 'GET', '/roles')
const role = n => roles.find(r => r.name === n).id

await call(owner, 'POST', '/users', {
  email: 'hotels@demo.erpos.local', fullName: 'Hotels Admin', password: DEMO_PASSWORD, userType: 'Admin',
  primaryEntityId: hotels.id, roleId: role('Administrator'),
})
await call(owner, 'POST', '/users', {
  email: 'skardu.manager@demo.erpos.local', fullName: 'Skardu Manager', password: DEMO_PASSWORD, userType: 'Manager',
  primaryEntityId: skardu.id, roleId: role('Manager'),
})
const desk = await call(owner, 'POST', '/users', {
  email: 'frontdesk@demo.erpos.local', fullName: 'Front Desk Agent', password: DEMO_PASSWORD, userType: 'Employee',
  primaryEntityId: skardu.id, roleId: role('Employee'),
})
await call(owner, 'POST', `/users/${desk.id}/overrides`, { permissionCode: 'hotel.reservations.create', entityId: skardu.id, isGranted: true })

console.log('Demo organization "GB Techive Group" created. Logins are listed at the top of tests/demo-seed.mjs.')
