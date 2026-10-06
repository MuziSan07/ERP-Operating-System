# ERPOS — Multi-industry ERP

A multi-tenant SaaS ERP for NGOs, tourism, hotels, travel, logistics and software services.
Backend: ASP.NET Core (.NET 10) Web API + EF Core + MySQL. Frontend: React + TypeScript (Vite) + Ant Design.

## Run locally

```bash
cp .env.example .env                                   # set local MySQL passwords
cp backend/src/Erpos.Api/appsettings.Development.example.json backend/src/Erpos.Api/appsettings.Development.json
                                                       # fill in the connection string, a JWT key (32+ chars) and the platform admin password
docker compose up -d                                   # MySQL 8.4 on port 3307
dotnet run --project backend/src/Erpos.Api --launch-profile http   # API on :5136, applies migrations, seeds platform admin
npm --prefix frontend install
npm --prefix frontend run dev                          # UI on :5173 (proxies /api)
```

- Swagger: http://localhost:5136/swagger
- Platform admin login: `Seed` section of `backend/src/Erpos.Api/appsettings.Development.json`
- Demo organization: run `demo-seed.mjs`, `demo-hr-seed.mjs`, `demo-finance-seed.mjs`, `demo-inventory-seed.mjs`,
  `demo-hotel-seed.mjs`, `demo-travel-seed.mjs`, `demo-logistics-seed.mjs`, `demo-ngo-seed.mjs`, then `demo-projects-seed.mjs` in `backend/tests` (logins are listed at the top of each file)
- Tests against a running API, all in `backend/tests`: `smoke.mjs` (core, 18 checks), `hr-smoke.mjs` (HR & payroll,
  33 checks with hand-computed tax figures), `finance-smoke.mjs` (finance, 34 checks; the statements must balance), `inventory-smoke.mjs` (inventory & procurement,
  30 checks; the stock value must equal the ledger), `hotel-smoke.mjs` (hotel, 24 checks),
  `travel-smoke.mjs` (travel & tours, 21 checks), `logistics-smoke.mjs` (logistics, 24 checks; the trial balance must balance), `ngo-smoke.mjs` (NGO, 33 checks; fund balances and
  the ledger must agree), `projects-smoke.mjs` (projects & services, 37 checks), `integrity-smoke.mjs` (concurrent double-clicks and
  half-finished operations, 9 checks), `security-smoke.mjs` (privilege escalation and sessions, 10 checks), `compliance-smoke.mjs` (withholding tax and bank
  reconciliation, 20 checks).
  GitHub Actions runs all of them on every push (`.github/workflows/ci.yml`).

## Concepts

| Concept | Meaning |
|---|---|
| **Platform Admin** | Runs the SaaS, onboards organizations (tenants). Sees no tenant business data. |
| **Organization (Tenant)** | A customer. All its data is isolated by `TenantId` through EF Core global query filters. |
| **Entity** | A node in the org tree: company → division → branch → department… with no depth limit. Stored with a materialized `Path`, so "this entity and everything under it" is a single prefix query. |
| **Modules** | Enabled per entity. A sub-entity can only enable what its parent has; disabling cascades down. A permission only takes effect where its module is on. |
| **Permission** | `module.resource.action`, e.g. `hotel.reservations.checkin`. Catalog: `Erpos.Application/Authorization/PermissionCatalog.cs`. |
| **Role** | Bundle of permissions, owned by the organization. Defaults: Administrator, Manager, Employee (editable). |
| **Assignment** | User + Role + Entity (+ include sub-entities). Assigning at a parent covers all descendants. |
| **Override** | Per-user Grant or Deny of one permission at an entity. Deny wins. |
| **User type** | SuperAdmin (everything in the org) › Admin › Manager › Employee. Decides who can manage whom; real access comes from roles. |

Rules that stop privilege escalation:
- To assign a role you need `core.users.assign` at that entity **and** must hold every permission in the role there.
- To grant or deny an override, you must hold that permission yourself.
- Non-owners editing a role can only add permissions they hold wherever the role is assigned.
- Admins can't create Super Admins; Managers can only manage Employees.

Every create, update or delete is written to the audit log automatically (`AppDbContext.SaveChangesAsync`).

## HR & Payroll (Pakistan)

