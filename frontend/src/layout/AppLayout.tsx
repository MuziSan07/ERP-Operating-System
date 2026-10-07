import { Suspense, useMemo, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Avatar, Badge, Button, Dropdown, Grid, Layout, Menu, Spin, Tag, Typography, theme, type MenuProps } from 'antd'
import {
  ApartmentOutlined, AuditOutlined, MailOutlined, BankOutlined, CarOutlined, DashboardOutlined, DollarOutlined, GlobalOutlined,
  HeartOutlined, HomeOutlined, InboxOutlined, LogoutOutlined, ProjectOutlined, SafetyOutlined, ShoppingCartOutlined,
  TeamOutlined, UserOutlined, WalletOutlined, CompassOutlined, SmileOutlined, IdcardOutlined, CalendarOutlined,
  ScheduleOutlined, ClusterOutlined, SettingOutlined, MenuOutlined, FileTextOutlined, FileDoneOutlined,
  SwapOutlined, BookOutlined, ContactsOutlined, BarChartOutlined, FundOutlined, DatabaseOutlined, RetweetOutlined, AppstoreOutlined,
  SolutionOutlined, ShoppingOutlined, CarryOutOutlined, FlagOutlined, ReadOutlined, BellOutlined, CalendarTwoTone, ClearOutlined, KeyOutlined, LineChartOutlined,
} from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { canOpen } from '../auth/routeAccess'
import ErrorBoundary from '../components/ErrorBoundary'
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
  const { me, logout, isPlatformAdmin } = useAuth()
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
    const open = (path: string) => canOpen(path, held) // same rules as the router (auth/routeAccess.ts)
    const hasModule = (m: string) => [...held].some(p => p.startsWith(m + '.'))

    const admin: MenuProps['items'] = [
      open('/entities') && { key: '/entities', icon: <ApartmentOutlined />, label: 'Entities' },
      open('/users') && { key: '/users', icon: <UserOutlined />, label: 'Users' },
      open('/roles') && { key: '/roles', icon: <SafetyOutlined />, label: 'Roles & Permissions' },
      open('/audit') && { key: '/audit', icon: <AuditOutlined />, label: 'Audit Log' },
      open('/admin/email') && { key: '/admin/email', icon: <MailOutlined />, label: 'Email & Reminders' },
    ].filter(Boolean) as MenuProps['items']

    // HR and Payroll have real screens; other modules still show the placeholder.
    const hr: MenuProps['items'] = [
      open('/hr/employees') && { key: '/hr/employees', icon: <IdcardOutlined />, label: 'Employees' },
      open('/hr/attendance') && { key: '/hr/attendance', icon: <CalendarOutlined />, label: 'Attendance' },
      { key: '/hr/leave', icon: <ScheduleOutlined />, label: <Badge count={pending} size="small" offset={[10, 0]}>Leave</Badge> },
      open('/hr/structure') && { key: '/hr/structure', icon: <ClusterOutlined />, label: 'Departments' },
    ].filter(Boolean) as MenuProps['items']
    const payroll: MenuProps['items'] = [
      open('/payroll') && { key: '/payroll', icon: <WalletOutlined />, label: 'Payroll runs' },
      open('/payroll/settings') && { key: '/payroll/settings', icon: <SettingOutlined />, label: 'HR & payroll settings' },
    ].filter(Boolean) as MenuProps['items']

    const finance: MenuProps['items'] = [
      open('/finance') && { key: '/finance', icon: <FundOutlined />, label: 'Overview' },
      open('/finance/invoices') && { key: '/finance/invoices', icon: <FileTextOutlined />, label: 'Sales invoices' },
      open('/finance/bills') && { key: '/finance/bills', icon: <FileDoneOutlined />, label: 'Purchase bills' },
      open('/finance/payments') && { key: '/finance/payments', icon: <SwapOutlined />, label: 'Payments' },
      open('/finance/contacts') && { key: '/finance/contacts', icon: <ContactsOutlined />, label: 'Customers & vendors' },
      open('/finance/journals') && { key: '/finance/journals', icon: <BookOutlined />, label: 'Journal entries' },
      open('/finance/reports') && { key: '/finance/reports', icon: <BarChartOutlined />, label: 'Reports' },
      open('/finance/assets') && { key: '/finance/assets', icon: <DatabaseOutlined />, label: 'Fixed assets' },
      open('/finance/withholding') && { key: '/finance/withholding', icon: <SafetyOutlined />, label: 'Withholding tax' },
      open('/finance/reconciliation') && { key: '/finance/reconciliation', icon: <BankOutlined />, label: 'Bank reconciliation' },
      open('/finance/settings') && { key: '/finance/settings', icon: <SettingOutlined />, label: 'Accounting setup' },
    ].filter(Boolean) as MenuProps['items']

    const inventory: MenuProps['items'] = [
      open('/inventory/stock') && { key: '/inventory/stock', icon: <DatabaseOutlined />, label: 'Stock' },
      open('/inventory/transactions') && { key: '/inventory/transactions', icon: <RetweetOutlined />, label: 'Stock transactions' },
      open('/inventory/setup') && { key: '/inventory/setup', icon: <AppstoreOutlined />, label: 'Items & warehouses' },
    ].filter(Boolean) as MenuProps['items']
    const procurement: MenuProps['items'] = [
      open('/procurement/requests') && { key: '/procurement/requests', icon: <SolutionOutlined />, label: 'Purchase requests' },
      open('/procurement/orders') && { key: '/procurement/orders', icon: <ShoppingOutlined />, label: 'Purchase orders & receipts' },
    ].filter(Boolean) as MenuProps['items']

    const hotel: MenuProps['items'] = [
      open('/hotel/front-desk') && { key: '/hotel/front-desk', icon: <BellOutlined />, label: 'Front desk' },
      open('/hotel/reservations') && { key: '/hotel/reservations', icon: <CalendarTwoTone twoToneColor="#8c8c8c" />, label: 'Reservations' },
      open('/hotel/housekeeping') && { key: '/hotel/housekeeping', icon: <ClearOutlined />, label: 'Housekeeping' },
      open('/hotel/setup') && { key: '/hotel/setup', icon: <KeyOutlined />, label: 'Rooms & rates' },
      open('/hotel/reports') && { key: '/hotel/reports', icon: <LineChartOutlined />, label: 'Hotel performance' },
    ].filter(Boolean) as MenuProps['items']

    const travel: MenuProps['items'] = [
      open('/travel') && { key: '/travel', icon: <GlobalOutlined />, label: 'Overview' },
      open('/travel/bookings') && { key: '/travel/bookings', icon: <CarryOutOutlined />, label: 'Bookings' },
      open('/tours/departures') && { key: '/tours/departures', icon: <FlagOutlined />, label: 'Departures' },
      open('/tours/packages') && { key: '/tours/packages', icon: <ReadOutlined />, label: 'Packages & guides' },
    ].filter(Boolean) as MenuProps['items']

    const logistics: MenuProps['items'] = [
      open('/logistics') && { key: '/logistics', icon: <CarOutlined />, label: 'Overview' },
      open('/logistics/consignments') && { key: '/logistics/consignments', icon: <InboxOutlined />, label: 'Consignments' },
      open('/logistics/trips') && { key: '/logistics/trips', icon: <SwapOutlined />, label: 'Trips & load sheets' },
      open('/logistics/fleet') && { key: '/logistics/fleet', icon: <ClusterOutlined />, label: 'Fleet & routes' },
      open('/logistics/billing') && { key: '/logistics/billing', icon: <DollarOutlined />, label: 'COD & billing' },
    ].filter(Boolean) as MenuProps['items']

    const ngo: MenuProps['items'] = [
      open('/ngo') && { key: '/ngo', icon: <HeartOutlined />, label: 'Overview' },
      open('/ngo/grants') && { key: '/ngo/grants', icon: <FileDoneOutlined />, label: 'Grants' },
      open('/ngo/donations') && { key: '/ngo/donations', icon: <DollarOutlined />, label: 'Donors & donations' },
      open('/ngo/beneficiaries') && { key: '/ngo/beneficiaries', icon: <TeamOutlined />, label: 'Beneficiaries' },
      open('/ngo/funds') && { key: '/ngo/funds', icon: <BankOutlined />, label: 'Funds & programs' },
      open('/ngo/reports') && { key: '/ngo/reports', icon: <BarChartOutlined />, label: 'NGO reports' },
    ].filter(Boolean) as MenuProps['items']

    const projects: MenuProps['items'] = [
      open('/projects') && { key: '/projects', icon: <ProjectOutlined />, label: 'Overview' },
      open('/projects/list') && { key: '/projects/list', icon: <AppstoreOutlined />, label: 'Projects & boards' },
      open('/timesheet') && { key: '/timesheet', icon: <ScheduleOutlined />, label: 'My timesheet' },
      open('/projects/approvals') && { key: '/projects/approvals', icon: <AuditOutlined />, label: 'Time approvals' },
      open('/projects/clients') && { key: '/projects/clients', icon: <ContactsOutlined />, label: 'Clients' },
      open('/projects/utilization') && { key: '/projects/utilization', icon: <LineChartOutlined />, label: 'Utilization' },
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
  }, [me, isPlatformAdmin, pending])

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
          {/* Pages load on first visit; keep the menu and header while they do. */}
          <ErrorBoundary key={location.pathname}>
            <Suspense fallback={<Spin style={{ display: 'block', margin: '80px auto' }} />}>
              <Outlet />
            </Suspense>
          </ErrorBoundary>
        </Content>
      </Layout>
    </Layout>
  )
}
