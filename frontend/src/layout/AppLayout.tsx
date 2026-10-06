import { useMemo, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Avatar, Badge, Button, Dropdown, Grid, Layout, Menu, Tag, Typography, theme, type MenuProps } from 'antd'
import {
  ApartmentOutlined, AuditOutlined, BankOutlined, CarOutlined, DashboardOutlined, DollarOutlined, GlobalOutlined,
  HeartOutlined, HomeOutlined, InboxOutlined, LogoutOutlined, ProjectOutlined, SafetyOutlined, ShoppingCartOutlined,
  TeamOutlined, UserOutlined, WalletOutlined, CompassOutlined, SmileOutlined, IdcardOutlined, CalendarOutlined,
  ScheduleOutlined, ClusterOutlined, SettingOutlined, MenuOutlined, FileTextOutlined, FileDoneOutlined,
  SwapOutlined, BookOutlined, ContactsOutlined, BarChartOutlined, FundOutlined, DatabaseOutlined, RetweetOutlined, AppstoreOutlined,
  SolutionOutlined, ShoppingOutlined, CarryOutOutlined, FlagOutlined, ReadOutlined, BellOutlined, CalendarTwoTone, ClearOutlined, KeyOutlined, LineChartOutlined,
} from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import { P, useAuth } from '../auth/AuthContext'
import { api } from '../api/client'

const { Header, Sider, Content } = Layout

// Business modules appear in the menu when the user holds any of their permissions somewhere.
export const BUSINESS_MODULES = [
  { code: 'hr', label: 'Human Resources', icon: <TeamOutlined /> },
  { code: 'payroll', label: 'Payroll', icon: <WalletOutlined /> },
  { code: 'finance', label: 'Finance', icon: <DollarOutlined /> },
  { code: 'inventory', label: 'Inventory', icon: <InboxOutlined /> },
  { code: 'procurement', label: 'Procurement', icon: <ShoppingCartOutlined /> },
  { code: 'hotel', label: 'Hotel', icon: <HomeOutlined /> },
  { code: 'travel', label: 'Travel', icon: <GlobalOutlined /> },
  { code: 'tourism', label: 'Tours & Packages', icon: <CompassOutlined /> },
  { code: 'logistics', label: 'Logistics', icon: <CarOutlined /> },
  { code: 'ngo', label: 'NGO', icon: <HeartOutlined /> },
  { code: 'projects', label: 'Projects', icon: <ProjectOutlined /> },
]