- **Employees** always have a login. New logins get the baseline *Employee* role (apply for leave only); people always see
  their own attendance, leave, salary and payslips in **My Workspace**.
- **Attendance** is manual and exception-based: unmarked working days count as present. Weekly offs and holidays
  (organization-wide or per entity, inherited by sub-entities) come from settings.
- **Leave** runs through a configurable multi-step chain (default: line manager → HR). Unresolvable steps are skipped,
  nobody approves their own request or two steps of the same request, and an all-skipped chain falls back to HR.
  Default types: 14 annual, 10 casual, 8 sick, maternity, paternity, unpaid (editable; prorated in the joining year).
- **Payroll** per entity and month: Draft → Approved (by someone other than the preparer; locks attendance/leave) → Posted.
  - Unpaid days (absent, half day, unpaid leave, before joining/after leaving) prorate earnings by calendar days.
  - Medical allowance is tax-exempt up to 10% of basic.
  - Income tax follows section 149: (annual tax on projected income − tax already withheld) ÷ remaining months.
  - FBR slab tables are stored per tax year and editable. Seeded: TY2026 (Finance Act 2025) and TY2027 (Finance Act 2026).
  - EOBI 1% employee / 5% employer of the minimum wage (Rs 40,700). PF and provincial social security are optional.
  - Exports a CSV bank-transfer sheet.

> Check the tax slabs, minimum wage and leave entitlements against current FBR and provincial notifications before
> running live payroll.

## Finance (accrual, IFRS for SMEs layout)

- **Chart of accounts**: seeded with an IFRS for SMEs statement layout and editable. Group accounts organize the chart; only posting
  accounts take entries.
- **Ledger engine** (`LedgerService`): every posting is double-entry and balanced in both the transaction currency and the
  base currency. Posted entries are immutable; corrections are reversals. Closed periods (`LockedThrough`) block back-dating.
  Numbers come from an atomic per-organization counter per fiscal year (JV-2027-00001, INV-…, BILL-…, RCT-…, PAY-…).
- **Invoices & bills**: draft → approve (numbered and posted) → paid. GST 18% and provincial sales tax on services are
  posted to output/input tax accounts. Printable sales-tax invoice with NTN/STRN. Voiding reverses the journal.
- **Multi-currency**: documents in any currency, using a stored or typed rate. Receivables and payables are cleared at the
  document's rate and the bank at the payment's rate, with the difference posted as **realized** exchange gain/loss. Bank
  accounts can hold a foreign currency.
- **Payroll → ledger**: posting a payroll run automatically accrues salaries, tax withheld, EOBI/PF/social security and net
  pay (split by entity); "Record salary transfer" clears salaries payable against the bank.
- **Reports** (consolidated or per entity): P&L, balance sheet (prior years' profit is rolled into retained earnings, no
  closing entries), trial balance, general ledger with drill-down, AR/AP aging, monthly sales tax summary, and a dashboard.

- **Withholding tax (s.153):** editable rates for goods, services and contracts, for companies and for individuals/AOPs.
  Suppliers not on FBR's Active Taxpayer List are charged double.
  - Paying a bill withholds the tax on the part paid, excluding sales tax. The bill is settled for the gross amount,
    the bank pays the net, and the tax is held in 2185.
  - Monthly register; deposit to the treasury with the CPR number; section 164 certificates per supplier.
- **Bank reconciliation:** import the bank's CSV export, auto-match by amount and date, match by hand, or post bank
  charges straight from a statement line. Deposits in transit and unpresented cheques are worked out; completion
  requires the statement and the books to agree.

Not yet: FBR POS/IRIS e-invoicing integration (needs FBR credentials), unrealized FX revaluation, budgets, and paying a
foreign-currency invoice from a base-currency account.

## Inventory & Procurement

- **Items** are stock or service items; stock items can track **batches and expiry**. **Warehouses** belong to branches.
- **Weighted average cost** per item per warehouse (`StockEngine`). Every change writes an immutable stock movement, and
  stock can't go negative. Optimistic concurrency stops two people corrupting an average at the same moment.
- **Issues** pick the **earliest-expiring batch first** and never issue expired stock; adjustments can write expired
  batches off. **Transfers** carry exact value between warehouses and branches; **adjustments and stock counts**
  post to an adjustments account; **opening stock** posts against retained earnings or an account you choose.
