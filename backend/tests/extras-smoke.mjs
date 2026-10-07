// Attachments (permission follows the record, type/size limits, download, delete), email outbox, forgot/reset password
// (no account discovery, single-use link, sessions ended) and the daily reminder digest.
// Reads delivered emails from the development pickup folder (src/Erpos.Api/App_Data/mail).
// Usage: node tests/extras-smoke.mjs [baseUrl]
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const base = process.argv[2] ?? 'http://localhost:5136/api'
const cfg = JSON.parse(readFileSync(new URL('../src/Erpos.Api/appsettings.Development.json', import.meta.url)))
const mailDir = fileURLToPath(new URL('../src/Erpos.Api/App_Data/mail', import.meta.url))
const run = Date.now().toString(36).toUpperCase()
const pw = `Test${run}1xYz`
const mail = n => `${n}.${run}@ext.test`.toLowerCase()
const day = n => new Date(Date.now() + 5 * 3600_000 + n * 86400_000).toISOString().slice(0, 10)
let failures = 0

async function call(token, method, path, body) {
  const isForm = body instanceof FormData
  const res = await fetch(base + path, { method, headers: { ...(isForm ? {} : { 'Content-Type': 'application/json' }), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? (isForm ? body : JSON.stringify(body)) : undefined })
  const type = res.headers.get('content-type') ?? ''
  if (type.includes('json') || res.status >= 400) { const text = await res.text(); return { status: res.status, data: text ? JSON.parse(text) : null } }
  return { status: res.status, bytes: new Uint8Array(await res.arrayBuffer()), headers: res.headers }
}
const ok = async (p, label) => { const r = await p; if (r.status >= 400) throw new Error(`${label}: ${r.status} ${JSON.stringify(r.data)}`); return r.data ?? r }
function check(cond, label, detail) {
  console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}${!cond && detail !== undefined ? `  → got ${JSON.stringify(detail)}` : ''}`)
  if (!cond) failures++
}
const loginFull = async (email, password = pw) => ok(call(null, 'POST', '/auth/login', { email, password }), `login ${email}`)
const login = async (email, password = pw) => (await loginFull(email, password)).accessToken
const sleep = ms => new Promise(r => setTimeout(r, ms))

/** Waits for a delivered .eml to `to` whose decoded text matches `pattern`; returns the decoded message. */
async function waitForMail(to, pattern, timeoutMs = 60000) {
  const decodeQp = s => s.replace(/=\r?\n/g, '').replace(/=([0-9A-F]{2})/gi, (_, h) => String.fromCharCode(parseInt(h, 16)))
  const started = Date.now()
  while (Date.now() - started < timeoutMs) {
    let files = []
    try { files = readdirSync(mailDir).map(f => join(mailDir, f)).sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs).slice(0, 200) } catch { /* not created yet */ }
    for (const f of files) {
      const raw = readFileSync(f, 'utf8')
      if (!raw.toLowerCase().includes(`to: ${to}`)) continue
      const [headers, ...rest] = raw.split(/\r?\n\r?\n/)
      let body = rest.join('\n\n')
      if (/Content-Transfer-Encoding: base64/i.test(headers)) body = Buffer.from(body.replace(/\s+/g, ''), 'base64').toString('utf8')
      else if (/quoted-printable/i.test(headers)) body = Buffer.from(decodeQp(body), 'latin1').toString('utf8')
      if (pattern.test(body)) return { headers, body }
    }
    await sleep(1500)
  }
  return null
}

const platform = await login(cfg.Seed.PlatformAdminEmail, cfg.Seed.PlatformAdminPassword)
await ok(call(platform, 'POST', '/platform/tenants', { name: `Extras ${run}`, code: `EXT${run}`, industry: 'General', maxUsers: 10, currency: 'PKR', timeZone: 'Asia/Karachi',
  modules: ['finance', 'hr'], superAdminName: 'Owner', superAdminEmail: mail('owner'), superAdminPassword: pw }), 'tenant')
const owner = await login(mail('owner'))
const E = (await ok(call(owner, 'GET', '/auth/me'), 'me')).entities[0].id
const roles = await ok(call(owner, 'GET', '/roles'), 'roles')
const clerk = await ok(call(owner, 'POST', '/users', { email: mail('clerk'), fullName: 'Clerk Ahmed', password: pw, userType: 'Employee', primaryEntityId: E, roleId: roles.find(r => r.name === 'Employee').id }), 'clerk')
const clerkTok = await login(mail('clerk'))
const accounts = await ok(call(owner, 'GET', '/finance/accounts'), 'accounts')
const acc = c => accounts.find(a => a.code === c).id
const cust = await ok(call(owner, 'POST', '/finance/contacts', { code: 'C-1', name: 'Late Payer Ltd', isCustomer: true, isVendor: false, isActive: true, paymentTermsDays: 30 }), 'customer')
const inv = await ok(call(owner, 'POST', '/finance/invoices', { entityId: E, contactId: cust.id, date: day(-45), lines: [{ description: 'Services', accountId: acc('4100'), quantity: 1, unitPrice: 25000 }] }), 'invoice')
await ok(call(owner, 'POST', `/finance/invoices/${inv.id}/approve`), 'approve')

// ---- attachments ----
const pdf = new TextEncoder().encode('%PDF-1.4\n% ERPOS test attachment\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n')
const upload = (token, name, bytes, recordType = 'invoice', recordId = inv.id) => {
  const fd = new FormData()
  fd.append('recordType', recordType); fd.append('recordId', recordId); fd.append('description', 'Signed delivery note')
  fd.append('file', new Blob([bytes]), name)
  return call(token, 'POST', '/attachments', fd)
}
const up = await ok(upload(owner, 'delivery-note.pdf', pdf), 'upload')
check(up.fileName === 'delivery-note.pdf' && up.contentType === 'application/pdf' && up.sizeBytes === pdf.length && up.uploadedBy === 'Owner', 'PDF attached to the invoice', up)
const list = await ok(call(owner, 'GET', `/attachments?recordType=invoice&recordId=${inv.id}`), 'list')
check(list.length === 1, 'the invoice lists its attachment')
const dl = await call(owner, 'GET', `/attachments/${up.id}/download`)
check(dl.status === 200 && Buffer.from(dl.bytes).equals(Buffer.from(pdf)) && /attachment/.test(dl.headers.get('content-disposition') ?? ''), 'download returns the exact file as an attachment')
const exe = await upload(owner, 'invoice.exe', new Uint8Array([77, 90, 0, 0]))
check(exe.status === 400, 'executables are refused')
const big = await upload(owner, 'scan.pdf', new Uint8Array(10 * 1024 * 1024 + 1))
check(big.status === 400 || big.status === 413, 'files over 10 MB are refused', big.status)
const clerkList = await call(clerkTok, 'GET', `/attachments?recordType=invoice&recordId=${inv.id}`)
const clerkDl = await call(clerkTok, 'GET', `/attachments/${up.id}/download`)
check(clerkList.status === 403 && clerkDl.status === 403, "someone who can't see the invoice can't see or download its files", [clerkList.status, clerkDl.status])
const bogus = await upload(owner, 'x.pdf', pdf, 'salary-sheet', inv.id)
check(bogus.status === 400, 'only known record types accept files')
await ok(call(owner, 'DELETE', `/attachments/${up.id}`), 'delete')
check((await ok(call(owner, 'GET', `/attachments?recordType=invoice&recordId=${inv.id}`), 'list2')).length === 0, 'deleted attachment disappears')

// ---- forgot / reset password ----
const unknown = await call(null, 'POST', '/auth/forgot-password', { email: `nobody.${run}@ext.test` })
const known = await call(null, 'POST', '/auth/forgot-password', { email: mail('clerk') })
check(unknown.status === 200 && known.status === 200 && unknown.data.message === known.data.message, 'same answer whether or not the email has an account')
const resetMail = await waitForMail(mail('clerk'), /reset-password\?token=/)
const token = resetMail ? decodeURIComponent(resetMail.body.match(/reset-password\?token=([^"&\s<]+)/)[1]) : null
check(!!token, 'reset link emailed to the user (delivered by the background dispatcher)')
const oldSession = await loginFull(mail('clerk'))
const weak = await call(null, 'POST', '/auth/reset-password', { token, newPassword: 'short1' })
check(weak.status === 400, 'the new password must meet the policy')
const newPw = `Reset${run}9zQ`
const reset = await call(null, 'POST', '/auth/reset-password', { token, newPassword: newPw })
const reuse = await call(null, 'POST', '/auth/reset-password', { token, newPassword: `Again${run}8x` })
const oldLogin = await call(null, 'POST', '/auth/login', { email: mail('clerk'), password: pw })
const newLogin = await call(null, 'POST', '/auth/login', { email: mail('clerk'), password: newPw })
const oldRefresh = await call(null, 'POST', '/auth/refresh', { refreshToken: oldSession.refreshToken })
check(reset.status === 200 && reuse.status === 400 && oldLogin.status === 401 && newLogin.status === 200 && oldRefresh.status === 401,
  'reset works once; old password and old sessions stop working', [reset.status, reuse.status, oldLogin.status, newLogin.status, oldRefresh.status])

// ---- outbox & reminders ----
const rem = await ok(call(owner, 'POST', '/admin/reminders/run'), 'reminders')
check(rem.items.some(i => /overdue customer invoice/.test(i.text) && /25,000/.test(i.text)) && rem.recipients === 1, 'reminder digest lists the 25,000 overdue invoice for the one Super Admin', rem)
const clerkRem = await call(clerkTok, 'POST', '/admin/reminders/run')
check(clerkRem.status === 403, 'only administrators can trigger reminders')
const digest = await waitForMail(mail('owner'), /overdue customer invoice/)
check(!!digest && /Late Payer|overdue/.test(digest.body), 'digest email delivered to the owner')
const outbox = await ok(call(owner, 'GET', '/admin/outbox'), 'outbox')
const resetItem = outbox.items.find(i => i.category === 'password-reset')
check(['pickup', 'smtp'].includes(outbox.mode) && !!resetItem && !('htmlBody' in resetItem) && outbox.items.some(i => i.category === 'reminders' && i.status === 'Sent'),
  'outbox shows delivery status without exposing email bodies (no reset links)', outbox.items.map(i => [i.category, i.status]))

console.log(failures === 0 ? '\nALL EXTRAS CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`)
process.exit(failures ? 1 : 0)