export default function AppLayout() {
  const { me, can, logout, isPlatformAdmin } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const screens = Grid.useBreakpoint()
  const [collapsed, setCollapsed] = useState(false)
  const { token } = theme.useToken()
  // Pending leave approvals for the badge (refreshes every minute).
  const { data: inbox } = useQuery({
    queryKey: ['leave-inbox'], enabled: !isPlatformAdmin, refetchInterval: 60_000,
    queryFn: async () => (await api.get<unknown[]>('/hr/leave/inbox')).data,
  })
  const pending = inbox?.length ?? 0

  const items = useMemo<MenuProps['items']>(() => {
    if (isPlatformAdmin)
      return [{ key: '/platform/tenants', icon: <BankOutlined />, label: 'Organizations' }]

    const held = new Set(me?.entities.flatMap(e => e.permissions) ?? [])
    const hasModule = (m: string) => [...held].some(p => p.startsWith(m + '.'))

    const admin: MenuProps['items'] = [
      can(P.entitiesView) && { key: '/entities', icon: <ApartmentOutlined />, label: 'Entities' },
      can(P.usersView) && { key: '/users', icon: <UserOutlined />, label: 'Users' },
      (can(P.rolesView) || can(P.rolesManage)) && { key: '/roles', icon: <SafetyOutlined />, label: 'Roles & Permissions' },
      can(P.auditView) && { key: '/audit', icon: <AuditOutlined />, label: 'Audit Log' },
    ].filter(Boolean) as MenuProps['items']

    // HR and Payroll have real screens; other modules still show the placeholder.
    const hr: MenuProps['items'] = [
      can(P.employeesView) && { key: '/hr/employees', icon: <IdcardOutlined />, label: 'Employees' },
      can(P.attendanceView) && { key: '/hr/attendance', icon: <CalendarOutlined />, label: 'Attendance' },
      { key: '/hr/leave', icon: <ScheduleOutlined />, label: <Badge count={pending} size="small" offset={[10, 0]}>Leave</Badge> },
      (can(P.departmentsView) || can(P.departmentsCreate)) && { key: '/hr/structure', icon: <ClusterOutlined />, label: 'Departments' },
    ].filter(Boolean) as MenuProps['items']
    const payroll: MenuProps['items'] = [
      can(P.payrollView) && { key: '/payroll', icon: <WalletOutlined />, label: 'Payroll runs' },
      (can(P.payrollSettings) || can(P.hrSettings)) && { key: '/payroll/settings', icon: <SettingOutlined />, label: 'HR & payroll settings' },
    ].filter(Boolean) as MenuProps['items']

    const has = (prefix: string) => [...held].some(p => p.startsWith(prefix))
    const finance: MenuProps['items'] = [
      can('finance.reports.view') && { key: '/finance', icon: <FundOutlined />, label: 'Overview' },
      has('finance.invoices.') && { key: '/finance/invoices', icon: <FileTextOutlined />, label: 'Sales invoices' },
      has('finance.bills.') && { key: '/finance/bills', icon: <FileDoneOutlined />, label: 'Purchase bills' },
      can('finance.payments.view') && { key: '/finance/payments', icon: <SwapOutlined />, label: 'Payments' },
      has('finance.contacts.') && { key: '/finance/contacts', icon: <ContactsOutlined />, label: 'Customers & vendors' },
      can('finance.journals.view') && { key: '/finance/journals', icon: <BookOutlined />, label: 'Journal entries' },
      can('finance.reports.view') && { key: '/finance/reports', icon: <BarChartOutlined />, label: 'Reports' },
      (can('finance.settings.manage') || can('finance.accounts.view')) && { key: '/finance/settings', icon: <SettingOutlined />, label: 'Accounting setup' },
    ].filter(Boolean) as MenuProps['items']

    const inventory: MenuProps['items'] = [
      can('inventory.stock.view') && { key: '/inventory/stock', icon: <DatabaseOutlined />, label: 'Stock' },
      (can('inventory.stock.issue') || can('inventory.stock.transfer') || can('inventory.stock.adjust') || can('inventory.stock.view')) && { key: '/inventory/transactions', icon: <RetweetOutlined />, label: 'Stock transactions' },
      (has('inventory.items.') || has('inventory.warehouses.')) && { key: '/inventory/setup', icon: <AppstoreOutlined />, label: 'Items & warehouses' },
    ].filter(Boolean) as MenuProps['items']
    const procurement: MenuProps['items'] = [
      has('procurement.purchase_requests.') && { key: '/procurement/requests', icon: <SolutionOutlined />, label: 'Purchase requests' },
      (has('procurement.purchase_orders.') || has('procurement.goods_receipts.')) && { key: '/procurement/orders', icon: <ShoppingOutlined />, label: 'Purchase orders & receipts' },
    ].filter(Boolean) as MenuProps['items']

    const hotel: MenuProps['items'] = [
      can('hotel.reservations.view') && { key: '/hotel/front-desk', icon: <BellOutlined />, label: 'Front desk' },
      can('hotel.reservations.view') && { key: '/hotel/reservations', icon: <CalendarTwoTone twoToneColor="#8c8c8c" />, label: 'Reservations' },
      can('hotel.housekeeping.view') && { key: '/hotel/housekeeping', icon: <ClearOutlined />, label: 'Housekeeping' },
      can('hotel.rooms.view') && { key: '/hotel/setup', icon: <KeyOutlined />, label: 'Rooms & rates' },
      can('hotel.reports.view') && { key: '/hotel/reports', icon: <LineChartOutlined />, label: 'Hotel performance' },
    ].filter(Boolean) as MenuProps['items']

    const travel: MenuProps['items'] = [
      (can('travel.bookings.view') || can('tourism.departures.view')) && { key: '/travel', icon: <GlobalOutlined />, label: 'Overview' },
      can('travel.bookings.view') && { key: '/travel/bookings', icon: <CarryOutOutlined />, label: 'Bookings' },
      (can('tourism.departures.view') || can('travel.bookings.create')) && { key: '/tours/departures', icon: <FlagOutlined />, label: 'Departures' },
      (has('tourism.packages.') || has('tourism.guides.')) && { key: '/tours/packages', icon: <ReadOutlined />, label: 'Packages & guides' },
    ].filter(Boolean) as MenuProps['items']

    const logistics: MenuProps['items'] = [
      (can('logistics.shipments.view') || can('logistics.fleet.view')) && { key: '/logistics', icon: <CarOutlined />, label: 'Overview' },
      can('logistics.shipments.view') && { key: '/logistics/consignments', icon: <InboxOutlined />, label: 'Consignments' },
      can('logistics.shipments.view') && { key: '/logistics/trips', icon: <SwapOutlined />, label: 'Trips & load sheets' },
      (has('logistics.fleet.') || has('logistics.drivers.') || has('logistics.routes.')) && { key: '/logistics/fleet', icon: <ClusterOutlined />, label: 'Fleet & routes' },
      (can('logistics.shipments.edit') || can('logistics.cod.remit')) && { key: '/logistics/billing', icon: <DollarOutlined />, label: 'COD & billing' },
    ].filter(Boolean) as MenuProps['items']

    const ngo: MenuProps['items'] = [
      has('ngo.') && { key: '/ngo', icon: <HeartOutlined />, label: 'Overview' },
      can('ngo.grants.view') && { key: '/ngo/grants', icon: <FileDoneOutlined />, label: 'Grants' },
      (can('ngo.donations.view') || can('ngo.donors.view')) && { key: '/ngo/donations', icon: <DollarOutlined />, label: 'Donors & donations' },
      can('ngo.beneficiaries.view') && { key: '/ngo/beneficiaries', icon: <TeamOutlined />, label: 'Beneficiaries' },
      (can('ngo.funds.view') || can('ngo.programs.view')) && { key: '/ngo/funds', icon: <BankOutlined />, label: 'Funds & programs' },
      can('ngo.reports.view') && { key: '/ngo/reports', icon: <BarChartOutlined />, label: 'NGO reports' },
    ].filter(Boolean) as MenuProps['items']

    const projects: MenuProps['items'] = [
      can('projects.projects.view') && { key: '/projects', icon: <ProjectOutlined />, label: 'Overview' },
      can('projects.projects.view') && { key: '/projects/list', icon: <AppstoreOutlined />, label: 'Projects & boards' },
      can('projects.timesheets.create') && { key: '/timesheet', icon: <ScheduleOutlined />, label: 'My timesheet' },
      can('projects.timesheets.approve') && { key: '/projects/approvals', icon: <AuditOutlined />, label: 'Time approvals' },
      can('projects.clients.view') && { key: '/projects/clients', icon: <ContactsOutlined />, label: 'Clients' },
      can('projects.reports.view') && { key: '/projects/utilization', icon: <LineChartOutlined />, label: 'Utilization' },
    ].filter(Boolean) as MenuProps['items']

    const modules = BUSINESS_MODULES.filter(m => hasModule(m.code) && !['hr', 'payroll', 'finance', 'inventory', 'procurement', 'hotel', 'travel', 'tourism', 'logistics', 'ngo', 'projects'].includes(m.code))
      .map(m => ({ key: `/m/${m.code}`, icon: m.icon, label: m.label }))

    return [
      { key: '/', icon: <DashboardOutlined />, label: 'Dashboard' },
      { key: '/me', icon: <SmileOutlined />, label: 'My Workspace' },
      ...(hotel?.length ? [{ type: 'group' as const, label: 'Hotel', children: hotel }] : []),
      ...(travel?.length ? [{ type: 'group' as const, label: 'Travel & Tours', children: travel }] : []),
      ...(logistics?.length ? [{ type: 'group' as const, label: 'Logistics', children: logistics }] : []),
      ...(ngo?.length ? [{ type: 'group' as const, label: 'NGO', children: ngo }] : []),
      ...(projects?.length ? [{ type: 'group' as const, label: 'Projects', children: projects }] : []),
      ...((hasModule('hr') || pending > 0) && hr?.length ? [{ type: 'group' as const, label: 'Human Resources', children: hr }] : []),
      ...(payroll?.length ? [{ type: 'group' as const, label: 'Payroll', children: payroll }] : []),
      ...(finance?.length ? [{ type: 'group' as const, label: 'Finance', children: finance }] : []),
      ...(inventory?.length ? [{ type: 'group' as const, label: 'Inventory', children: inventory }] : []),
      ...(procurement?.length ? [{ type: 'group' as const, label: 'Procurement', children: procurement }] : []),
      ...(modules.length ? [{ type: 'group' as const, label: 'Modules', children: modules }] : []),
      ...(admin?.length ? [{ type: 'group' as const, label: 'Administration', children: admin }] : []),
    ]
  }, [me, can, isPlatformAdmin, pending])

  const userMenu: MenuProps = {
    items: [
      { key: 'profile', icon: <UserOutlined />, label: 'My profile' },
      { type: 'divider' },
      { key: 'logout', icon: <LogoutOutlined />, label: 'Sign out', danger: true },
    ],
    onClick: ({ key }) => (key === 'logout' ? void logout().then(() => navigate('/login')) : navigate('/profile')),
  }

  const parts = location.pathname.split('/').filter(Boolean)
  const selected = parts.length === 0 ? '/' : parts[0] === 'payroll' && parts[1] !== 'settings' ? '/payroll' : '/' + parts.slice(0, 2).join('/')

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        breakpoint="lg"
        collapsedWidth={screens.md ? 80 : 0}
        // On phones the floating trigger would cover the page title; the header gets a menu button instead.
        trigger={screens.md ? undefined : null}
        width={240}
        theme="dark"
      >
        <div style={{ height: 56, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontWeight: 700, fontSize: collapsed ? 16 : 20, letterSpacing: 1 }}>
          {collapsed ? 'E' : 'ERPOS'}
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={[selected]} items={items} onClick={e => { navigate(e.key); if (!screens.md) setCollapsed(true) }} />
      </Sider>
      <Layout>
        <Header style={{ background: token.colorBgContainer, padding: '0 16px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: `1px solid ${token.colorBorderSecondary}` }}>
          {!screens.md && <Button type="text" icon={<MenuOutlined />} onClick={() => setCollapsed(c => !c)} aria-label="Menu" />}
          <Typography.Text strong ellipsis style={{ flex: 1, marginLeft: screens.md ? 0 : 4 }}>
            {isPlatformAdmin ? 'Platform administration' : me?.tenant?.name}
          </Typography.Text>
          <Dropdown menu={userMenu} trigger={['click']}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              {screens.sm && <Tag color="blue">{me?.user.userType}</Tag>}
              {screens.sm && <span>{me?.user.fullName}</span>}
              <Avatar style={{ background: token.colorPrimary }}>{me?.user.fullName?.[0]?.toUpperCase()}</Avatar>
            </div>
          </Dropdown>
        </Header>
        <Content style={{ padding: screens.md ? 24 : 16 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
