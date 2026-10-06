import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Popconfirm, Progress, Result, Row,
  Segmented, Select, Space, Statistic, Switch, Table, Tabs, Tag, Typography,
} from 'antd'
import { CheckOutlined, DeleteOutlined, DollarOutlined, EditOutlined, PlusOutlined, PrinterOutlined, StopOutlined, WarningOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  BUDGET_CATEGORIES, DONOR_TYPES, FUND_COLORS, GRANT_COLORS, words, type Beneficiary, type BeneficiaryListItem, type BudgetCategory, type Donation,
  type DonationListItem, type Donor, type DonorSummaryRow, type Fund, type FundExpense, type FunctionalExpenses, type Grant, type GrantListItem,
  type GrantStatus, type NgoDashboard, type NgoProgram,
} from '../../api/ngo'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect, useFinanceSettings } from '../../components/FinancePickers'

const useFunds = () => useQuery({ queryKey: ['ngo-funds'], queryFn: async () => (await api.get<Fund[]>('/ngo/funds')).data })
const useDonors = () => useQuery({ queryKey: ['ngo-donors'], queryFn: async () => (await api.get<Donor[]>('/ngo/donors')).data })
const usePrograms = () => useQuery({ queryKey: ['ngo-programs'], queryFn: async () => (await api.get<NgoProgram[]>('/ngo/programs')).data })
const useActiveGrants = () => useQuery({ queryKey: ['ngo-grants', 'Active'], queryFn: async () => (await api.get<GrantListItem[]>('/ngo/grants', { params: { status: 'Active' } })).data })
const d8 = (d?: Dayjs | null) => d?.format('YYYY-MM-DD')
const dj = (s?: string) => (s ? dayjs(s) : undefined)
const refresh = (qc: ReturnType<typeof useQueryClient>) => Promise.all(['ngo-dash', 'ngo-grants', 'ngo-grant', 'ngo-funds', 'ngo-donations', 'ngo-donors', 'ngo-expenses',
  'ngo-bens', 'ngo-ben', 'ngo-programs'].map(k => qc.invalidateQueries({ queryKey: [k] })))

const FundTag = ({ f }: { f: Pick<Fund, 'code' | 'kind'> }) => <Tag color={FUND_COLORS[f.kind]}>{f.code}</Tag>
const FundSelect = ({ value, onChange, spendable, allowClear }: { value?: string; onChange?: (v: string) => void; spendable?: boolean; allowClear?: boolean }) => {
  const { data = [] } = useFunds()
  return <Select value={value} onChange={onChange} allowClear={allowClear} placeholder="Fund" showSearch={{ optionFilterProp: 'label' }} style={{ width: '100%' }}
    options={data.filter(f => f.isActive && !f.grantId && (!spendable || f.kind !== 'Endowment'))
      .map(f => ({ value: f.id, label: `${f.code} · ${f.name} (${f.kind}${spendable && f.kind !== 'Unrestricted' ? `, ${f.balance.toLocaleString()} left` : ''})` }))} />
}
const DonorSelect = ({ value, onChange }: { value?: string; onChange?: (v: string) => void }) => {
  const { data = [] } = useDonors()
  return <Select value={value} onChange={onChange} showSearch={{ optionFilterProp: 'label' }} placeholder="Donor" style={{ width: '100%' }}
    options={data.map(d => ({ value: d.id, label: `${d.name} (${d.code})` }))} />
}
const ProgramSelect = ({ value, onChange }: { value?: string; onChange?: (v?: string) => void }) => {
  const { data = [] } = usePrograms()
  return <Select value={value} onChange={onChange} allowClear placeholder="Program" style={{ width: '100%' }} options={data.filter(p => p.isActive).map(p => ({ value: p.id, label: `${p.code} · ${p.name}` }))} />
}
const Burn = ({ burn, time }: { burn: number; time: number }) => (
  <div title={`${burn}% spent · ${time}% of time elapsed`}>
    <Progress percent={Math.min(100, burn)} success={{ percent: 0 }} size="small" status={burn > 100 ? 'exception' : 'normal'} format={() => `${burn}%`} />
    <Typography.Text type="secondary" style={{ fontSize: 12 }}>{time}% of time</Typography.Text>
  </div>
)

// ======================= Dashboard =======================

