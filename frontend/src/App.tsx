import { lazy, useMemo, type ComponentType, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Result, Spin } from 'antd'
import { useAuth } from './auth/AuthContext'
import { canOpen } from './auth/routeAccess'
import AppLayout from './layout/AppLayout'
import LoginPage from './pages/LoginPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import DashboardPage from './pages/DashboardPage'

// Every page is loaded on first visit, so a payroll clerk never downloads the hotel or NGO screens.
// Pages of one module share a chunk (they come from the same file).
type Loader = () => Promise<Record<string, unknown>>
const page = (load: Loader, name = 'default') =>
  lazy(async () => ({ default: (await load())[name] as ComponentType<Record<string, unknown>> }))

const hr = () => import('./pages/hr/MyWorkspacePage')
const EntitiesPage = page(() => import('./pages/EntitiesPage'))
const UsersPage = page(() => import('./pages/UsersPage'))
const RolesPage = page(() => import('./pages/RolesPage'))
const TenantsPage = page(() => import('./pages/TenantsPage'))
const AuditPage = page(() => import('./pages/AuditPage'))
const OutboxPage = page(() => import('./pages/OutboxPage'))
const ProfilePage = page(() => import('./pages/ProfilePage'))
const ModulePlaceholder = page(() => import('./pages/ModulePlaceholder'))
const MyWorkspacePage = page(hr)
const EmployeesPage = page(() => import('./pages/hr/EmployeesPage'))
const EmployeeDetailPage = page(() => import('./pages/hr/EmployeeDetailPage'))
const AttendancePage = page(() => import('./pages/hr/AttendancePage'))
const LeavePage = page(() => import('./pages/hr/LeavePage'))
const OrgStructurePage = page(() => import('./pages/hr/OrgStructurePage'))
const PayrollRunsPage = page(() => import('./pages/payroll/PayrollRunsPage'))
const PayrollRunDetailPage = page(() => import('./pages/payroll/PayrollRunDetailPage'))
const PayrollSettingsPage = page(() => import('./pages/payroll/PayrollSettingsPage'))
const DocumentsPage = page(() => import('./pages/finance/DocumentsPage'))
const ledger = () => import('./pages/finance/LedgerPages')
const ContactsPage = page(ledger, 'ContactsPage')
const JournalsPage = page(ledger, 'JournalsPage')
const PaymentsPage = page(ledger, 'PaymentsPage')
const reports = () => import('./pages/finance/ReportsPage')
const ReportsPage = page(reports)
const FinanceDashboardPage = page(reports, 'FinanceDashboardPage')
const FinanceSettingsPage = page(() => import('./pages/finance/FinanceSettingsPage'))
const FixedAssetsPage = page(() => import('./pages/finance/FixedAssetsPage'))
const compliance = () => import('./pages/finance/CompliancePages')
const WithholdingPage = page(compliance, 'WithholdingPage')
const BankReconciliationPage = page(compliance, 'BankReconciliationPage')
const inventory = () => import('./pages/inventory/InventoryPages')
const InventorySetupPage = page(inventory, 'InventorySetupPage')
const StockPage = page(inventory, 'StockPage')
const StockTransactionsPage = page(inventory, 'StockTransactionsPage')
const procurement = () => import('./pages/inventory/ProcurementPages')
const PurchaseOrdersPage = page(procurement, 'PurchaseOrdersPage')
const PurchaseRequestsPage = page(procurement, 'PurchaseRequestsPage')
const hotel = () => import('./pages/hotel/HotelPages')
const FrontDeskPage = page(hotel, 'FrontDeskPage')
const HotelReportsPage = page(hotel, 'HotelReportsPage')
const HotelSetupPage = page(hotel, 'HotelSetupPage')
const HousekeepingPage = page(hotel, 'HousekeepingPage')
const ReservationsPage = page(hotel, 'ReservationsPage')
const travel = () => import('./pages/travel/TravelPages')
const BookingsPage = page(travel, 'BookingsPage')
const DeparturesPage = page(travel, 'DeparturesPage')
const PackagesPage = page(travel, 'PackagesPage')
const TravelDashboardPage = page(travel, 'TravelDashboardPage')
const logistics = () => import('./pages/logistics/LogisticsPages')
const CodBillingPage = page(logistics, 'CodBillingPage')
const ConsignmentsPage = page(logistics, 'ConsignmentsPage')
const FleetPage = page(logistics, 'FleetPage')
const LogisticsDashboardPage = page(logistics, 'LogisticsDashboardPage')
const TripsPage = page(logistics, 'TripsPage')
const ngo = () => import('./pages/ngo/NgoPages')
const BeneficiariesPage = page(ngo, 'BeneficiariesPage')
const DonationsPage = page(ngo, 'DonationsPage')
const FundsPage = page(ngo, 'FundsPage')
const GrantsPage = page(ngo, 'GrantsPage')
const NgoDashboardPage = page(ngo, 'NgoDashboardPage')
const NgoReportsPage = page(ngo, 'NgoReportsPage')
const projects = () => import('./pages/projects/ProjectPages')
const ClientsPage = page(projects, 'ClientsPage')
const MyTimesheetPage = page(projects, 'MyTimesheetPage')
const ProjectsDashboardPage = page(projects, 'ProjectsDashboardPage')
const ProjectsPage = page(projects, 'ProjectsPage')
const TimeApprovalsPage = page(projects, 'TimeApprovalsPage')
const UtilizationPage = page(projects, 'UtilizationPage')

