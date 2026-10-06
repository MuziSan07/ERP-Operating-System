import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Popconfirm, Progress, Row,
  Segmented, Select, Space, Statistic, Table, Tabs, Tag, Typography,
} from 'antd'
import { CheckOutlined, CloseOutlined, DeleteOutlined, EditOutlined, FileDoneOutlined, LeftOutlined, PlusOutlined, RightOutlined, SendOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  COLUMNS, ENTRY_COLORS, PRIORITY_COLORS, PROJECT_COLORS, hrs, words, type BillingType, type Client, type InvoiceResult, type MyWeek, type Project,
  type ProjectListItem, type ProjectPerson, type ProjectStatus, type ProjectsDashboard, type Task, type TimeEntry, type Utilization, type WorkItemStatus,
} from '../../api/projects'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { TaxRateSelect } from '../../components/FinancePickers'

const usePeople = () => useQuery({ queryKey: ['prj-people'], queryFn: async () => (await api.get<ProjectPerson[]>('/projects/people')).data })
const useClients = () => useQuery({ queryKey: ['prj-clients'], queryFn: async () => (await api.get<Client[]>('/projects/clients')).data })
const d8 = (d?: Dayjs | null) => d?.format('YYYY-MM-DD')
const refresh = (qc: ReturnType<typeof useQueryClient>) => Promise.all(['prj-dash', 'prj-list', 'prj-project', 'prj-tasks', 'prj-week', 'prj-approvals', 'prj-time', 'prj-clients']
  .map(k => qc.invalidateQueries({ queryKey: [k] })))
const HoursBar = ({ used, budget, pct }: { used: number; budget: number; pct: number }) => budget > 0
  ? <Progress size="small" percent={Math.min(100, pct)} status={pct > 100 ? 'exception' : 'normal'} format={() => `${+used.toFixed(1)} / ${budget} h`} />
  : <Typography.Text type="secondary">{hrs(used)}</Typography.Text>

// ======================= Dashboard =======================

