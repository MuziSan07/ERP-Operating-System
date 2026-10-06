// Who may open which page — one table used by both the menu and the router, so they can't drift apart.
// Each rule is "any of": a full permission code, or a prefix ending in "." meaning any permission under it.
// Pages not listed (dashboard, My Workspace, leave, profile) are open to every signed-in user.
export const ROUTE_ACCESS: Record<string, string[]> = {
  '/entities': ['core.entities.view'],
  '/users': ['core.users.view'],
  '/roles': ['core.roles.view', 'core.roles.manage'],
  '/audit': ['core.audit.view'],
  '/hr/employees': ['hr.employees.view'],
  '/hr/attendance': ['hr.attendance.view'],
  '/hr/structure': ['hr.departments.view', 'hr.departments.create'],
  '/payroll': ['payroll.payroll_runs.view'],
  '/payroll/settings': ['payroll.settings.manage', 'hr.settings.manage'],
  '/finance': ['finance.reports.view'],
  '/finance/invoices': ['finance.invoices.'],
  '/finance/bills': ['finance.bills.'],
  '/finance/payments': ['finance.payments.view'],
  '/finance/contacts': ['finance.contacts.'],
  '/finance/journals': ['finance.journals.view'],
  '/finance/reports': ['finance.reports.view'],
  '/finance/settings': ['finance.settings.manage', 'finance.accounts.view'],
  '/inventory/stock': ['inventory.stock.view'],
  '/inventory/transactions': ['inventory.stock.issue', 'inventory.stock.transfer', 'inventory.stock.adjust', 'inventory.stock.view'],
  '/inventory/setup': ['inventory.items.', 'inventory.warehouses.'],
  '/procurement/requests': ['procurement.purchase_requests.'],
  '/procurement/orders': ['procurement.purchase_orders.', 'procurement.goods_receipts.'],
  '/hotel/front-desk': ['hotel.reservations.view'],
  '/hotel/reservations': ['hotel.reservations.view'],
  '/hotel/housekeeping': ['hotel.housekeeping.view'],
  '/hotel/setup': ['hotel.rooms.view'],
  '/hotel/reports': ['hotel.reports.view'],
  '/travel': ['travel.bookings.view', 'tourism.departures.view'],
  '/travel/bookings': ['travel.bookings.view'],
  '/tours/departures': ['tourism.departures.view', 'travel.bookings.create'],
  '/tours/packages': ['tourism.packages.', 'tourism.guides.'],
  '/logistics': ['logistics.shipments.view', 'logistics.fleet.view'],
  '/logistics/consignments': ['logistics.shipments.view'],
  '/logistics/trips': ['logistics.shipments.view'],
  '/logistics/fleet': ['logistics.fleet.', 'logistics.drivers.', 'logistics.routes.'],
  '/logistics/billing': ['logistics.shipments.edit', 'logistics.cod.remit'],
  '/ngo': ['ngo.'],
  '/ngo/grants': ['ngo.grants.view'],
  '/ngo/donations': ['ngo.donations.view', 'ngo.donors.view'],
  '/ngo/beneficiaries': ['ngo.beneficiaries.view'],
  '/ngo/funds': ['ngo.funds.view', 'ngo.programs.view'],
  '/ngo/reports': ['ngo.reports.view'],
  '/projects': ['projects.projects.view'],
  '/projects/list': ['projects.projects.view'],
  '/projects/clients': ['projects.clients.view'],
  '/projects/approvals': ['projects.timesheets.approve'],
  '/projects/utilization': ['projects.reports.view'],
  '/timesheet': ['projects.timesheets.create'],
}

/** True when the user holds any permission the page needs. `held` is every permission the user has somewhere. */
export function canOpen(path: string, held: Set<string>): boolean {
  const rule = ROUTE_ACCESS[path]
  if (!rule) return true
  return rule.some(r => (r.endsWith('.') ? [...held].some(p => p.startsWith(r)) : held.has(r)))
}