- **Purchase request → purchase order → goods received note → bill (three-way match)**:
  - Requests and orders need approval by a different person from the one who raised them.
  - A GRN brings stock in at the PO price: Dr Inventory (or the service's expense) / Cr *Goods received not invoiced*.
  - "Create bill" drafts the vendor bill for received-but-unbilled quantities. On approval, the quantity must not
    exceed received minus billed and the price must be within tolerance (default 2%). It clears GRNI, and price or FX
    differences go to *Purchase price variance*. Voiding the bill gives the quantities back to the PO.
- **Reports**: stock on hand with reorder flags, batches and expiry, stock movements, received-not-billed (GRNI) detail,
  and **valuation vs the general ledger** (they must agree).

Not yet: sales/stock-out against customer invoices, purchase returns / debit notes, landed costs, serial numbers,
multiple units of measure per item, barcode scanning, and stock reservations.

## Hotel

- **Rooms & rates**: room types per property (rack rate, occupancy, sales tax on room nights), rooms, guest register (CNIC/passport, VIP).
- **Reservations**: availability per room type and night; overbooking is refused. Tentative and confirmed bookings,
  bill-to company or travel agent, negotiated rates, and room assignment with no double-booking of a room.
- **Front desk**: arrivals, departures and in-house lists, occupancy, and a 14-day room calendar.
  - **Check-in** needs every room assigned, clean or inspected, and not occupied.
  - **Deposits** are held as customer advances (Dr Bank / Cr Customer advances).
- **Folio**: restaurant, laundry, transport and other charges. Minibar or restaurant items can be issued from an outlet
  warehouse at average cost.
- **Night audit** posts each in-house room night, and is safe to run more than once.
- **Check-out**:
  - Bills any nights not yet charged; early or late departure bills the nights actually stayed.
  - Issues an approved **sales tax invoice** (room nights grouped).
  - Applies deposits and records the settlement. A walk-in can't leave unpaid; a bill-to company keeps the balance
    on its account.
  - Marks the rooms dirty. If settlement fails, checkout resumes on retry without invoicing twice.
- **Housekeeping board** (Clean / Dirty / Inspected / Out of order) and **performance report** (occupancy, ADR, RevPAR,
  revenue by outlet).
- Front-desk staff need no finance permissions: checkout posts invoices and payments as a system action.

Not yet: online booking engine / OTA channel manager, rate plans and seasons, group blocks, split folios, deposit
refunds from the front desk (use Finance), POS for the restaurant, and key-card integration.

## Travel & Tours

- **Tour packages** with a day-by-day itinerary, inclusions/exclusions, adult, child and single prices, and sales tax.
- **Departures**: dated runs with seat capacity (no duplicate departures). Each has:
  - guides (one guide can't lead overlapping departures)
  - operating costs that can be drafted as vendor bills
  - a printable passenger manifest with passport warnings
  - profit: revenue minus costs minus guide days
- **Travel files (bookings)**: customer, passengers (CNIC/passport) and services, each with a supplier, cost price and
  selling price:
  - tour seats (seats are held on confirmation and capacity is enforced)
  - flights (airline, route, PNR, ticket number), hotels, visas (status tracker per passenger), transport, insurance
- **Warnings**: passports expiring within 6 months of travel, missing passports for visa travel, rejected visas, and
  flights without ticket numbers.
- **Money**:
  - Advances are customer deposits.
  - "Invoice customer" issues an approved sales invoice and applies the advances.
  - "Create supplier bills" drafts one cost-of-sales bill per supplier for Accounts to approve.
  - Margin is shown per file and per departure.
- **Dashboard**: open files, monthly sales and margin, departures with load factor, upcoming travel, visas in progress,
  and passport alerts.

Not yet: GDS/airline API ticketing, online tour booking, rooming lists, multi-currency package pricing, commissions to
sub-agents, and refunds/credit notes from Travel (use Finance).

## Logistics

- **Routes (rate cards)**: rate per kg with a minimum charge, a full-truck rate, a fuel surcharge %, transit hours and
  optional sales tax on services. Express is priced at 1.5× part load, and a negotiated freight can override the rate card.
- **Consignments (CN / bilty)**: shipper, consignee, lane, pieces, weight, declared value and a promised date. They
  can be printed, and are tracked through a timeline (booked → in transit → at hub → out for delivery → delivered/returned).
  Three ways to pay:
  - Prepaid: invoiced and collected at booking
  - To pay: invoiced and collected from the consignee on delivery
  - Account: billed monthly in one consolidated invoice
- **Cash on delivery**: the full COD must be collected at delivery. It is held in COD payable (2210) until it is
  remitted to the shipper; remitting needs a separate permission, `logistics.cod.remit`.
- **Trips (load sheets)**: vehicle, driver, route and manifest.
  - Loading is capacity-checked, and a consignment can't be on two trips at once.
  - Dispatch is blocked by expired fitness, insurance, route permit or token tax papers, an expired licence, or a
    vehicle or driver already on the road.
  - Arrival puts the consignments at the hub.
  - Trip expenses (fuel, tolls, allowance, loading, hire, repairs) are posted from cash/bank or drafted as vendor bills.
  - Trip margin and cost per km are shown.
- **Fleet**: own and hired vehicles (with owner/broker), paper expiry alerts, odometer, drivers with licence expiry,
  and a maintenance log that drafts workshop bills.
- **Dashboard**: bookings and deliveries today, on-time %, monthly freight, COD awaiting remittance, active trips and
  papers expiring within 30 days.

Not yet: GPS/live tracking, a customer tracking portal, proof-of-delivery photos and signatures, rider apps,
fuel-card imports, and container/port (customs) workflows.

## NGO

- **Funds** (fund accounting):
  - Unrestricted gifts are income at once (4300).
  - Restricted gifts, grant tranches and Zakat are held as deferred income (2220 / 2230). They are released to income
    (4310) as the money is spent.
  - Endowments are capital (3300) and can't be spent.
  - Restricted and Zakat funds can't be overspent. Zakat can only be paid out as assistance to beneficiaries verified
    as Zakat-eligible, never for administration.
- **Donors and donations**: individual, corporate, foundation, institutional and government donors (CNIC/NTN). Every
  donation gets a numbered, printable receipt with the amount in words (lakh/crore).
- **Grants**:
  - Each grant has a budget by line, tranches (instalments) and its own restricted fund. Approval requires the tranches
    to add up to the budget, and generates the donor reporting schedule (periodic reports due 30 days after each period,
    final report 60 days after the end).
  - Grants can be in a foreign currency: actuals are converted at the average rate of the money received.
  - Costs can only be charged inside the grant period, on a budget line, within the line's flexibility % (e.g. 10%
    overspend without donor approval).
  - Costs are charged as paid now, owed to a vendor (an approved bill), or allocated from a cost already booked
    (e.g. salaries).
  - Warnings: overdue tranches and reports, lines over budget, spending behind schedule, pre-financing.
  - Closing requires all reports submitted. Unspent money is refunded to the donor; overspend is borne by unrestricted funds.
- **Beneficiaries**: register with organization-wide CNIC deduplication, household size, vulnerabilities, Zakat
  eligibility and program. Assistance is cash (charged to a fund or grant line), in kind or service, and the same help
  under the same program within 30 days is flagged.
- **Reports**: statement of functional expenses (program / management & general / fundraising, program ratio), donor
  summary, fund balances, and a dashboard with grant burn vs time and upcoming deadlines.
- Roles: Grants Manager, Program Officer (registration and in-kind help only; cash needs spending permission),
  Fundraising Officer.

Not yet: donor portal, online giving, recurring pledges, beneficiary photos/biometrics, M&E indicators and logframes,
and indirect cost recovery.

## Projects & services

- **Clients** are finance customers.
- **Projects** come in three billing types:
  - **Time & materials:** approved billable hours are invoiced at the member's or project's rate.
  - **Fixed price:** milestones must add up to the contract and are invoiced when completed.
  - **Non-billable:** internal work.
- Each project has a team with bill rates and cost rates. The cost rate defaults to salary ÷ 176 hours, and can be
  overridden per person.
- Each project also has an hour budget, a manager, sales tax on services, and states: planned → active ⇄ on hold →
  completed / cancelled.
- **Task board** per project (to do / in progress / review / done) with drag and drop, priorities, estimates against
  logged hours, and due dates. Assignees can move their own cards.
- **Weekly timesheets**: everyone logs their own time on projects whose team they're on.
  - Time must be on an active project, inside the project dates, not in the future, in quarter hours, and at most
    24 hours a day.
  - The week is submitted for approval. Approvers (never for their own time) approve, or reject with a reason; the
    person corrects the entry and resubmits.
  - Rates are captured when time is logged.
- **Billing**: one invoice per project for approved hours (one line per person and rate) or per completed milestone.
  Invoices post through Finance to revenue (4100) and receivables. Invoiced time can't be billed again.
- **Profitability**: hours vs budget, invoiced, ready to invoice, team cost and margin per project.
- **Utilization**: billable hours ÷ capacity (8 h per weekday employed).
- **Dashboard**: projects at risk, milestones due, overdue tasks, approvals waiting.
- Roles: Project Manager (everything in projects, including approvals and billing); Accountant can invoice projects;
  every employee keeps a timesheet.

Not yet: expense claims billed to projects, retainers and recurring billing, Gantt charts and dependencies, Git/Jira
integration, client portal, and percentage-of-completion revenue recognition.

## Layout

```
backend/
  src/Erpos.Domain          entities and enums
  src/Erpos.Application     permission/module catalogs, AccessService (permission engine), services, DTOs
  src/Erpos.Infrastructure  EF Core (MySQL), migrations, JWT, BCrypt, seeding
  src/Erpos.Api             controllers, current user, error handling
  tests/                    smoke test and demo seed
frontend/src
  auth/  layout/  pages/  components/  api/
```

## Roadmap

1. ✅ Core: tenants, entity tree, users, roles, permissions, overrides, modules, audit
2. ✅ HR & Payroll: employees, departments, attendance, multi-step leave, salaries, Pakistani payroll
3. ✅ Finance: IFRS chart, ledger, GST invoices/bills, multi-currency payments, payroll postings, statements
4. ✅ Inventory & Procurement: weighted average, batches/expiry, transfers, PR → PO → GRN → three-way-matched bill
5. Industry modules: ✅ Hotel · ✅ Travel & Tours · ✅ Logistics · ✅ NGO · ✅ Projects/Software services

New modules plug into the existing catalog: add permissions in `PermissionCatalog.cs`, make tables `IEntityScoped`,
and call `access.EnsureAsync("module.resource.action", entityId)` in services.

## Deploy

```bash
cp .env.prod.example .env.prod          # real secrets: DB passwords, JWT key (32+ chars), platform admin, public URL
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

- **Services:** `mysql`, `migrate` (one-off: `dotnet Erpos.Api.dll --migrate` applies migrations and data upgrades,
  then exits), `api` (starts only after `migrate` succeeds; no public port), `web` (nginx serving the app and
  proxying `/api`), `backup` (nightly `mysqldump`, kept 14 days in `./backups`; binary logs allow point-in-time recovery).
- **Migrations** never run on API startup outside Development (`Database:MigrateOnStartup`); an API started against a
  database with pending migrations refuses to start instead of running half-upgraded.
- **TLS:** terminate HTTPS in front of the `web` port (Caddy, a cloud load balancer or certbot) and forward
  `X-Forwarded-Proto`; the API honours forwarded headers and sends HSTS outside Development.
- **Health:** `/health/live` (process up) and `/health/ready` (database reachable) for load balancers and uptime checks.
- **Logs:** JSON lines on stdout in production, each tagged with tenant and user; error responses carry a `traceId`
  that matches the log entry.
- **Backups:** copy `./backups` off the server (rclone/S3/another host) and test a restore regularly:
  `gunzip < backups/erpos-YYYYMMDD-HHMM.sql.gz | docker compose -f docker-compose.prod.yml exec -T mysql mysql -u root -p`.
- **Configuration** comes from environment variables (`ConnectionStrings__Default`, `Jwt__Key`, `Seed__*`,
  `Cors__Origins__0`, `AllowedHosts`, `Security__AuthRequestsPerMinute`). `.env`, `.env.prod` and
  `appsettings.Development.json` are local only and never committed.