export function NgoDashboardPage() {
  const [open, setOpen] = useState<string | null>(null)
  const { data, isLoading, error } = useQuery({ queryKey: ['ngo-dash'], retry: false, queryFn: async () => (await api.get<NgoDashboard>('/ngo/dashboard')).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>NGO overview</Typography.Title></div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Donations this month', amount(data?.donationsThisMonth, 0), `${data?.donorsThisMonth ?? 0} donors`], ['Active grants', data?.activeGrants, `${amount(data?.committedBase, 0)} committed`],
          ['Grant money received', amount(data?.receivedBase, 0), `${amount(data?.spentBase, 0)} spent`], ['Beneficiaries', data?.activeBeneficiaries, `${data?.assistedThisMonth ?? 0} helped this month`],
          ['Program spending ratio', `${data?.programRatio ?? 0}%`, 'this financial year']].map(([t, v, s]) => (
          <Col key={t as string} xs={12} md={8} xl={4}><Card loading={isLoading} size="small"><Statistic title={t as string} value={v as string} />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>{s as string}</Typography.Text></Card></Col>
        ))}
      </Row>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title="Active grants — spending against time" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.grants} locale={{ emptyText: 'No active grants' }} scroll={{ x: 600 }}
              onRow={g => ({ onClick: () => setOpen(g.id), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Grant', render: (_, g) => <><b>{g.title}</b><div><Typography.Text type="secondary">{g.number} · {g.donorName}</Typography.Text></div></> },
                { title: 'Budget', align: 'right', render: (_, g) => `${g.currency} ${g.amount.toLocaleString()}` },
                { title: 'Spent', width: 170, render: (_, g) => <Burn burn={g.burnPercent} time={g.timeElapsedPercent} /> },
                { title: 'Ends', dataIndex: 'endDate', render: fmtDate }]} />
          </Card>
        </Col>
        <Col xs={24} xl={10}>
          <Card title={<Space><WarningOutlined style={{ color: '#fa8c16' }} />Due in the next 30 days</Space>} loading={isLoading} style={{ marginBottom: 16 }}>
            <Table size="small" rowKey={d => `${d.kind}-${d.grantId}-${d.dueDate}-${d.title}`} pagination={false} dataSource={data?.dueSoon} locale={{ emptyText: 'Nothing due' }}
              onRow={d => ({ onClick: () => setOpen(d.grantId), style: { cursor: 'pointer' } })}
              columns={[{ title: 'What', render: (_, d) => <><Tag>{d.kind}</Tag>{d.title}</> },
                { title: 'Due', render: (_, d) => <Tag color={d.overdue ? 'red' : 'orange'}>{fmtDate(d.dueDate)}{d.overdue ? ' — overdue' : ''}</Tag> }]} />
          </Card>
          <Card title="Fund balances" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.funds}
              columns={[{ title: 'Fund', render: (_, f) => <><FundTag f={f} />{f.name}</> }, { title: 'Balance', align: 'right', render: (_, f) => amount(f.balance, 0) }]} />
          </Card>
        </Col>
      </Row>
      {open && <GrantDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

// ======================= Grants =======================

export function GrantsPage() {
  const { can } = useAuth()
  const [status, setStatus] = useState<GrantStatus | 'All'>('Active')
  const [open, setOpen] = useState<string | null>(null)
  const [editing, setEditing] = useState<Grant | 'new' | null>(null)
  const { data, isFetching } = useQuery({ queryKey: ['ngo-grants', status], queryFn: async () => (await api.get<GrantListItem[]>('/ngo/grants', { params: { status: status === 'All' ? undefined : status } })).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Grants</Typography.Title>
        {can('ngo.grants.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New grant</Button>}
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={status} onChange={v => setStatus(v as typeof status)} options={['Active', 'Proposal', 'Closed', 'Cancelled', 'All']} />
        <Table<GrantListItem> rowKey="id" loading={isFetching} dataSource={data} scroll={{ x: 1000 }} onRow={g => ({ onClick: () => setOpen(g.id), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Grant', render: (_, g) => <><b>{g.title}</b><div><Typography.Text type="secondary">{g.number}</Typography.Text></div></> },
            { title: 'Donor', dataIndex: 'donorName' },
            { title: 'Period', render: (_, g) => `${fmtDate(g.startDate)} – ${fmtDate(g.endDate)}` },
            { title: 'Budget', align: 'right', render: (_, g) => `${g.currency} ${g.amount.toLocaleString()}` },
            { title: 'Received', align: 'right', render: (_, g) => amount(g.receivedBase, 0) },
            { title: 'Spent', width: 160, render: (_, g) => <Burn burn={g.burnPercent} time={g.timeElapsedPercent} /> },
            { title: 'Next report', render: (_, g) => g.nextReportDue ? <Tag color={dayjs(g.nextReportDue).isBefore(dayjs(), 'day') ? 'red' : undefined}>{fmtDate(g.nextReportDue)}</Tag> : '—' },
            { title: 'Status', dataIndex: 'status', render: (s: GrantStatus) => <Tag color={GRANT_COLORS[s]}>{s}</Tag> },
          ]} />
      </Card>
      {editing && <GrantEditor grant={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setOpen} />}
      {open && <GrantDrawer id={open} onClose={() => setOpen(null)} onEdit={g => { setOpen(null); setEditing(g) }} />}
    </>
  )
}

interface LineDraft { id?: string; code: string; description: string; category: BudgetCategory; amount: number; expenseAccountId?: string }
interface TrancheDraft { id?: string; dueDate?: Dayjs | null; amount: number; condition?: string; received?: boolean }

function GrantEditor({ grant, onClose, onSaved }: { grant?: Grant; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: settings } = useFinanceSettings()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const currency = Form.useWatch('currency', form) ?? grant?.currency
  const [lines, setLines] = useState<LineDraft[]>(grant?.budgetLines.map(l => ({ ...l })) ?? [{ code: 'PER', description: '', category: 'Personnel', amount: 0 }])
  const [tranches, setTranches] = useState<TrancheDraft[]>(grant?.tranches.map(t => ({ ...t, dueDate: dayjs(t.dueDate), received: !!t.receivedDate })) ?? [{ amount: 0 }])
  const setL = (i: number, p: Partial<LineDraft>) => setLines(xs => xs.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const setT = (i: number, p: Partial<TrancheDraft>) => setTranches(xs => xs.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const budget = lines.reduce((s, l) => s + (l.amount || 0), 0)
  const trancheTotal = tranches.reduce((s, t) => s + (t.amount || 0), 0)
  const foreign = !!currency && currency !== (settings?.baseCurrency ?? 'PKR')

  const save = async () => {
    const v = await form.validateFields()
    if (tranches.some(t => !t.dueDate)) { message.error('Every tranche needs a due date.'); return }
    setBusy(true)
    try {
      const body = { ...v, startDate: d8(v.startDate), endDate: d8(v.endDate), budgetLines: lines, tranches: tranches.map(t => ({ id: t.id, dueDate: d8(t.dueDate), amount: t.amount, condition: t.condition })) }
      const res = grant ? await api.put<Grant>(`/ngo/grants/${grant.id}`, body) : await api.post<Grant>('/ngo/grants', body)
      message.success(grant ? 'Grant saved' : `Grant ${res.data.number} drafted`)
      await refresh(qc)
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  const active = grant?.status === 'Active'
  return (
    <Drawer open onClose={onClose} size={1000} title={grant ? `Edit ${grant.number}` : 'New grant'} extra={<Button type="primary" loading={busy} onClick={save}>Save</Button>}>
      {active && <Alert type="info" showIcon style={{ marginBottom: 16 }} title="Re-budgeting an active grant: lines can't drop below what's been spent, and received tranches are locked. Keep the donor's approval on file." />}
      <Form form={form} layout="vertical" initialValues={grant ? { ...grant, startDate: dj(grant.startDate), endDate: dj(grant.endDate) }
        : { entityId: me?.entities.find(e => e.permissions.includes('ngo.grants.create'))?.id, reportingFrequency: 'Quarterly', flexibilityPercent: 10, currency: settings?.baseCurrency ?? 'PKR' }}>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="title" label="Project title" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={6}><Form.Item name="donorId" label="Donor" rules={[{ required: true }]}><DonorSelect /></Form.Item></Col>
          <Col xs={24} md={6}><Form.Item name="agreementRef" label="Agreement no."><Input /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="startDate" label="Start" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" disabled={active} /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="endDate" label="End" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={8} md={3}><Form.Item name="currency" label="Currency"><Input maxLength={3} disabled={active} style={{ textTransform: 'uppercase' }} /></Form.Item></Col>
          {foreign && <Col xs={16} md={4}><Form.Item name="agreementRate" label={`Rate (${settings?.baseCurrency ?? 'PKR'} per ${currency})`} rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          <Col xs={12} md={4}><Form.Item name="reportingFrequency" label="Donor reporting"><Select options={['Monthly', 'Quarterly', 'SemiAnnual', 'Annual', 'EndOnly'].map(f => ({ value: f, label: words(f) }))} /></Form.Item></Col>
          <Col xs={12} md={foreign ? 5 : 9}><Form.Item name="flexibilityPercent" label="Line flexibility %" tooltip="How far a budget line may overspend without donor approval"><InputNumber min={0} max={100} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="programId" label="Program"><ProgramSelect /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="entityId" label="Implementing entity" rules={[{ required: true }]}><EntityPicker permission="ngo.grants.create" /></Form.Item></Col>
        </Row>
      </Form>
      <Typography.Title level={5}>Budget ({currency}) — total {budget.toLocaleString()}</Typography.Title>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines} scroll={{ x: 900 }}
        columns={[
          { title: 'Code', width: 90, render: (_, l, i) => <Input value={l.code} onChange={e => setL(i, { code: e.target.value })} /> },
          { title: 'Description', render: (_, l, i) => <Input value={l.description} onChange={e => setL(i, { description: e.target.value })} /> },
          { title: 'Category', width: 140, render: (_, l, i) => <Select value={l.category} onChange={v => setL(i, { category: v })} style={{ width: '100%' }} options={BUDGET_CATEGORIES.map(c => ({ value: c, label: c }))} /> },
          { title: 'Default expense account', width: 230, render: (_, l, i) => <AccountSelect types={['Expense']} value={l.expenseAccountId} onChange={v => setL(i, { expenseAccountId: v })} allowClear /> },
          { title: 'Amount', width: 130, render: (_, l, i) => <InputNumber min={0} value={l.amount} onChange={v => setL(i, { amount: v ?? 0 })} style={{ width: '100%' }} /> },
          { key: 'x', width: 50, render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length === 1} onClick={() => setLines(xs => xs.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(xs => [...xs, { code: '', description: '', category: 'Activities', amount: 0 }])}>Add budget line</Button>

      <Typography.Title level={5} style={{ marginTop: 24 }}>Tranches — total {trancheTotal.toLocaleString()}
        {trancheTotal !== budget && <Tag color="orange" style={{ marginLeft: 8 }}>Must equal the budget before approval</Tag>}</Typography.Title>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={tranches}
        columns={[
          { title: '#', width: 40, render: (_, __, i) => i + 1 },
          { title: 'Due', width: 160, render: (_, t, i) => <DatePicker value={t.dueDate} disabled={t.received} onChange={d => setT(i, { dueDate: d })} format="DD MMM YYYY" /> },
          { title: 'Amount', width: 150, render: (_, t, i) => <InputNumber min={0} value={t.amount} disabled={t.received} onChange={v => setT(i, { amount: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Condition', render: (_, t, i) => <Input value={t.condition} placeholder="e.g. on approval of the first progress report" onChange={e => setT(i, { condition: e.target.value })} /> },
          { key: 'x', width: 50, render: (_, t, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={t.received || tranches.length === 1} onClick={() => setTranches(xs => xs.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setTranches(xs => [...xs, { amount: Math.max(0, budget - trancheTotal) }])}>Add tranche</Button>
    </Drawer>
  )
}

function GrantDrawer({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit?: (g: Grant) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [receive, setReceive] = useState<string | null>(null)
  const [spend, setSpend] = useState(false)
  const [closing, setClosing] = useState(false)
  const { data: g, isLoading } = useQuery({ queryKey: ['ngo-grant', id], queryFn: async () => (await api.get<Grant>(`/ngo/grants/${id}`)).data })
  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); message.success(ok); await refresh(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const edit = can('ngo.grants.edit')
  const fx = g && g.averageRate !== 1
  return (
    <Drawer open onClose={onClose} size={1000} loading={isLoading} title={g && <Space>{g.number}<Tag color={GRANT_COLORS[g.status]}>{g.status}</Tag></Space>}
      extra={g && <Space wrap>
        {edit && ['Proposal', 'Active'].includes(g.status) && onEdit && <Button icon={<EditOutlined />} onClick={() => onEdit(g)}>{g.status === 'Active' ? 'Re-budget' : 'Edit'}</Button>}
        {g.status === 'Proposal' && can('ngo.grants.approve') && <Button type="primary" icon={<CheckOutlined />} onClick={() => act(() => api.post(`/ngo/grants/${id}/activate`), 'Grant approved — reporting schedule created')}>Approve</Button>}
        {g.status === 'Proposal' && edit && <Popconfirm title="Cancel this proposal?" onConfirm={() => act(() => api.post(`/ngo/grants/${id}/cancel`), 'Cancelled')}><Button danger icon={<StopOutlined />}>Cancel</Button></Popconfirm>}
        {g.status === 'Active' && can('ngo.expenses.create') && <Button icon={<DollarOutlined />} onClick={() => setSpend(true)}>Charge cost</Button>}
        {g.status === 'Active' && can('ngo.grants.approve') && <Button onClick={() => setClosing(true)}>Close grant</Button>}
      </Space>}>
      {g && <>
        <Typography.Title level={4} style={{ marginTop: 0 }}>{g.title}</Typography.Title>
        {g.warnings.map(w => <Alert key={w} type="warning" showIcon title={w} style={{ marginBottom: 8 }} />)}
        <Row gutter={[16, 16]} style={{ margin: '16px 0' }}>
          <Col xs={12} md={6}><Statistic title={`Budget (${g.currency})`} value={g.amount.toLocaleString()} /></Col>
          <Col xs={12} md={6}><Statistic title={`Received (${g.currency})`} value={g.receivedAmount.toLocaleString()} /></Col>
          <Col xs={12} md={6}><Statistic title={`Spent (${g.currency})`} value={g.spent.toLocaleString()} /></Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Burn vs time</Typography.Text><Burn burn={g.burnPercent} time={g.timeElapsedPercent} /></Col>
        </Row>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
          { label: 'Donor', children: `${g.donorName}${g.agreementRef ? ` · ${g.agreementRef}` : ''}` }, { label: 'Period', children: `${fmtDate(g.startDate)} – ${fmtDate(g.endDate)}` },
          { label: 'Program', children: g.programName ?? '—' }, { label: 'Entity', children: g.entityName },
          { label: 'Restricted fund', children: g.fundCode }, { label: 'Unspent (held as deferred income)', children: amount(g.unspentBase, 0) },
          ...(fx ? [{ label: 'Exchange rate', children: `agreement ${g.agreementRate} · average received ${g.averageRate}`, span: 'filled' as const }] : []),
        ]} />

        <Typography.Title level={5} style={{ marginTop: 24 }}>Budget vs actual ({g.currency}) · flexibility {g.flexibilityPercent}%</Typography.Title>
        <Table size="small" rowKey="id" pagination={false} dataSource={g.budgetLines} scroll={{ x: 700 }}
          summary={() => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={2}><b>Total</b></Table.Summary.Cell>
            <Table.Summary.Cell index={2} align="right"><b>{g.amount.toLocaleString()}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={3} align="right"><b>{g.spent.toLocaleString()}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={4} align="right"><b>{(g.amount - g.spent).toLocaleString()}</b></Table.Summary.Cell><Table.Summary.Cell index={5} /></Table.Summary.Row>}
          columns={[{ title: 'Line', render: (_, l) => <><b>{l.code}</b> {l.description}</> }, { title: 'Category', dataIndex: 'category' },
            { title: 'Budget', align: 'right', render: (_, l) => l.amount.toLocaleString() },
            { title: 'Actual', align: 'right', render: (_, l) => <Typography.Text type={l.overBudget ? 'danger' : undefined}>{l.actual.toLocaleString()}</Typography.Text> },
            { title: 'Remaining', align: 'right', render: (_, l) => l.variance.toLocaleString() },
            { title: 'Used', width: 140, render: (_, l) => <Progress percent={Math.min(100, l.burnPercent)} size="small" status={l.overBudget ? 'exception' : 'normal'} format={() => `${l.burnPercent}%`} /> }]} />

        <Row gutter={16} style={{ marginTop: 24 }}>
          <Col xs={24} lg={12}>
            <Typography.Title level={5}>Tranches</Typography.Title>
            <Table size="small" rowKey="id" pagination={false} dataSource={g.tranches}
              columns={[{ title: '#', dataIndex: 'sequence' }, { title: 'Due', render: (_, t) => <>{fmtDate(t.dueDate)}{t.overdue && <Tag color="red" style={{ marginLeft: 4 }}>Overdue</Tag>}</> },
                { title: 'Amount', align: 'right', render: (_, t) => t.amount.toLocaleString() },
                { title: '', render: (_, t) => t.receivedDate ? <Tag color="green">Received {fmtDate(t.receivedDate)}{fx ? ` · ${amount(t.receivedBase, 0)}` : ''}</Tag>
                  : g.status === 'Active' && edit && <Button size="small" onClick={() => setReceive(t.id)}>Receive</Button> }]} />
          </Col>
          <Col xs={24} lg={12}>
            <Typography.Title level={5}>Donor reports</Typography.Title>
            <Table size="small" rowKey="id" pagination={false} dataSource={g.reports} locale={{ emptyText: g.status === 'Proposal' ? 'Scheduled on approval' : 'None' }}
              columns={[{ title: 'Report', render: (_, r) => <>{r.title}<div><Typography.Text type="secondary" style={{ fontSize: 12 }}>{fmtDate(r.periodStart)} – {fmtDate(r.periodEnd)}</Typography.Text></div></> },
                { title: 'Due', render: (_, r) => <Typography.Text type={r.overdue ? 'danger' : undefined}>{fmtDate(r.dueDate)}</Typography.Text> },
                { title: '', render: (_, r) => r.submittedOn ? <Tag color="green">Sent {fmtDate(r.submittedOn)}</Tag>
                  : edit && <Button size="small" onClick={() => act(() => api.post(`/ngo/grants/${id}/reports/${r.id}/submit`, {}), `${r.title} marked as submitted`)}>Mark sent</Button> }]} />
          </Col>
        </Row>
        <Typography.Title level={5} style={{ marginTop: 24 }}>Costs charged</Typography.Title>
        <ExpenseTable rows={g.expenses} />
      </>}
      {receive && g && <ReceiveModal grant={g} trancheId={receive} onClose={() => setReceive(null)} />}
      {spend && g && <ChargeModal grant={g} onClose={() => setSpend(false)} />}
      {closing && g && <CloseModal grant={g} onClose={() => setClosing(false)} />}
    </Drawer>
  )
}

const ExpenseTable = ({ rows, loading }: { rows?: FundExpense[]; loading?: boolean }) => (
  <Table size="small" rowKey="id" loading={loading} pagination={{ pageSize: 20, hideOnSinglePage: true }} dataSource={rows} locale={{ emptyText: 'Nothing charged yet' }} scroll={{ x: 800 }}
    columns={[{ title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'No.', dataIndex: 'number' },
      { title: 'Charged to', render: (_, e) => <>{e.grantNumber ?? e.fundCode}{e.budgetLineCode && <Tag style={{ marginLeft: 4 }}>{e.budgetLineCode}</Tag>}</> },
      { title: 'Description', dataIndex: 'description' }, { title: 'Function', dataIndex: 'function', render: words },
      { title: 'How', dataIndex: 'paidHow', render: (h: string) => <Tag color={h === 'Allocation' ? 'purple' : h === 'Vendor bill' ? 'blue' : undefined}>{h}</Tag> },
      { title: 'Amount', align: 'right', render: (_, e) => amount(e.amount, 0) }]} />
)

function ReceiveModal({ grant, trancheId, onClose }: { grant: Grant; trancheId: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const t = grant.tranches.find(x => x.id === trancheId)!
  const ok = async () => {
    const v = await form.validateFields()
    try { await api.post(`/ngo/grants/${grant.id}/tranches`, { ...v, trancheId, date: d8(v.date) }); message.success(`Tranche ${t.sequence} received`); await refresh(qc); onClose() }
    catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={`Receive tranche ${t.sequence}`} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={{ date: dayjs(), amount: t.amount }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="date" label="Date received" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={12}><Form.Item name="amount" label={`Amount (${grant.currency})`} rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          {grant.averageRate !== 1 || grant.agreementRate !== 1 ? <Col span={12}><Form.Item name="rate" label="Exchange rate" extra="Blank = rate table"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col> : null}
          <Col span={grant.agreementRate !== 1 ? 12 : 24}><Form.Item name="bankAccountId" label="Received into" rules={[{ required: true }]}><AccountSelect subTypes={['Bank']} /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

function CloseModal({ grant, onClose }: { grant: Grant; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try { await api.post(`/ngo/grants/${grant.id}/close`, v); message.success(`${grant.number} closed`); await refresh(qc); onClose() } catch (e) { message.error(errorMessage(e)) }
  }
  const pending = grant.reports.filter(r => !r.submittedOn).length
  return (
    <Modal open title={`Close ${grant.number}`} onCancel={onClose} onOk={ok} okText="Close grant" okButtonProps={{ disabled: pending > 0 }}>
      {pending > 0 && <Alert type="error" showIcon title={`${pending} donor report(s) still to submit.`} style={{ marginBottom: 12 }} />}
      {grant.unspentBase > 0 ? <>
        <Alert type="warning" showIcon title={`${amount(grant.unspentBase, 0)} is unspent and will be refunded to ${grant.donorName}.`} style={{ marginBottom: 12 }} />
        <Form form={form} layout="vertical"><Form.Item name="refundFromAccountId" label="Refund from" rules={[{ required: true }]}><AccountSelect subTypes={['Bank']} /></Form.Item></Form>
      </> : grant.unspentBase < 0
        ? <Alert type="warning" showIcon title={`Spending exceeded money received by ${amount(-grant.unspentBase, 0)}; that overspend will be borne by unrestricted funds.`} />
        : <Typography.Text>All money received has been spent.</Typography.Text>}
    </Modal>
  )
}

/** Charge a cost to a grant line or a fund. */
function ChargeModal({ grant, onClose }: { grant?: Grant; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: grants = [] } = useActiveGrants()
  const [form] = Form.useForm()
  const [mode, setMode] = useState<'paid' | 'vendor' | 'allocation'>('paid')
  const [target, setTarget] = useState<'grant' | 'fund'>(grant ? 'grant' : 'grant')
  const grantId = Form.useWatch('grantId', form) ?? grant?.id
  const { data: g } = useQuery({ queryKey: ['ngo-grant', grantId], enabled: !!grantId && target === 'grant', queryFn: async () => (await api.get<Grant>(`/ngo/grants/${grantId}`)).data })
  const lineId = Form.useWatch('budgetLineId', form)
  const ok = async () => {
    const v = await form.validateFields()
    try {
      await api.post('/ngo/expenses', { ...v, date: d8(v.date), grantId: target === 'grant' ? grantId : undefined, fundId: target === 'fund' ? v.fundId : undefined,
        budgetLineId: target === 'grant' ? v.budgetLineId : undefined, accountId: v.accountId ?? undefined,
        paidFromAccountId: mode === 'paid' ? v.paidFromAccountId : undefined, vendorId: mode === 'vendor' ? v.vendorId : undefined, allocationOnly: mode === 'allocation' })
      message.success('Cost charged'); await refresh(qc); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  const line = g?.budgetLines.find(l => l.id === lineId)
  return (
    <Modal open width={680} title={grant ? `Charge a cost to ${grant.number}` : 'Charge a cost'} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={{ date: dayjs(), function: 'Program', entityId: me?.entities.find(e => e.permissions.includes('ngo.expenses.create'))?.id }}>
        {!grant && <Segmented style={{ marginBottom: 12 }} value={target} onChange={v => setTarget(v as typeof target)} options={[{ value: 'grant', label: 'Grant budget line' }, { value: 'fund', label: 'Fund' }]} />}
        <Row gutter={12}>
          {target === 'grant' ? <>
            {!grant && <Col span={12}><Form.Item name="grantId" label="Grant" rules={[{ required: true }]}><Select options={grants.map(x => ({ value: x.id, label: `${x.number} · ${x.title}` }))} /></Form.Item></Col>}
            <Col span={grant ? 24 : 12}><Form.Item name="budgetLineId" label="Budget line" rules={[{ required: true }]} extra={line && `${line.actual.toLocaleString()} of ${line.amount.toLocaleString()} ${g?.currency} used`}>
              <Select options={g?.budgetLines.map(l => ({ value: l.id, label: `${l.code} · ${l.description}` }))} /></Form.Item></Col>
          </> : <>
            <Col span={12}><Form.Item name="fundId" label="Fund" rules={[{ required: true }]}><FundSelect spendable /></Form.Item></Col>
            <Col span={12}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="ngo.expenses.create" /></Form.Item></Col>
          </>}
          <Col span={16}><Form.Item name="description" label="Description" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col span={8}><Form.Item name="amount" label="Amount" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={8}><Form.Item name="function" label="Function"><Select options={['Program', 'ManagementGeneral', 'Fundraising'].map(f => ({ value: f, label: words(f) }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="programId" label="Program"><ProgramSelect /></Form.Item></Col>
          <Col span={24}><Form.Item name="accountId" label="Expense account" rules={target === 'fund' || (line && !line.expenseAccountId) ? [{ required: true }] : []}
            extra={line?.expenseAccountName && `Blank = ${line.expenseAccountName}`}><AccountSelect types={['Expense']} allowClear /></Form.Item></Col>
        </Row>
        <Segmented style={{ marginBottom: 12 }} value={mode} onChange={v => setMode(v as typeof mode)}
          options={[{ value: 'paid', label: 'Paid now' }, { value: 'vendor', label: 'Owed to vendor' }, { value: 'allocation', label: 'Allocate a booked cost' }]} />
        {mode === 'paid' && <Form.Item name="paidFromAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Cash', 'Bank']} /></Form.Item>}
        {mode === 'vendor' && <Form.Item name="vendorId" label="Vendor" extra="An approved bill is created in Finance" rules={[{ required: true }]}><ContactSelect vendors /></Form.Item>}
        {mode === 'allocation' && <Alert type="info" showIcon title="For costs already in the books (e.g. salaries posted by payroll): charges them to this fund and releases restricted income, without posting the expense again." />}
      </Form>
    </Modal>
  )
}

// ======================= Donors & donations =======================

export function DonationsPage() {
  const { can } = useAuth()
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Donors & donations</Typography.Title></div>
      <Card><Tabs items={[
        can('ngo.donations.view') && { key: 'donations', label: 'Donations', children: <DonationsTab /> },
        can('ngo.donors.view') && { key: 'donors', label: 'Donors', children: <DonorsTab /> },
      ].filter(Boolean) as { key: string; label: string; children: React.ReactNode }[]} /></Card>
    </>
  )
}

function DonationsTab() {
  const { can } = useAuth()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [recording, setRecording] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['ngo-donations', search, page], queryFn: async () => (await api.get<PagedResult<DonationListItem>>('/ngo/donations', { params: { search, page, pageSize: 25 } })).data })
  const print = async (id: string) => printReceipt((await api.get<Donation>(`/ngo/donations/${id}`)).data)
  return (
    <>
      <Space wrap style={{ marginBottom: 12 }}>
        {can('ngo.donations.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setRecording(true)}>Record donation</Button>}
        <Input.Search allowClear placeholder="Receipt no., donor, reference" onSearch={v => { setSearch(v); setPage(1) }} style={{ width: 280 }} />
      </Space>
      <Table<DonationListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
        pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[{ title: 'Receipt', dataIndex: 'number' }, { title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'Donor', dataIndex: 'donorName' },
          { title: 'Fund', render: (_, d) => <FundTag f={{ code: d.fundCode, kind: d.fundKind }} /> }, { title: 'Method', dataIndex: 'method', render: words },
          { title: 'Reference', dataIndex: 'reference' }, { title: 'Amount', align: 'right', render: (_, d) => amount(d.amount, 0) },
          { key: 'p', render: (_, d) => <Button size="small" icon={<PrinterOutlined />} onClick={() => print(d.id)}>Receipt</Button> }]} />
      {recording && <DonationModal onClose={() => setRecording(false)} onSaved={printReceipt} />}
    </>
  )
}

function DonationModal({ onClose, onSaved }: { onClose: () => void; onSaved: (d: Donation) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const method = Form.useWatch('method', form)
  const ok = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const d = (await api.post<Donation>('/ngo/donations', { ...v, date: d8(v.date) })).data
      message.success(`Receipt ${d.number} issued`); await refresh(qc); onClose(); onSaved(d)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Modal open width={640} title="Record donation" onCancel={onClose} onOk={ok} okText="Save & print receipt" confirmLoading={busy}>
      <Form form={form} layout="vertical" initialValues={{ date: dayjs(), method: 'BankTransfer', entityId: me?.entities.find(e => e.permissions.includes('ngo.donations.create'))?.id }}>
        <Row gutter={12}>
          <Col span={14}><Form.Item name="donorId" label="Donor" rules={[{ required: true }]}><DonorSelect /></Form.Item></Col>
          <Col span={10}><Form.Item name="amount" label="Amount" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={14}><Form.Item name="fundId" label="Fund" rules={[{ required: true }]} extra="Zakat goes to a Zakat fund; gifts for a stated purpose to a restricted fund"><FundSelect /></Form.Item></Col>
          <Col span={10}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={10}><Form.Item name="method" label="Method"><Select options={['Cash', 'BankTransfer', 'Cheque', 'Online'].map(m => ({ value: m, label: words(m) }))} /></Form.Item></Col>
          <Col span={14}><Form.Item name="bankAccountId" label="Received into" rules={[{ required: true }]}><AccountSelect subTypes={method === 'Cash' ? ['Cash'] : ['Bank', 'Cash']} /></Form.Item></Col>
          <Col span={10}><Form.Item name="reference" label="Reference"><Input placeholder="Cheque / transaction no." /></Form.Item></Col>
          <Col span={14}><Form.Item name="programId" label="Program (optional)"><ProgramSelect /></Form.Item></Col>
          <Col span={24}><Form.Item name="entityId" label="Receiving entity" rules={[{ required: true }]}><EntityPicker permission="ngo.donations.create" /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

function printReceipt(d: Donation) {
  const w = window.open('', '_blank', 'width=800,height=700')
  if (!w) return
  const esc = (x?: string | number) => String(x ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)
  w.document.write(`<html><head><title>${esc(d.number)}</title><style>body{font-family:sans-serif;padding:32px}h1{margin:0}.box{border:2px solid #333;padding:24px;margin-top:16px}
    table{width:100%;border-collapse:collapse}td{padding:6px 0;vertical-align:top}td:first-child{width:35%;color:#555}.amt{font-size:22px;font-weight:bold}.sig{margin-top:56px;text-align:right}</style></head><body>
    <h1>${esc(d.organizationName)}</h1><div>${esc(d.entityName)}</div><div class="box"><h2 style="margin-top:0">Donation receipt ${esc(d.number)}</h2><table>
    <tr><td>Date</td><td>${esc(fmtDate(d.date))}</td></tr><tr><td>Received with thanks from</td><td>${esc(d.donorName)}${d.donorCnic ? ` (CNIC ${esc(d.donorCnic)})` : ''}${d.donorNtn ? ` (NTN ${esc(d.donorNtn)})` : ''}</td></tr>
    <tr><td>Amount</td><td class="amt">${esc(amount(d.amount, 0))}</td></tr><tr><td>In words</td><td>${esc(d.amountInWords)}</td></tr>
    <tr><td>Towards</td><td>${esc(d.fundName)}${d.fundKind === 'Zakat' ? ' (Zakat)' : ''}${d.programName ? ` — ${esc(d.programName)}` : ''}</td></tr>
    <tr><td>Paid by</td><td>${esc(words(d.method))}${d.reference ? ` · ${esc(d.reference)}` : ''}</td></tr></table>
    <div class="sig">Authorised signature ____________________</div></div><script>window.print()</script></body></html>`)
  w.document.close()
}

function DonorsTab() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data, isFetching } = useDonors()
  const [editing, setEditing] = useState<Donor | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (d: Donor | 'new') => { setEditing(d); form.resetFields(); form.setFieldsValue(d === 'new' ? { type: 'Individual' } : d) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      if (editing && editing !== 'new') await api.put(`/ngo/donors/${editing.id}`, v); else await api.post('/ngo/donors', v)
      message.success('Donor saved'); await qc.invalidateQueries({ queryKey: ['ngo-donors'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      {can('ngo.donors.create') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add donor</Button>}
      <Table<Donor> rowKey="id" loading={isFetching} dataSource={data} pagination={{ pageSize: 25, hideOnSinglePage: true }} scroll={{ x: 800 }}
        onRow={d => ({ onClick: () => can('ngo.donors.edit') && open(d), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Donor', render: (_, d) => <><b>{d.name}</b><div><Typography.Text type="secondary">{d.code}{d.city ? ` · ${d.city}` : ''}</Typography.Text></div></> },
          { title: 'Type', dataIndex: 'type' }, { title: 'CNIC / NTN', render: (_, d) => d.cnic ?? d.ntn ?? '—' },
          { title: 'Gifts', align: 'right', render: (_, d) => `${d.donations} · ${amount(d.totalDonated, 0)}` },
          { title: 'Last gift', render: (_, d) => d.lastDonation ? fmtDate(d.lastDonation) : '—' },
          { title: 'Grants', align: 'right', render: (_, d) => d.grants ? `${d.grants} · ${amount(d.grantsReceived, 0)} received` : '—' }]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add donor' : 'Edit donor'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="type" label="Type"><Select options={DONOR_TYPES.map(t => ({ value: t, label: t }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="cnic" label="CNIC"><Input placeholder="35202-1234567-1" /></Form.Item></Col>
            <Col span={12}><Form.Item name="ntn" label="NTN"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="email" label="Email"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="address" label="Address"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="city" label="City"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="country" label="Country"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="notes" label="Notes"><Input.TextArea rows={2} /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

// ======================= Beneficiaries =======================

export function BeneficiariesPage() {
  const { can } = useAuth()
  const [search, setSearch] = useState('')
  const [programId, setProgramId] = useState<string | undefined>()
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)
  const [editing, setEditing] = useState<Beneficiary | 'new' | null>(null)
  const { data, isFetching } = useQuery({ queryKey: ['ngo-bens', search, programId, page],
    queryFn: async () => (await api.get<PagedResult<BeneficiaryListItem>>('/ngo/beneficiaries', { params: { search, programId, page, pageSize: 25 } })).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Beneficiaries</Typography.Title>
        {can('ngo.beneficiaries.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>Register beneficiary</Button>}
      </div>
      <Card>
        <Space wrap style={{ marginBottom: 12 }}>
          <Input.Search allowClear placeholder="Name, CNIC, registration no., phone" onSearch={v => { setSearch(v); setPage(1) }} style={{ width: 300 }} />
          <div style={{ width: 220 }}><ProgramSelect value={programId} onChange={v => { setProgramId(v); setPage(1) }} /></div>
        </Space>
        <Table<BeneficiaryListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 900 }}
          onRow={b => ({ onClick: () => setOpen(b.id), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Reg. no.', dataIndex: 'registrationNo' },
            { title: 'Name', render: (_, b) => <><b>{b.fullName}</b>{!b.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}{b.zakatEligible && <Tag color="purple" style={{ marginLeft: 6 }}>Zakat</Tag>}</> },
            { title: 'CNIC', dataIndex: 'cnic' }, { title: 'Gender', dataIndex: 'gender' }, { title: 'District', dataIndex: 'district' },
            { title: 'Household', dataIndex: 'householdSize' }, { title: 'Program', dataIndex: 'programName' },
            { title: 'Assistance', align: 'right', render: (_, b) => b.assistanceCount ? `${b.assistanceCount} · ${amount(b.assistanceValue, 0)}` : '—' },
            { title: 'Last helped', render: (_, b) => b.lastAssisted ? fmtDate(b.lastAssisted) : '—' },
          ]} />
      </Card>
      {editing && <BeneficiaryModal b={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setOpen} />}
      {open && <BeneficiaryDrawer id={open} onClose={() => setOpen(null)} onEdit={b => { setOpen(null); setEditing(b) }} />}
    </>
  )
}

function BeneficiaryModal({ b, onClose, onSaved }: { b?: Beneficiary; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, dateOfBirth: d8(v.dateOfBirth), enrolledOn: d8(v.enrolledOn) }
      const res = b ? await api.put<Beneficiary>(`/ngo/beneficiaries/${b.id}`, body) : await api.post<Beneficiary>('/ngo/beneficiaries', body)
      message.success(b ? 'Saved' : `Registered as ${res.data.registrationNo}`); await refresh(qc); onClose(); onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open width={720} title={b ? `Edit ${b.registrationNo}` : 'Register beneficiary'} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={b ? { ...b, dateOfBirth: dj(b.dateOfBirth), enrolledOn: dj(b.enrolledOn) }
        : { entityId: me?.entities.find(e => e.permissions.includes('ngo.beneficiaries.create'))?.id, gender: 'Female', householdSize: 1, isActive: true, zakatEligible: false, enrolledOn: dayjs() }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="fullName" label="Full name" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col span={12}><Form.Item name="cnic" label="CNIC" extra="Checked against the whole register to avoid duplicates"><Input placeholder="32304-1234567-2" /></Form.Item></Col>
          <Col span={8}><Form.Item name="gender" label="Gender"><Select options={['Female', 'Male', 'Other'].map(g => ({ value: g, label: g }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="dateOfBirth" label="Date of birth"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={8}><Form.Item name="householdSize" label="Household size"><InputNumber min={1} max={50} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
          <Col span={8}><Form.Item name="district" label="District"><Input /></Form.Item></Col>
          <Col span={8}><Form.Item name="enrolledOn" label="Enrolled on"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={24}><Form.Item name="address" label="Address"><Input /></Form.Item></Col>
          <Col span={24}><Form.Item name="vulnerabilities" label="Vulnerabilities"><Input placeholder="e.g. widow, disability, out-of-school children" /></Form.Item></Col>
          <Col span={12}><Form.Item name="programId" label="Program"><ProgramSelect /></Form.Item></Col>
          <Col span={12}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="ngo.beneficiaries.create" /></Form.Item></Col>
          <Col span={12}><Form.Item name="zakatEligible" label="Verified Zakat-eligible" valuePropName="checked"><Switch /></Form.Item></Col>
          <Col span={12}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

function BeneficiaryDrawer({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit: (b: Beneficiary) => void }) {
  const { can } = useAuth()
  const [assist, setAssist] = useState(false)
  const { data: b, isLoading } = useQuery({ queryKey: ['ngo-ben', id], queryFn: async () => (await api.get<Beneficiary>(`/ngo/beneficiaries/${id}`)).data })
  return (
    <Drawer open onClose={onClose} size={760} loading={isLoading} title={b && <Space>{b.fullName}<Tag>{b.registrationNo}</Tag>{b.zakatEligible && <Tag color="purple">Zakat-eligible</Tag>}</Space>}
      extra={b && can('ngo.beneficiaries.edit') && <Space>
        <Button icon={<EditOutlined />} onClick={() => onEdit(b)}>Edit</Button>
        {b.isActive && <Button type="primary" icon={<PlusOutlined />} onClick={() => setAssist(true)}>Record assistance</Button>}
      </Space>}>
      {b && <>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
          { label: 'CNIC', children: b.cnic ?? '—' }, { label: 'Gender / age', children: `${b.gender}${b.age != null ? ` · ${b.age} years` : ''}` },
          { label: 'Phone', children: b.phone ?? '—' }, { label: 'District', children: b.district ?? '—' },
          { label: 'Household', children: `${b.householdSize} people` }, { label: 'Program', children: b.programName ?? '—' },
          { label: 'Enrolled', children: `${fmtDate(b.enrolledOn)} · ${b.entityName}` }, { label: 'Status', children: b.isActive ? 'Active' : 'Inactive' },
          { label: 'Address', children: b.address ?? '—', span: 'filled' as const }, { label: 'Vulnerabilities', children: b.vulnerabilities ?? '—', span: 'filled' as const },
        ]} />
        <Typography.Title level={5} style={{ marginTop: 24 }}>Assistance received — {amount(b.assistanceValue, 0)}</Typography.Title>
        <Table size="small" rowKey="id" pagination={false} dataSource={b.assistance} locale={{ emptyText: 'No assistance yet' }}
          columns={[{ title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'Type', dataIndex: 'type', render: words }, { title: 'Description', dataIndex: 'description' },
            { title: 'Funded by', render: (_, a) => a.fundCode ?? '—' }, { title: 'Value', align: 'right', render: (_, a) => amount(a.value, 0) }]} />
      </>}
      {assist && b && <AssistanceModal b={b} onClose={() => setAssist(false)} />}
    </Drawer>
  )
}

function AssistanceModal({ b, onClose }: { b: Beneficiary; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const { data: grants = [] } = useActiveGrants()
  const [form] = Form.useForm()
  const type = Form.useWatch('type', form)
  const [source, setSource] = useState<'fund' | 'grant'>('fund')
  const grantId = Form.useWatch('grantId', form)
  const { data: g } = useQuery({ queryKey: ['ngo-grant', grantId], enabled: !!grantId, queryFn: async () => (await api.get<Grant>(`/ngo/grants/${grantId}`)).data })
  const ok = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/ngo/beneficiaries/${b.id}/assistance`, { ...v, date: d8(v.date), fundId: source === 'fund' ? v.fundId : undefined, grantId: source === 'grant' && type === 'Cash' ? v.grantId : undefined,
        budgetLineId: source === 'grant' && type === 'Cash' ? v.budgetLineId : undefined, allowRepeat: !!v.allowRepeat })
      message.success('Assistance recorded'); await refresh(qc); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  const cashAllowed = can('ngo.expenses.create')
  return (
    <Modal open width={640} title={`Assistance for ${b.fullName}`} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={{ date: dayjs(), type: cashAllowed ? 'Cash' : 'InKind', programId: b.programId }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="type" label="Type"><Segmented block options={[{ value: 'Cash', label: 'Cash', disabled: !cashAllowed }, { value: 'InKind', label: 'In kind' }, { value: 'Service', label: 'Service' }]} /></Form.Item></Col>
          <Col span={12}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={16}><Form.Item name="description" label="Description" rules={[{ required: true }]}><Input placeholder={type === 'Cash' ? 'Monthly stipend' : 'Ration bag / school kit'} /></Form.Item></Col>
          <Col span={8}><Form.Item name="value" label="Value" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          {type !== 'Cash' && <Col span={8}><Form.Item name="quantity" label="Quantity"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          <Col span={type === 'Cash' ? 24 : 16}><Form.Item name="programId" label="Program"><ProgramSelect /></Form.Item></Col>
        </Row>
        {type === 'Cash' && <>
          <Segmented style={{ marginBottom: 12 }} value={source} onChange={v => setSource(v as typeof source)} options={[{ value: 'fund', label: 'From a fund' }, { value: 'grant', label: 'From a grant' }]} />
          <Row gutter={12}>
            {source === 'fund' ? <Col span={24}><Form.Item name="fundId" label="Fund" rules={[{ required: true }]} extra={!b.zakatEligible && 'Not Zakat-eligible: Zakat funds will be refused'}><FundSelect spendable /></Form.Item></Col> : <>
              <Col span={12}><Form.Item name="grantId" label="Grant" rules={[{ required: true }]}><Select options={grants.map(x => ({ value: x.id, label: `${x.number} · ${x.title}` }))} /></Form.Item></Col>
              <Col span={12}><Form.Item name="budgetLineId" label="Budget line" rules={[{ required: true }]}><Select options={g?.budgetLines.map(l => ({ value: l.id, label: `${l.code} · ${l.description}` }))} /></Form.Item></Col>
            </>}
            <Col span={24}><Form.Item name="paidFromAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Cash', 'Bank']} /></Form.Item></Col>
          </Row>
        </>}
        {type !== 'Cash' && <Form.Item name="fundId" label="Fund (for reporting only)"><FundSelect allowClear /></Form.Item>}
        <Form.Item name="allowRepeat" valuePropName="checked" style={{ marginBottom: 0 }}><Checkbox>Allow repeat — same help under this program within 30 days</Checkbox></Form.Item>
      </Form>
    </Modal>
  )
}

// ======================= Funds, programs, spending =======================

export function FundsPage() {
  const { can, me } = useAuth()
  const anyNgo = (p: string) => me?.entities.some(e => e.permissions.includes(p))
  const tabs = [
    { key: 'funds', label: 'Funds', children: <FundsTab /> },
    { key: 'programs', label: 'Programs', children: <ProgramsTab /> },
    (anyNgo('ngo.expenses.view') || can('ngo.expenses.view')) && { key: 'spending', label: 'Spending', children: <SpendingTab /> },
  ].filter(Boolean) as { key: string; label: string; children: React.ReactNode }[]
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Funds & programs</Typography.Title></div>
      <Card><Tabs items={tabs} /></Card>
    </>
  )
}

function FundsTab() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data, isFetching, error } = useFunds()
  const [editing, setEditing] = useState<Fund | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (f: Fund | 'new') => { setEditing(f); form.resetFields(); form.setFieldsValue(f === 'new' ? { kind: 'Unrestricted', isActive: true } : f) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      if (editing && editing !== 'new') await api.put(`/ngo/funds/${editing.id}`, v); else await api.post('/ngo/funds', v)
      message.success('Fund saved'); await qc.invalidateQueries({ queryKey: ['ngo-funds'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  if (error) return <Result status="403" title={errorMessage(error)} />
  return (
    <>
      {can('ngo.funds.manage') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add fund</Button>}
      <Table<Fund> rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 800 }}
        onRow={f => ({ onClick: () => can('ngo.funds.manage') && !f.grantId && open(f), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Fund', render: (_, f) => <><FundTag f={f} /><b>{f.name}</b>{!f.isActive && <Tag style={{ marginLeft: 6 }}>Closed</Tag>}</> },
          { title: 'Kind', dataIndex: 'kind', render: (k: string, f) => <>{k}{f.grantNumber && <Typography.Text type="secondary"> · grant</Typography.Text>}</> },
          { title: 'Received', align: 'right', render: (_, f) => amount(f.received, 0) }, { title: 'Spent', align: 'right', render: (_, f) => amount(f.spent, 0) },
          { title: 'Balance', align: 'right', render: (_, f) => <b>{amount(f.balance, 0)}</b> }]} />
      <Alert type="info" showIcon style={{ marginTop: 16 }} title="How funds post"
        description="Unrestricted gifts are income straight away. Restricted gifts, grant tranches and Zakat wait in deferred income and become income as they are spent. Endowments are capital and can't be spent." />
      <Modal open={!!editing} title={editing === 'new' ? 'Add fund' : 'Edit fund'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="kind" label="Kind"><Select options={['Unrestricted', 'Restricted', 'Zakat', 'Endowment'].map(k => ({ value: k, label: k }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="isActive" label="Open" valuePropName="checked"><Switch /></Form.Item></Col>
            <Col span={24}><Form.Item name="purpose" label="Purpose / donor restriction"><Input.TextArea rows={2} /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

function ProgramsTab() {
  const { can, me } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data, isFetching } = usePrograms()
  const [editing, setEditing] = useState<NgoProgram | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (p: NgoProgram | 'new') => { setEditing(p); form.resetFields(); form.setFieldsValue(p === 'new' ? { isActive: true, targetBeneficiaries: 0, entityId: me?.entities.find(e => e.permissions.includes('ngo.programs.create'))?.id } : p) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      if (editing && editing !== 'new') await api.put(`/ngo/programs/${editing.id}`, v); else await api.post('/ngo/programs', v)
      message.success('Program saved'); await qc.invalidateQueries({ queryKey: ['ngo-programs'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      {can('ngo.programs.create') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add program</Button>}
      <Table<NgoProgram> rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 700 }}
        onRow={p => ({ onClick: () => can('ngo.programs.edit') && open(p), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Program', render: (_, p) => <><b>{p.code}</b> {p.name}{!p.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}</> },
          { title: 'Sector', dataIndex: 'sector' }, { title: 'Entity', dataIndex: 'entityName' },
          { title: 'Reach', width: 200, render: (_, p) => <Progress size="small" percent={p.targetBeneficiaries ? Math.round(p.beneficiaries / p.targetBeneficiaries * 100) : 0} format={() => `${p.beneficiaries} / ${p.targetBeneficiaries}`} /> },
          { title: 'Spent', align: 'right', render: (_, p) => amount(p.spent, 0) }]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add program' : 'Edit program'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="sector" label="Sector"><Select allowClear options={['Education', 'Health', 'WASH', 'Livelihoods', 'Food security', 'Shelter', 'Protection', 'Emergency relief'].map(s => ({ value: s, label: s }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="targetBeneficiaries" label="Target beneficiaries"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={24}><Form.Item name="description" label="Description"><Input.TextArea rows={2} /></Form.Item></Col>
            <Col span={16}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="ngo.programs.create" /></Form.Item></Col>
            <Col span={8}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

function SpendingTab() {
  const { can } = useAuth()
  const [fundId, setFundId] = useState<string | undefined>()
  const [charging, setCharging] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['ngo-expenses', fundId], queryFn: async () => (await api.get<FundExpense[]>('/ngo/expenses', { params: { fundId } })).data })
  return (
    <>
      <Space wrap style={{ marginBottom: 12 }}>
        {can('ngo.expenses.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCharging(true)}>Charge cost</Button>}
        <div style={{ width: 280 }}><FundSelect value={fundId} onChange={setFundId} allowClear /></div>
      </Space>
      <ExpenseTable rows={data} loading={isFetching} />
      {charging && <ChargeModal onClose={() => setCharging(false)} />}
    </>
  )
}

// ======================= Reports =======================

export function NgoReportsPage() {
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null)
  const params = range ? { from: d8(range[0]), to: d8(range[1]) } : {}
  const { data: fx, isFetching: f1, error } = useQuery({ queryKey: ['ngo-fx', params], retry: false, queryFn: async () => (await api.get<FunctionalExpenses>('/ngo/reports/functional', { params })).data })
  const { data: donors, isFetching: f2 } = useQuery({ queryKey: ['ngo-donor-sum', params], enabled: !error, queryFn: async () => (await api.get<DonorSummaryRow[]>('/ngo/reports/donors', { params })).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>NGO reports</Typography.Title>
        <DatePicker.RangePicker value={range} onChange={v => setRange(v as [Dayjs, Dayjs] | null)} format="DD MMM YYYY" placeholder={['Financial year start', 'Today']} />
      </div>
      <Card title={`Statement of functional expenses${fx ? ` · ${fmtDate(fx.from)} – ${fmtDate(fx.to)}` : ''}`} loading={f1} style={{ marginBottom: 16 }}
        extra={fx && <Tag color={fx.programRatio >= 75 ? 'green' : 'orange'}>Program ratio {fx.programRatio}%</Tag>}>
        <Table size="small" rowKey="program" pagination={false} dataSource={fx?.rows} scroll={{ x: 600 }}
          summary={() => fx && <Table.Summary.Row><Table.Summary.Cell index={0}><b>Total</b></Table.Summary.Cell>
            {[fx.programCost, fx.managementGeneral, fx.fundraising, fx.total].map((v, i) => <Table.Summary.Cell key={i} index={i + 1} align="right"><b>{amount(v, 0)}</b></Table.Summary.Cell>)}</Table.Summary.Row>}
          columns={[{ title: 'Program', dataIndex: 'program' }, { title: 'Program services', align: 'right', render: (_, r) => amount(r.programCost, 0) },
            { title: 'Management & general', align: 'right', render: (_, r) => amount(r.managementGeneral, 0) },
            { title: 'Fundraising', align: 'right', render: (_, r) => amount(r.fundraising, 0) }, { title: 'Total', align: 'right', render: (_, r) => amount(r.total, 0) }]} />
      </Card>
      <Card title="Donor summary" loading={f2}>
        <Table size="small" rowKey="donorId" pagination={false} dataSource={donors} scroll={{ x: 600 }}
          columns={[{ title: 'Donor', dataIndex: 'donorName' }, { title: 'Type', dataIndex: 'type' },
            { title: 'Donations', align: 'right', render: (_, r) => r.gifts ? `${r.gifts} · ${amount(r.donations, 0)}` : '—' },
            { title: 'Grants committed', align: 'right', render: (_, r) => r.grantsCommitted ? amount(r.grantsCommitted, 0) : '—' },
            { title: 'Grant money received', align: 'right', render: (_, r) => r.grantsReceived ? amount(r.grantsReceived, 0) : '—' }]} />
      </Card>
    </>
  )
}