export function ProjectsDashboardPage() {
  const [open, setOpen] = useState<string | null>(null)
  const { data, isLoading, error } = useQuery({ queryKey: ['prj-dash'], retry: false, queryFn: async () => (await api.get<ProjectsDashboard>('/projects/dashboard')).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Projects</Typography.Title></div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Active projects', data?.activeProjects], ['Hours this week', hrs(data?.hoursThisWeek)], ['Billable this week', data?.hoursThisWeek ? `${Math.round((data.billableThisWeek / data.hoursThisWeek) * 100)}%` : '—'],
          ['Ready to invoice', amount(data?.unbilled, 0)], ['Invoiced this month', amount(data?.billedThisMonth, 0)], ['Timesheets to approve', data?.pendingApprovals]].map(([t, v]) => (
          <Col key={t as string} xs={12} md={8} xl={4}><Card loading={isLoading} size="small"><Statistic title={t as string} value={v as string} /></Card></Col>
        ))}
      </Row>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title="Projects needing attention" loading={isLoading} extra={<Typography.Text type="secondary">over budget, ≥ 90% of hours, or losing money</Typography.Text>}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.atRisk} locale={{ emptyText: 'All projects on track' }} scroll={{ x: 600 }}
              onRow={p => ({ onClick: () => setOpen(p.id), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Project', render: (_, p) => <><b>{p.code}</b> {p.name}<div><Typography.Text type="secondary">{p.clientName}</Typography.Text></div></> },
                { title: 'Hours', width: 180, render: (_, p) => <HoursBar used={p.hoursLogged} budget={p.budgetHours} pct={p.hoursPercent} /> },
                { title: 'Margin', align: 'right', render: (_, p) => <Typography.Text type={p.margin < 0 ? 'danger' : undefined}>{amount(p.margin, 0)}</Typography.Text> }]} />
          </Card>
        </Col>
        <Col xs={24} xl={10}>
          <Card title="Milestones due (30 days)" loading={isLoading} style={{ marginBottom: 16 }}>
            <Table size="small" rowKey={m => `${m.projectId}-${m.name}`} pagination={false} dataSource={data?.milestonesDue} locale={{ emptyText: 'None' }}
              onRow={m => ({ onClick: () => setOpen(m.projectId), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Milestone', render: (_, m) => <><Tag>{m.projectCode}</Tag>{m.name}</> }, { title: 'Amount', align: 'right', render: (_, m) => amount(m.amount, 0) },
                { title: 'Due', render: (_, m) => m.completed ? <Tag color="green">Done — invoice</Tag> : <Tag color={m.overdue ? 'red' : 'orange'}>{fmtDate(m.dueDate)}</Tag> }]} />
          </Card>
          <Card title="Overdue tasks" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.overdueTasks} locale={{ emptyText: 'Nothing overdue' }}
              onRow={t => ({ onClick: () => setOpen(t.projectId), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Task', render: (_, t) => <><Tag>{t.key}</Tag>{t.title}</> }, { title: 'Assignee', dataIndex: 'assigneeName' },
                { title: 'Due', dataIndex: 'dueDate', render: (d: string) => <Typography.Text type="danger">{fmtDate(d)}</Typography.Text> }]} />
          </Card>
        </Col>
      </Row>
      {open && <ProjectDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

// ======================= Projects =======================

export function ProjectsPage() {
  const { can } = useAuth()
  const [status, setStatus] = useState<ProjectStatus | 'All'>('Active')
  const [open, setOpen] = useState<string | null>(null)
  const [editing, setEditing] = useState<Project | 'new' | null>(null)
  const { data, isFetching } = useQuery({ queryKey: ['prj-list', status], queryFn: async () => (await api.get<ProjectListItem[]>('/projects', { params: { status: status === 'All' ? undefined : status } })).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Projects</Typography.Title>
        {can('projects.projects.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New project</Button>}
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={status} onChange={v => setStatus(v as typeof status)} options={['Active', 'Planned', 'OnHold', 'Completed', 'All'].map(s => ({ value: s, label: words(s) }))} />
        <Table<ProjectListItem> rowKey="id" loading={isFetching} dataSource={data} scroll={{ x: 1100 }} onRow={p => ({ onClick: () => setOpen(p.id), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Project', render: (_, p) => <><b>{p.code}</b> {p.name}<div><Typography.Text type="secondary">{p.clientName ?? 'Internal'}</Typography.Text></div></> },
            { title: 'Billing', dataIndex: 'billingType', render: (b: BillingType) => <Tag>{words(b)}</Tag> },
            { title: 'Manager', dataIndex: 'managerName' },
            { title: 'Hours', width: 180, render: (_, p) => <HoursBar used={p.hoursLogged} budget={p.budgetHours} pct={p.hoursPercent} /> },
            { title: 'Invoiced', align: 'right', render: (_, p) => amount(p.billed, 0) },
            { title: 'To invoice', align: 'right', render: (_, p) => p.unbilled ? <Typography.Text strong>{amount(p.unbilled, 0)}</Typography.Text> : '—' },
            { title: 'Cost', align: 'right', render: (_, p) => amount(p.cost, 0) },
            { title: 'Margin', align: 'right', render: (_, p) => <Typography.Text type={p.margin < 0 ? 'danger' : undefined}>{amount(p.margin, 0)}</Typography.Text> },
            { title: 'Open tasks', dataIndex: 'openTasks' },
            { title: 'Status', dataIndex: 'status', render: (s: ProjectStatus) => <Tag color={PROJECT_COLORS[s]}>{words(s)}</Tag> },
          ]} />
      </Card>
      {editing && <ProjectEditor project={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setOpen} />}
      {open && <ProjectDrawer id={open} onClose={() => setOpen(null)} onEdit={p => { setOpen(null); setEditing(p) }} />}
    </>
  )
}

interface MemberDraft { employeeId?: string; role?: string; billRate?: number | null; costRate?: number | null }
interface MilestoneDraft { id?: string; name: string; dueDate?: Dayjs | null; amount: number; invoiced?: boolean }

function ProjectEditor({ project, onClose, onSaved }: { project?: Project; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: people = [] } = usePeople()
  const { data: clients = [] } = useClients()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const billing: BillingType = Form.useWatch('billingType', form) ?? project?.billingType ?? 'TimeAndMaterials'
  const contract = Form.useWatch('contractAmount', form) ?? 0
  const [members, setMembers] = useState<MemberDraft[]>(project?.members.map(m => ({ employeeId: m.employeeId, role: m.role, billRate: m.billRate, costRate: m.costRate })) ?? [{}])
  const [milestones, setMilestones] = useState<MilestoneDraft[]>(project?.milestones.map(m => ({ id: m.id, name: m.name, dueDate: dayjs(m.dueDate), amount: m.amount, invoiced: !!m.invoiceId })) ?? [])
  const setM = (i: number, p: Partial<MemberDraft>) => setMembers(xs => xs.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const setMs = (i: number, p: Partial<MilestoneDraft>) => setMilestones(xs => xs.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const msTotal = milestones.reduce((s, m) => s + (m.amount || 0), 0)

  const save = async () => {
    const v = await form.validateFields()
    if (members.some(m => !m.employeeId)) { message.error('Choose a person for every team row (or remove it).'); return }
    if (milestones.some(m => !m.name || !m.dueDate)) { message.error('Every milestone needs a name and due date.'); return }
    setBusy(true)
    try {
      const body = { ...v, startDate: d8(v.startDate), endDate: d8(v.endDate), contractAmount: v.contractAmount ?? 0, defaultBillRate: v.defaultBillRate ?? 0, budgetHours: v.budgetHours ?? 0,
        members: members.map(m => ({ employeeId: m.employeeId, role: m.role, billRate: m.billRate ?? undefined, costRate: m.costRate ?? undefined })),
        milestones: billing === 'FixedPrice' ? milestones.map(m => ({ id: m.id, name: m.name, dueDate: d8(m.dueDate), amount: m.amount })) : [] }
      const res = project ? await api.put<Project>(`/projects/${project.id}`, body) : await api.post<Project>('/projects', body)
      message.success(project ? 'Project saved' : `Project ${res.data.code} created`)
      await refresh(qc)
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  const memberIds = members.map(m => m.employeeId).filter(Boolean)
  return (
    <Drawer open onClose={onClose} size={980} title={project ? `Edit ${project.code}` : 'New project'} extra={<Button type="primary" loading={busy} onClick={save}>Save</Button>}>
      <Form form={form} layout="vertical" initialValues={project ? { ...project, startDate: dayjs(project.startDate), endDate: project.endDate ? dayjs(project.endDate) : undefined }
        : { entityId: me?.entities.find(e => e.permissions.includes('projects.projects.create'))?.id, billingType: 'TimeAndMaterials', startDate: dayjs(), budgetHours: 0, defaultBillRate: 0 }}>
        <Row gutter={12}>
          <Col xs={8} md={4}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input placeholder="WEB" style={{ textTransform: 'uppercase' }} /></Form.Item></Col>
          <Col xs={16} md={10}><Form.Item name="name" label="Project name" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={10}><Form.Item name="billingType" label="Billing"><Segmented block options={['TimeAndMaterials', 'FixedPrice', 'NonBillable'].map(b => ({ value: b, label: words(b) }))} /></Form.Item></Col>
          {billing !== 'NonBillable' && <Col xs={24} md={10}><Form.Item name="clientId" label="Client" rules={[{ required: true }]}>
            <Select showSearch={{ optionFilterProp: 'label' }} options={clients.map(c => ({ value: c.id, label: `${c.name} (${c.code})` }))} placeholder="Client" /></Form.Item></Col>}
          <Col xs={12} md={4}><Form.Item name="startDate" label="Start" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="endDate" label="Planned end"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={3}><Form.Item name="budgetHours" label="Hour budget"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          {billing === 'TimeAndMaterials' && <Col xs={12} md={3}><Form.Item name="defaultBillRate" label="Rate / hour"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          {billing === 'FixedPrice' && <Col xs={12} md={3}><Form.Item name="contractAmount" label="Fixed price" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          {billing !== 'NonBillable' && <Col xs={24} md={8}><Form.Item name="taxRateId" label="Sales tax on services"><TaxRateSelect /></Form.Item></Col>}
          <Col xs={24} md={8}><Form.Item name="managerEmployeeId" label="Project manager" extra="Must be on the team">
            <Select allowClear options={people.filter(p => memberIds.includes(p.employeeId)).map(p => ({ value: p.employeeId, label: p.name }))} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="projects.projects.create" /></Form.Item></Col>
          <Col xs={24}><Form.Item name="description" label="Scope"><Input.TextArea rows={2} /></Form.Item></Col>
        </Row>
      </Form>
      <Typography.Title level={5}>Team</Typography.Title>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={members} scroll={{ x: 760 }}
        columns={[
          { title: 'Person', width: 240, render: (_, m, i) => <Select value={m.employeeId} style={{ width: '100%' }} showSearch={{ optionFilterProp: 'label' }} placeholder="Choose"
            onChange={v => setM(i, { employeeId: v, costRate: null })} options={people.map(p => ({ value: p.employeeId, label: `${p.name}${p.designation ? ` · ${p.designation}` : ''}`, disabled: memberIds.includes(p.employeeId) && p.employeeId !== m.employeeId }))} /> },
          { title: 'Role', render: (_, m, i) => <Input value={m.role} placeholder="Developer" onChange={e => setM(i, { role: e.target.value })} /> },
          ...(billing === 'TimeAndMaterials' ? [{ title: 'Bill rate', width: 130, render: (_: unknown, m: MemberDraft, i: number) =>
            <InputNumber min={0} value={m.billRate} placeholder="Default" onChange={v => setM(i, { billRate: v })} style={{ width: '100%' }} /> }] : []),
          { title: 'Cost / hour', width: 140, render: (_, m, i) => <InputNumber min={0} value={m.costRate} style={{ width: '100%' }} onChange={v => setM(i, { costRate: v })}
            placeholder={m.employeeId ? `${people.find(p => p.employeeId === m.employeeId)?.costRate ?? 0} (salary)` : ''} /> },
          { key: 'x', width: 50, render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} onClick={() => setMembers(xs => xs.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setMembers(xs => [...xs, {}])}>Add person</Button>
      {billing === 'FixedPrice' && <>
        <Typography.Title level={5} style={{ marginTop: 24 }}>Milestones — {msTotal.toLocaleString()} of {Number(contract).toLocaleString()}
          {msTotal !== contract && <Tag color="orange" style={{ marginLeft: 8 }}>Must equal the fixed price</Tag>}</Typography.Title>
        <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={milestones}
          columns={[
            { title: 'Milestone', render: (_, m, i) => <Input value={m.name} disabled={m.invoiced} onChange={e => setMs(i, { name: e.target.value })} /> },
            { title: 'Due', width: 160, render: (_, m, i) => <DatePicker value={m.dueDate} onChange={d => setMs(i, { dueDate: d })} format="DD MMM YYYY" /> },
            { title: 'Amount', width: 150, render: (_, m, i) => <InputNumber min={0} value={m.amount} disabled={m.invoiced} onChange={v => setMs(i, { amount: v ?? 0 })} style={{ width: '100%' }} /> },
            { key: 'x', width: 50, render: (_, m, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={m.invoiced} onClick={() => setMilestones(xs => xs.filter((_, j) => j !== i))} /> },
          ]} />
        <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setMilestones(xs => [...xs, { name: '', amount: Math.max(0, contract - msTotal) }])}>Add milestone</Button>
      </>}
    </Drawer>
  )
}

function ProjectDrawer({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit?: (p: Project) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message, modal } = App.useApp()
  const { data: p, isLoading } = useQuery({ queryKey: ['prj-project', id], queryFn: async () => (await api.get<Project>(`/projects/${id}`)).data })
  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); message.success(ok); await refresh(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const invoice = (path: string) => act(async () => {
    const r = (await api.post<InvoiceResult>(path, {})).data
    modal.success({ title: `Invoice ${r.invoiceNumber} issued`, content: `${r.hours ? `${hrs(r.hours)} · ` : ''}${amount(r.total, 0)} including tax. Find it under Finance → Invoices.` })
  }, 'Invoiced')
  const status = (s: ProjectStatus, label: string) => act(() => api.post(`/projects/${id}/status/${s}`), label)
  const edit = can('projects.projects.edit')
  return (
    <Drawer open onClose={onClose} size={1100} loading={isLoading} title={p && <Space>{p.code} · {p.name}<Tag color={PROJECT_COLORS[p.status]}>{words(p.status)}</Tag></Space>}
      extra={p && <Space wrap>
        {edit && onEdit && ['Planned', 'Active', 'OnHold'].includes(p.status) && <Button icon={<EditOutlined />} onClick={() => onEdit(p)}>Edit</Button>}
        {edit && p.status === 'Planned' && <Button type="primary" onClick={() => status('Active', 'Project started')}>Start</Button>}
        {edit && p.status === 'Active' && <Button onClick={() => status('OnHold', 'On hold')}>Hold</Button>}
        {edit && p.status === 'OnHold' && <Button onClick={() => status('Active', 'Resumed')}>Resume</Button>}
        {edit && ['Active', 'OnHold'].includes(p.status) && <Popconfirm title="Mark the project complete? No more time can be logged." onConfirm={() => status('Completed', 'Completed')}><Button>Complete</Button></Popconfirm>}
        {edit && ['Planned', 'OnHold'].includes(p.status) && <Popconfirm title="Cancel this project?" onConfirm={() => status('Cancelled', 'Cancelled')}><Button danger>Cancel</Button></Popconfirm>}
        {can('projects.billing.create') && p.billingType === 'TimeAndMaterials' && p.unbilled > 0 &&
          <Popconfirm title={`Invoice ${amount(p.unbilled, 0)} of approved time (plus tax)?`} onConfirm={() => invoice(`/projects/${id}/invoice`)}><Button type="primary" icon={<FileDoneOutlined />}>Invoice approved time</Button></Popconfirm>}
      </Space>}>
      {p && <Tabs items={[
        { key: 'overview', label: 'Overview', children: <>
          {p.warnings.map(w => <Alert key={w} type="warning" showIcon title={w} style={{ marginBottom: 8 }} />)}
          <Row gutter={[16, 16]} style={{ margin: '16px 0' }}>
            <Col xs={12} md={6}><Typography.Text type="secondary">Hours</Typography.Text><HoursBar used={p.hoursLogged} budget={p.budgetHours} pct={p.hoursPercent} /></Col>
            <Col xs={12} md={4}><Statistic title="Invoiced" value={amount(p.billed, 0)} /></Col>
            <Col xs={12} md={4}><Statistic title={p.billingType === 'FixedPrice' ? 'Milestones done, not invoiced' : 'Approved, not invoiced'} value={amount(p.unbilled, 0)} /></Col>
            <Col xs={12} md={5}><Statistic title="Team cost" value={amount(p.cost, 0)} /></Col>
            <Col xs={12} md={5}><Statistic title="Margin" value={`${amount(p.margin, 0)} (${p.marginPercent}%)`} styles={{ content: { color: p.margin < 0 ? '#cf1322' : undefined } }} /></Col>
          </Row>
          <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
            { label: 'Client', children: p.clientName ?? 'Internal' }, { label: 'Billing', children: `${words(p.billingType)}${p.billingType === 'FixedPrice' ? ` · ${amount(p.contractAmount, 0)}` : p.billingType === 'TimeAndMaterials' ? ` · ${amount(p.defaultBillRate, 0)}/h default` : ''}` },
            { label: 'Dates', children: `${fmtDate(p.startDate)} – ${p.endDate ? fmtDate(p.endDate) : 'open'}` }, { label: 'Manager', children: p.managerName ?? '—' },
            { label: 'Approved / pending hours', children: `${hrs(p.hoursApproved)} / ${hrs(p.hoursPending)}` }, { label: 'Entity', children: p.entityName },
            ...(p.description ? [{ label: 'Scope', children: p.description, span: 'filled' as const }] : []),
          ]} />
          <Typography.Title level={5} style={{ marginTop: 24 }}>Team</Typography.Title>
          <Table size="small" rowKey="id" pagination={false} dataSource={p.members}
            columns={[{ title: 'Person', render: (_, m) => <>{m.name}{m.role && <Typography.Text type="secondary"> · {m.role}</Typography.Text>}</> },
              ...(p.billingType === 'TimeAndMaterials' ? [{ title: 'Bill rate', align: 'right' as const, render: (_: unknown, m: Project['members'][number]) => amount(m.effectiveBillRate, 0) }] : []),
              { title: 'Cost / hour', align: 'right', render: (_, m) => amount(m.costRate, 0) },
              { title: 'Hours', align: 'right', render: (_, m) => `${hrs(m.hours)}${p.billingType === 'TimeAndMaterials' ? ` (${hrs(m.billableHours)} billable)` : ''}` }]} />
          {p.billingType === 'FixedPrice' && <>
            <Typography.Title level={5} style={{ marginTop: 24 }}>Milestones</Typography.Title>
            <Table size="small" rowKey="id" pagination={false} dataSource={p.milestones}
              columns={[{ title: 'Milestone', dataIndex: 'name' }, { title: 'Due', render: (_, m) => <Typography.Text type={m.overdue ? 'danger' : undefined}>{fmtDate(m.dueDate)}</Typography.Text> },
                { title: 'Amount', align: 'right', render: (_, m) => amount(m.amount, 0) },
                { title: '', render: (_, m) => m.invoiceId ? <Tag color="blue">Invoiced {m.invoiceNumber}</Tag>
                  : m.completedOn ? <Space><Tag color="green">Done {fmtDate(m.completedOn)}</Tag>
                    {can('projects.billing.create') && <Button size="small" type="primary" onClick={() => invoice(`/projects/${id}/milestones/${m.id}/invoice`)}>Invoice</Button>}</Space>
                  : edit && p.status === 'Active' && <Popconfirm title="Has the client accepted this milestone?" onConfirm={() => act(() => api.post(`/projects/${id}/milestones/${m.id}/complete`), 'Milestone completed')}>
                    <Button size="small">Mark complete</Button></Popconfirm> }]} />
          </>}
        </> },
        { key: 'board', label: `Board (${Object.entries(p.tasksByStatus).filter(([k]) => k !== 'Done').reduce((s, [, n]) => s + n, 0)} open)`, children: <TaskBoard project={p} /> },
        { key: 'time', label: 'Time', children: <ProjectTime projectId={p.id} /> },
      ]} />}
    </Drawer>
  )
}

function TaskBoard({ project }: { project: Project }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [editing, setEditing] = useState<Task | WorkItemStatus | null>(null)
  const [dragging, setDragging] = useState<string | null>(null)
  const { data = [], isLoading } = useQuery({ queryKey: ['prj-tasks', project.id], queryFn: async () => (await api.get<Task[]>('/projects/tasks', { params: { projectId: project.id } })).data })
  const move = async (taskId: string, status: WorkItemStatus) => {
    const t = data.find(x => x.id === taskId)
    if (!t || t.status === status) return
    try { await api.post(`/projects/tasks/${taskId}/move`, { status, sortOrder: data.filter(x => x.status === status).length }); await refresh(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const closed = ['Completed', 'Cancelled'].includes(project.status)
  return (
    <>
      <Row gutter={12} wrap={false} style={{ overflowX: 'auto' }}>
        {COLUMNS.map(col => (
          <Col key={col} flex="1 0 230px" onDragOver={e => e.preventDefault()} onDrop={() => { if (dragging) move(dragging, col); setDragging(null) }}>
            <Card size="small" loading={isLoading} title={<Space>{words(col)}<Tag>{data.filter(t => t.status === col).length}</Tag></Space>} style={{ minHeight: 360 }}
              extra={!closed && can('projects.tasks.create') && <Button size="small" type="text" icon={<PlusOutlined />} onClick={() => setEditing(col)} />}>
              <Space orientation="vertical" style={{ width: '100%' }}>
                {data.filter(t => t.status === col).map(t => (
                  <Card key={t.id} size="small" hoverable draggable={!closed} onDragStart={() => setDragging(t.id)} onClick={() => can('projects.tasks.edit') && setEditing(t)}>
                    <Space size={4} wrap><Typography.Text type="secondary" style={{ fontSize: 12 }}>{t.key}</Typography.Text><Tag color={PRIORITY_COLORS[t.priority]} style={{ fontSize: 11 }}>{t.priority}</Tag></Space>
                    <div style={{ fontWeight: 500, margin: '4px 0' }}>{t.title}</div>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {t.assigneeName ?? 'Unassigned'} · {+t.loggedHours.toFixed(1)}{t.estimateHours ? `/${t.estimateHours}` : ''} h
                      {t.dueDate && <> · <Typography.Text type={t.overdue ? 'danger' : 'secondary'} style={{ fontSize: 12 }}>{dayjs(t.dueDate).format('DD MMM')}</Typography.Text></>}
                    </Typography.Text>
                  </Card>
                ))}
              </Space>
            </Card>
          </Col>
        ))}
      </Row>
      <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>Drag cards between columns. Assignees can move their own cards.</Typography.Text>
      {editing && <TaskModal project={project} task={typeof editing === 'string' ? undefined : editing} status={typeof editing === 'string' ? editing : editing.status} onClose={() => setEditing(null)} />}
    </>
  )
}

function TaskModal({ project, task, status, onClose }: { project: Project; task?: Task; status: WorkItemStatus; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, projectId: project.id, dueDate: d8(v.dueDate), estimateHours: v.estimateHours ?? 0 }
      if (task) await api.put(`/projects/tasks/${task.id}`, body); else await api.post('/projects/tasks', body)
      message.success('Task saved'); await refresh(qc); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  const remove = async () => {
    try { await api.delete(`/projects/tasks/${task!.id}`); message.success('Task deleted'); await refresh(qc); onClose() } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open width={640} title={task ? task.key : 'New task'} onCancel={onClose} onOk={ok}
      footer={(_, { OkBtn, CancelBtn }) => <Space>{task && can('projects.tasks.delete') && <Popconfirm title="Delete this task?" onConfirm={remove}><Button danger>Delete</Button></Popconfirm>}<CancelBtn /><OkBtn /></Space>}>
      <Form form={form} layout="vertical" initialValues={task ? { ...task, dueDate: task.dueDate ? dayjs(task.dueDate) : undefined } : { status, priority: 'Medium' }}>
        <Form.Item name="title" label="Title" rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="description" label="Description"><Input.TextArea rows={3} /></Form.Item>
        <Row gutter={12}>
          <Col span={8}><Form.Item name="status" label="Status"><Select options={COLUMNS.map(c => ({ value: c, label: words(c) }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="priority" label="Priority"><Select options={['Low', 'Medium', 'High', 'Urgent'].map(c => ({ value: c, label: c }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="assigneeEmployeeId" label="Assignee"><Select allowClear options={project.members.map(m => ({ value: m.employeeId, label: m.name }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="estimateHours" label="Estimate (h)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="dueDate" label="Due"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          {project.billingType === 'FixedPrice' && <Col span={8}><Form.Item name="milestoneId" label="Milestone"><Select allowClear options={project.milestones.map(m => ({ value: m.id, label: m.name }))} /></Form.Item></Col>}
        </Row>
      </Form>
    </Modal>
  )
}

function ProjectTime({ projectId }: { projectId: string }) {
  const { data, isFetching } = useQuery({ queryKey: ['prj-time', projectId], queryFn: async () => (await api.get<TimeEntry[]>('/timesheets', { params: { projectId } })).data })
  return <TimeTable rows={data} loading={isFetching} showPerson />
}

const TimeTable = ({ rows, loading, showPerson, extra }: { rows?: TimeEntry[]; loading?: boolean; showPerson?: boolean; extra?: (e: TimeEntry) => React.ReactNode }) => (
  <Table size="small" rowKey="id" loading={loading} dataSource={rows} pagination={{ pageSize: 50, hideOnSinglePage: true }} locale={{ emptyText: 'No time logged' }} scroll={{ x: 800 }}
    columns={[{ title: 'Date', dataIndex: 'date', render: (d: string) => dayjs(d).format('ddd DD MMM') },
      ...(showPerson ? [{ title: 'Person', dataIndex: 'employeeName' }] : []),
      { title: 'Project / task', render: (_, e) => <><Tag>{e.projectCode}</Tag>{e.taskKey && <Typography.Text type="secondary">{e.taskKey} </Typography.Text>}{e.taskTitle}</> },
      { title: 'Description', dataIndex: 'description' },
      { title: 'Hours', align: 'right', render: (_, e) => <>{+e.hours.toFixed(2)}{!e.billable && <Tag style={{ marginLeft: 6 }}>Non-billable</Tag>}</> },
      { title: 'Status', render: (_, e) => <><Tag color={ENTRY_COLORS[e.status]}>{e.status}</Tag>{e.rejectReason && <div><Typography.Text type="danger" style={{ fontSize: 12 }}>{e.rejectReason}</Typography.Text></div>}</> },
      ...(extra ? [{ key: 'x', render: (_: unknown, e: TimeEntry) => extra(e) }] : [])]} />
)

// ======================= My timesheet =======================

export function MyTimesheetPage() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [date, setDate] = useState(dayjs())
  const [editing, setEditing] = useState<TimeEntry | 'new' | null>(null)
  const { data, isLoading } = useQuery({ queryKey: ['prj-week', d8(date)], queryFn: async () => (await api.get<MyWeek>('/timesheets/me', { params: { date: d8(date) } })).data })
  const submit = async () => {
    try { await api.post('/timesheets/me/submit', null, { params: { date: d8(date) } }); message.success('Week submitted for approval'); await refresh(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const remove = async (e: TimeEntry) => { try { await api.delete(`/timesheets/entries/${e.id}`); await refresh(qc) } catch (err) { message.error(errorMessage(err)) } }
  const days = data ? Array.from({ length: 7 }, (_, i) => dayjs(data.weekStart).add(i, 'day')) : []
  if (data && !data.employeeId) return <Alert type="info" showIcon title="Your login isn't linked to an employee record, so there's no timesheet to keep." />
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>My timesheet</Typography.Title>
        <Space wrap>
          <Button icon={<LeftOutlined />} onClick={() => setDate(d => d.subtract(7, 'day'))} />
          <DatePicker picker="week" value={date} onChange={d => d && setDate(d)} format={() => data ? `${dayjs(data.weekStart).format('DD MMM')} – ${dayjs(data.weekEnd).format('DD MMM YYYY')}` : ''} allowClear={false} />
          <Button icon={<RightOutlined />} disabled={date.add(7, 'day').isAfter(dayjs(), 'week')} onClick={() => setDate(d => d.add(7, 'day'))} />
          <Button icon={<PlusOutlined />} disabled={!data?.projects.length} onClick={() => setEditing('new')}>Log time</Button>
          <Popconfirm title="Submit this week for approval? Entries can't be changed afterwards unless rejected." onConfirm={submit} disabled={!data?.canSubmit}>
            <Button type="primary" icon={<SendOutlined />} disabled={!data?.canSubmit}>Submit week</Button>
          </Popconfirm>
        </Space>
      </div>
      {data && !data.projects.length && <Alert type="info" showIcon style={{ marginBottom: 16 }} title="You aren't on any active project team yet. Ask your project manager to add you." />}
      <Row gutter={[8, 8]} style={{ marginBottom: 16 }}>
        {days.map(d => {
          const h = data!.entries.filter(e => e.date === d8(d) && e.status !== 'Rejected').reduce((s, e) => s + e.hours, 0)
          const weekend = d.day() === 0 || d.day() === 6
          return <Col key={d8(d)} flex="1 0 90px"><Card size="small" loading={isLoading} style={{ textAlign: 'center', opacity: weekend && !h ? 0.6 : 1 }}>
            <Typography.Text type="secondary">{d.format('ddd DD')}</Typography.Text>
            <div style={{ fontSize: 20, fontWeight: 600, color: h > 8 ? '#fa8c16' : undefined }}>{+h.toFixed(2)}</div></Card></Col>
        })}
        <Col flex="1 0 130px"><Card size="small" loading={isLoading} style={{ textAlign: 'center' }}>
          <Typography.Text type="secondary">Week</Typography.Text><div style={{ fontSize: 20, fontWeight: 600 }}>{+(data?.totalHours ?? 0).toFixed(2)} h</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>{+(data?.billableHours ?? 0).toFixed(2)} billable</Typography.Text></Card></Col>
      </Row>
      <Card>
        <TimeTable rows={data?.entries} loading={isLoading} extra={e => ['Draft', 'Rejected'].includes(e.status) && <Space>
          <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(e)} />
          <Popconfirm title="Delete this entry?" onConfirm={() => remove(e)}><Button size="small" danger icon={<DeleteOutlined />} /></Popconfirm></Space>} />
      </Card>
      {editing && data && <EntryModal week={data} entry={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function EntryModal({ week, entry, onClose }: { week: MyWeek; entry?: TimeEntry; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const projectId = Form.useWatch('projectId', form)
  const project = week.projects.find(p => p.id === projectId)
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, date: d8(v.date), billable: project?.billable ? v.billable : false }
      if (entry) await api.put(`/timesheets/entries/${entry.id}`, body); else await api.post('/timesheets/entries', body)
      message.success('Time saved'); await refresh(qc); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  const defaultDate = dayjs().isBefore(dayjs(week.weekEnd)) ? dayjs() : dayjs(week.weekEnd)
  return (
    <Modal open title={entry ? 'Edit time' : 'Log time'} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={entry ? { ...entry, date: dayjs(entry.date) } : { date: defaultDate, hours: 1, billable: true, projectId: week.projects.length === 1 ? week.projects[0].id : undefined }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="date" label="Date" rules={[{ required: true }]}>
            <DatePicker style={{ width: '100%' }} format="ddd DD MMM" disabledDate={d => d.isBefore(dayjs(week.weekStart), 'day') || d.isAfter(dayjs(week.weekEnd), 'day') || d.isAfter(dayjs(), 'day')} /></Form.Item></Col>
          <Col span={12}><Form.Item name="hours" label="Hours" rules={[{ required: true }]} extra="Quarter-hour steps"><InputNumber min={0.25} max={24} step={0.25} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={24}><Form.Item name="projectId" label="Project" rules={[{ required: true }]}><Select options={week.projects.map(p => ({ value: p.id, label: `${p.code} · ${p.name}` }))} onChange={() => form.setFieldValue('taskId', undefined)} /></Form.Item></Col>
          <Col span={24}><Form.Item name="taskId" label="Task"><Select allowClear options={project?.tasks.map(t => ({ value: t.id, label: `${t.key} · ${t.title}` }))} placeholder="Optional" /></Form.Item></Col>
          <Col span={24}><Form.Item name="description" label="What did you work on?"><Input.TextArea rows={2} /></Form.Item></Col>
          {project?.billable && <Col span={24}><Form.Item name="billable" valuePropName="checked"><Checkbox>Billable to the client</Checkbox></Form.Item></Col>}
        </Row>
      </Form>
    </Modal>
  )
}

// ======================= Approvals =======================

export function TimeApprovalsPage() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [selected, setSelected] = useState<string[]>([])
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const { data, isFetching } = useQuery({ queryKey: ['prj-approvals'], queryFn: async () => (await api.get<TimeEntry[]>('/timesheets/approvals')).data })
  const decide = async (approve: boolean) => {
    try {
      const n = (await api.post<number>(approve ? '/timesheets/approve' : '/timesheets/reject', { entryIds: selected, reason })).data
      message.success(`${n} entr${n === 1 ? 'y' : 'ies'} ${approve ? 'approved' : 'rejected'}`); setSelected([]); setRejecting(false); setReason(''); await refresh(qc)
    } catch (e) { message.error(errorMessage(e)) }
  }
  const total = (data ?? []).filter(e => selected.includes(e.id)).reduce((s, e) => s + e.hours, 0)
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Timesheet approvals</Typography.Title>
        <Space>
          <Button icon={<CloseOutlined />} danger disabled={!selected.length} onClick={() => setRejecting(true)}>Reject</Button>
          <Button type="primary" icon={<CheckOutlined />} disabled={!selected.length} onClick={() => decide(true)}>Approve {selected.length ? `(${+total.toFixed(2)} h)` : ''}</Button>
        </Space>
      </div>
      <Card>
        {data && !data.length ? <Empty description="Nothing waiting for your approval" /> :
          <Table<TimeEntry> size="small" rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 900 }}
            rowSelection={{ selectedRowKeys: selected, onChange: k => setSelected(k as string[]) }}
            columns={[{ title: 'Person', dataIndex: 'employeeName' }, { title: 'Date', dataIndex: 'date', render: (d: string) => dayjs(d).format('ddd DD MMM') },
              { title: 'Project / task', render: (_, e) => <><Tag>{e.projectCode}</Tag>{e.taskKey && <Typography.Text type="secondary">{e.taskKey} </Typography.Text>}{e.taskTitle}</> },
              { title: 'Description', dataIndex: 'description' },
              { title: 'Hours', align: 'right', render: (_, e) => <>{+e.hours.toFixed(2)}{!e.billable && <Tag style={{ marginLeft: 6 }}>Non-billable</Tag>}</> },
              { title: 'Value', align: 'right', render: (_, e) => e.billable ? amount(e.hours * e.billRate, 0) : '—' }]} />}
      </Card>
      <Modal open={rejecting} title={`Reject ${selected.length} entr${selected.length === 1 ? 'y' : 'ies'}`} onCancel={() => setRejecting(false)} onOk={() => decide(false)} okButtonProps={{ danger: true, disabled: !reason.trim() }} okText="Reject">
        <Input.TextArea rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder="Why? The person sees this and can correct the entry." />
      </Modal>
    </>
  )
}

// ======================= Clients =======================

export function ClientsPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data, isFetching } = useClients()
  const [editing, setEditing] = useState<Client | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (c: Client | 'new') => { setEditing(c); form.resetFields(); form.setFieldsValue(c === 'new' ? { paymentTermsDays: 30 } : { ...c, paymentTermsDays: 30 }) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      if (editing && editing !== 'new') await api.put(`/projects/clients/${editing.id}`, v); else await api.post('/projects/clients', v)
      message.success('Client saved'); await qc.invalidateQueries({ queryKey: ['prj-clients'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Clients</Typography.Title>
        {can('projects.clients.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => open('new')}>Add client</Button>}
      </div>
      <Card>
        <Table<Client> rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 700 }}
          onRow={c => ({ onClick: () => can('projects.clients.edit') && open(c), style: { cursor: 'pointer' } })}
          columns={[{ title: 'Client', render: (_, c) => <><b>{c.name}</b><div><Typography.Text type="secondary">{c.code}{c.city ? ` · ${c.city}` : ''}</Typography.Text></div></> },
            { title: 'Contact', render: (_, c) => [c.email, c.phone].filter(Boolean).join(' · ') || '—' }, { title: 'NTN', dataIndex: 'ntn' },
            { title: 'Active projects', dataIndex: 'activeProjects' }, { title: 'Invoiced', align: 'right', render: (_, c) => amount(c.billed, 0) },
            { title: 'To invoice', align: 'right', render: (_, c) => c.unbilled ? amount(c.unbilled, 0) : '—' }]} />
      </Card>
      <Modal open={!!editing} title={editing === 'new' ? 'Add client' : 'Edit client'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={24}><Form.Item name="name" label="Company name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="email" label="Billing email"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="address" label="Address"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="city" label="City"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="country" label="Country"><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="ntn" label="NTN"><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="strn" label="STRN"><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="paymentTermsDays" label="Payment terms (days)"><InputNumber min={0} max={365} style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

// ======================= Utilization =======================

export function UtilizationPage() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('month'), dayjs()])
  const { data, isFetching, error } = useQuery({ queryKey: ['prj-util', d8(range[0]), d8(range[1])], retry: false,
    queryFn: async () => (await api.get<Utilization>('/projects/reports/utilization', { params: { from: d8(range[0]), to: d8(range[1]) } })).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Team utilization</Typography.Title>
        <DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} format="DD MMM YYYY" allowClear={false} />
      </div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Capacity', hrs(data?.capacity)], ['Hours logged', hrs(data?.hours)], ['Billable hours', hrs(data?.billableHours)], ['Utilization', `${data?.utilization ?? 0}%`]].map(([t, v]) => (
          <Col key={t} xs={12} md={6}><Card size="small" loading={isFetching}><Statistic title={t} value={v} /></Card></Col>))}
      </Row>
      <Card>
        <Table size="small" rowKey="employeeId" loading={isFetching} dataSource={data?.rows} pagination={false} scroll={{ x: 700 }}
          columns={[{ title: 'Person', render: (_, r) => <>{r.name}{r.designation && <Typography.Text type="secondary"> · {r.designation}</Typography.Text>}</> },
            { title: 'Capacity', align: 'right', render: (_, r) => hrs(r.capacity) }, { title: 'Logged', align: 'right', render: (_, r) => hrs(r.hours) },
            { title: 'Billable', align: 'right', render: (_, r) => hrs(r.billableHours) },
            { title: 'Utilization', width: 200, render: (_, r) => <Progress size="small" percent={Math.min(100, r.utilization)} format={() => `${r.utilization}%`}
              strokeColor={r.utilization >= 70 ? '#52c41a' : r.utilization >= 40 ? '#faad14' : '#ff4d4f'} /> },
            { title: 'Billable value', align: 'right', render: (_, r) => amount(r.billableValue, 0) }]} />
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 12 }}>
          Capacity is 8 hours per weekday employed in the period. Counts submitted, approved and invoiced time.
        </Typography.Text>
      </Card>
    </>
  )
}