/** Shows "no access" for a page the user lacks permission for, instead of an empty screen full of failed requests. */
function Guard({ path, children }: { path: string; children: ReactNode }) {
  const { me } = useAuth()
  const held = useMemo(() => new Set(me?.entities.flatMap(e => e.permissions) ?? []), [me])
  if (!canOpen(path, held)) return <Result status="403" title="No access" subTitle="You don't have permission to open this page. Ask your administrator if you need it." />
  return <>{children}</>
}

export default function App() {
  const { me, loading, isPlatformAdmin } = useAuth()

  if (loading) return <Spin fullscreen />
  if (!me) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  // [path, page]; the access rule comes from ROUTE_ACCESS (detail routes reuse their list page's rule).
  const routes: [string, () => ReactNode, string?][] = [
    ['/', () => <DashboardPage />],
    ['/entities', () => <EntitiesPage />],
    ['/users', () => <UsersPage />],
    ['/roles', () => <RolesPage />],
    ['/audit', () => <AuditPage />],
    ['/admin/email', () => <OutboxPage />],
    ['/me', () => <MyWorkspacePage />],
    ['/hr/employees', () => <EmployeesPage />],
    ['/hr/employees/:id', () => <EmployeeDetailPage />, '/hr/employees'],
    ['/hr/attendance', () => <AttendancePage />],
    ['/hr/leave', () => <LeavePage />],
    ['/hr/structure', () => <OrgStructurePage />],
    ['/payroll', () => <PayrollRunsPage />],
    ['/payroll/runs/:id', () => <PayrollRunDetailPage />, '/payroll'],
    ['/payroll/settings', () => <PayrollSettingsPage />],
    ['/finance', () => <FinanceDashboardPage />],
    ['/finance/invoices', () => <DocumentsPage kind="Invoice" />],
    ['/finance/bills', () => <DocumentsPage kind="Bill" />],
    ['/finance/payments', () => <PaymentsPage />],
    ['/finance/journals', () => <JournalsPage />],
    ['/finance/contacts', () => <ContactsPage />],
    ['/finance/reports', () => <ReportsPage />],
    ['/finance/settings', () => <FinanceSettingsPage />],
    ['/finance/assets', () => <FixedAssetsPage />],
    ['/finance/withholding', () => <WithholdingPage />],
    ['/finance/reconciliation', () => <BankReconciliationPage />],
    ['/inventory/stock', () => <StockPage />],
    ['/inventory/transactions', () => <StockTransactionsPage />],
    ['/inventory/setup', () => <InventorySetupPage />],
    ['/procurement/requests', () => <PurchaseRequestsPage />],
    ['/procurement/orders', () => <PurchaseOrdersPage />],
    ['/hotel/front-desk', () => <FrontDeskPage />],
    ['/hotel/reservations', () => <ReservationsPage />],
    ['/hotel/housekeeping', () => <HousekeepingPage />],
    ['/hotel/setup', () => <HotelSetupPage />],
    ['/hotel/reports', () => <HotelReportsPage />],
    ['/travel', () => <TravelDashboardPage />],
    ['/travel/bookings', () => <BookingsPage />],
    ['/tours/departures', () => <DeparturesPage />],
    ['/tours/packages', () => <PackagesPage />],
    ['/logistics', () => <LogisticsDashboardPage />],
    ['/logistics/consignments', () => <ConsignmentsPage />],
    ['/logistics/trips', () => <TripsPage />],
    ['/logistics/fleet', () => <FleetPage />],
    ['/logistics/billing', () => <CodBillingPage />],
    ['/ngo', () => <NgoDashboardPage />],
    ['/ngo/grants', () => <GrantsPage />],
    ['/ngo/donations', () => <DonationsPage />],
    ['/ngo/beneficiaries', () => <BeneficiariesPage />],
    ['/ngo/funds', () => <FundsPage />],
    ['/ngo/reports', () => <NgoReportsPage />],
    ['/projects', () => <ProjectsDashboardPage />],
    ['/projects/list', () => <ProjectsPage />],
    ['/projects/clients', () => <ClientsPage />],
    ['/projects/approvals', () => <TimeApprovalsPage />],
    ['/projects/utilization', () => <UtilizationPage />],
    ['/timesheet', () => <MyTimesheetPage />],
    ['/m/:module', () => <ModulePlaceholder />],
  ]

  return (
    <Routes>
      <Route element={<AppLayout />}>
        {isPlatformAdmin ? (
          <>
            <Route path="/" element={<Navigate to="/platform/tenants" replace />} />
            <Route path="/platform/tenants" element={<TenantsPage />} />
          </>
        ) : (
          routes.map(([path, render, rule]) => <Route key={path} path={path} element={<Guard path={rule ?? path}>{render()}</Guard>} />)
        )}
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
