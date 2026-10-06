import { Navigate, Route, Routes } from 'react-router-dom'
import { Spin } from 'antd'
import { useAuth } from './auth/AuthContext'
import AppLayout from './layout/AppLayout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import EntitiesPage from './pages/EntitiesPage'
import UsersPage from './pages/UsersPage'
import RolesPage from './pages/RolesPage'
import TenantsPage from './pages/TenantsPage'
import AuditPage from './pages/AuditPage'
import ProfilePage from './pages/ProfilePage'
import ModulePlaceholder from './pages/ModulePlaceholder'
import MyWorkspacePage from './pages/hr/MyWorkspacePage'
import EmployeesPage from './pages/hr/EmployeesPage'
import EmployeeDetailPage from './pages/hr/EmployeeDetailPage'
import AttendancePage from './pages/hr/AttendancePage'
import LeavePage from './pages/hr/LeavePage'
import OrgStructurePage from './pages/hr/OrgStructurePage'
import PayrollRunsPage from './pages/payroll/PayrollRunsPage'
import PayrollRunDetailPage from './pages/payroll/PayrollRunDetailPage'
import PayrollSettingsPage from './pages/payroll/PayrollSettingsPage'
import DocumentsPage from './pages/finance/DocumentsPage'
import { ContactsPage, JournalsPage, PaymentsPage } from './pages/finance/LedgerPages'
import ReportsPage, { FinanceDashboardPage } from './pages/finance/ReportsPage'
import FinanceSettingsPage from './pages/finance/FinanceSettingsPage'
import { InventorySetupPage, StockPage, StockTransactionsPage } from './pages/inventory/InventoryPages'
import { PurchaseOrdersPage, PurchaseRequestsPage } from './pages/inventory/ProcurementPages'
import { FrontDeskPage, HotelReportsPage, HotelSetupPage, HousekeepingPage, ReservationsPage } from './pages/hotel/HotelPages'
import { BookingsPage, DeparturesPage, PackagesPage, TravelDashboardPage } from './pages/travel/TravelPages'
import { CodBillingPage, ConsignmentsPage, FleetPage, LogisticsDashboardPage, TripsPage } from './pages/logistics/LogisticsPages'
import { BeneficiariesPage, DonationsPage, FundsPage, GrantsPage, NgoDashboardPage, NgoReportsPage } from './pages/ngo/NgoPages'
import { ClientsPage, MyTimesheetPage, ProjectsDashboardPage, ProjectsPage, TimeApprovalsPage, UtilizationPage } from './pages/projects/ProjectPages'

export default function App() {
  const { me, loading, isPlatformAdmin } = useAuth()

  if (loading) return <Spin fullscreen />
  if (!me) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  return (
    <Routes>
      <Route element={<AppLayout />}>
        {isPlatformAdmin ? (
          <>
            <Route path="/" element={<Navigate to="/platform/tenants" replace />} />
            <Route path="/platform/tenants" element={<TenantsPage />} />
          </>
        ) : (
          <>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/entities" element={<EntitiesPage />} />
            <Route path="/users" element={<UsersPage />} />
            <Route path="/roles" element={<RolesPage />} />
            <Route path="/audit" element={<AuditPage />} />
            <Route path="/me" element={<MyWorkspacePage />} />
            <Route path="/hr/employees" element={<EmployeesPage />} />
            <Route path="/hr/employees/:id" element={<EmployeeDetailPage />} />
            <Route path="/hr/attendance" element={<AttendancePage />} />
            <Route path="/hr/leave" element={<LeavePage />} />
            <Route path="/hr/structure" element={<OrgStructurePage />} />
            <Route path="/payroll" element={<PayrollRunsPage />} />
            <Route path="/payroll/runs/:id" element={<PayrollRunDetailPage />} />
            <Route path="/payroll/settings" element={<PayrollSettingsPage />} />
            <Route path="/finance" element={<FinanceDashboardPage />} />
            <Route path="/finance/invoices" element={<DocumentsPage kind="Invoice" />} />
            <Route path="/finance/bills" element={<DocumentsPage kind="Bill" />} />
            <Route path="/finance/payments" element={<PaymentsPage />} />
            <Route path="/finance/journals" element={<JournalsPage />} />
            <Route path="/finance/contacts" element={<ContactsPage />} />
            <Route path="/finance/reports" element={<ReportsPage />} />
            <Route path="/finance/settings" element={<FinanceSettingsPage />} />
            <Route path="/inventory/stock" element={<StockPage />} />
            <Route path="/inventory/transactions" element={<StockTransactionsPage />} />
            <Route path="/inventory/setup" element={<InventorySetupPage />} />
            <Route path="/procurement/requests" element={<PurchaseRequestsPage />} />
            <Route path="/procurement/orders" element={<PurchaseOrdersPage />} />
            <Route path="/hotel/front-desk" element={<FrontDeskPage />} />
            <Route path="/hotel/reservations" element={<ReservationsPage />} />
            <Route path="/hotel/housekeeping" element={<HousekeepingPage />} />
            <Route path="/hotel/setup" element={<HotelSetupPage />} />
            <Route path="/hotel/reports" element={<HotelReportsPage />} />
            <Route path="/travel" element={<TravelDashboardPage />} />
            <Route path="/travel/bookings" element={<BookingsPage />} />
            <Route path="/tours/departures" element={<DeparturesPage />} />
            <Route path="/tours/packages" element={<PackagesPage />} />
            <Route path="/logistics" element={<LogisticsDashboardPage />} />
            <Route path="/logistics/consignments" element={<ConsignmentsPage />} />
            <Route path="/logistics/trips" element={<TripsPage />} />
            <Route path="/logistics/fleet" element={<FleetPage />} />
            <Route path="/logistics/billing" element={<CodBillingPage />} />
            <Route path="/ngo" element={<NgoDashboardPage />} />
            <Route path="/ngo/grants" element={<GrantsPage />} />
            <Route path="/ngo/donations" element={<DonationsPage />} />
            <Route path="/ngo/beneficiaries" element={<BeneficiariesPage />} />
            <Route path="/ngo/funds" element={<FundsPage />} />
            <Route path="/ngo/reports" element={<NgoReportsPage />} />
            <Route path="/projects" element={<ProjectsDashboardPage />} />
            <Route path="/projects/list" element={<ProjectsPage />} />
            <Route path="/projects/clients" element={<ClientsPage />} />
            <Route path="/projects/approvals" element={<TimeApprovalsPage />} />
            <Route path="/projects/utilization" element={<UtilizationPage />} />
            <Route path="/timesheet" element={<MyTimesheetPage />} />
            <Route path="/m/:module" element={<ModulePlaceholder />} />
          </>
        )}
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
