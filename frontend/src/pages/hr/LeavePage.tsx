import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Badge, Button, Card, Checkbox, Col, Drawer, Empty, Form, Input, InputNumber, Modal, Popconfirm, Row, Select, Space, Table,
  Tabs, Tag, Typography,
} from 'antd'
import { ArrowDownOutlined, ArrowUpOutlined, CheckOutlined, CloseOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../../api/client'
import type { PagedResult, User } from '../../api/types'
import { APPROVER_LABELS, type ApproverType, type LeaveRequest, type LeaveStatus, type LeaveStep, type LeaveType } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import { ApprovalSteps, LeaveStatusTag, leaveDates } from '../../components/HrWidgets'
import EntityPicker from '../../components/EntityPicker'

export default function LeavePage() {
  const { can } = useAuth()
  const { data: inbox = [] } = useQuery({ queryKey: ['leave-inbox'], queryFn: async () => (await api.get<LeaveRequest[]>('/hr/leave/inbox')).data })
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Leave</Typography.Title></div>
      <Tabs items={[
        { key: 'inbox', label: <Badge count={inbox.length} offset={[12, 0]} size="small">Approvals</Badge>, children: <Inbox requests={inbox} /> },
        ...(can(P.leaveView) ? [{ key: 'all', label: 'All requests', children: <AllRequests /> }] : []),
        ...(can(P.hrSettings) ? [
          { key: 'types', label: 'Leave types', children: <LeaveTypes /> },
          { key: 'workflow', label: 'Approval workflow', children: <Workflow /> },
        ] : []),
      ]} />
    </>
  )
}

function useDecide() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  return async (r: LeaveRequest, approve: boolean, comment?: string) => {
    try {
      await api.post(`/hr/leave/requests/${r.id}/decision`, { approve, comment })
      message.success(approve ? 'Approved' : 'Rejected')
      await Promise.all(['leave-inbox', 'leave-requests', 'me-hr'].map(k => qc.invalidateQueries({ queryKey: [k] })))
      return true
    } catch (e) { message.error(errorMessage(e)); return false }
  }
}

function Inbox({ requests }: { requests: LeaveRequest[] }) {
  const [open, setOpen] = useState<LeaveRequest | null>(null)
  if (!requests.length) return <Card><Empty description="Nothing waiting for your approval" /></Card>
  return (
    <>
      <Row gutter={[16, 16]}>
        {requests.map(r => (
          <Col key={r.id} xs={24} md={12} xl={8}>
            <Card hoverable onClick={() => setOpen(r)} title={r.employeeName} extra={<Tag>{r.employeeCode}</Tag>}>
              <div><b>{r.leaveType}</b>{!r.isPaid && <Tag style={{ marginLeft: 8 }}>Unpaid</Tag>}</div>
              <div>{leaveDates(r)} · {r.days} day(s)</div>
              <Typography.Text type="secondary">{r.entityName} · step: {r.approvals.find(a => a.status === 'Pending')?.stepName}</Typography.Text>
              {r.reason && <Typography.Paragraph ellipsis={{ rows: 2 }} style={{ marginTop: 8, marginBottom: 0 }}>“{r.reason}”</Typography.Paragraph>}
            </Card>
          </Col>
        ))}
      </Row>
      {open && <RequestDrawer request={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function RequestDrawer({ request, onClose }: { request: LeaveRequest; onClose: () => void }) {
  const decide = useDecide()
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const act = async (approve: boolean) => {
    setBusy(true)
    if (await decide(request, approve, comment || undefined)) onClose()
    setBusy(false)
  }
  return (
    <Drawer open onClose={onClose} title={`${request.employeeName} — ${request.leaveType}`} size={480}
      footer={request.canAct && (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Input.TextArea rows={2} placeholder="Comment (required when rejecting)" value={comment} onChange={e => setComment(e.target.value)} />
          <Space>
            <Button type="primary" icon={<CheckOutlined />} loading={busy} onClick={() => act(true)}>Approve</Button>
            <Button danger icon={<CloseOutlined />} loading={busy} onClick={() => act(false)}>Reject</Button>
          </Space>
        </Space>
      )}>
      <Space orientation="vertical" style={{ width: '100%' }}>
        <div><LeaveStatusTag status={request.status} /> {leaveDates(request)} · <b>{request.days}</b> working day(s){!request.isPaid && ' · unpaid'}</div>
        <Typography.Text type="secondary">{request.employeeCode} · {request.entityName}</Typography.Text>
        {request.reason && <Typography.Paragraph>“{request.reason}”</Typography.Paragraph>}
        <Typography.Title level={5}>Approval chain</Typography.Title>
        <ApprovalSteps request={request} />
      </Space>
    </Drawer>
  )
}

function AllRequests() {
  const [entityId, setEntityId] = useState<string>()
  const [status, setStatus] = useState<LeaveStatus>()
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<LeaveRequest | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['leave-requests', entityId, status, page],
    queryFn: async () => (await api.get<PagedResult<LeaveRequest>>('/hr/leave/requests', { params: { entityId, status, page, pageSize: 25 } })).data,
  })
  return (
    <Card>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={9}><EntityPicker value={entityId} onChange={v => { setEntityId(v); setPage(1) }} placeholder="All entities" /></Col>
        <Col xs={24} md={5}>
          <Select allowClear placeholder="Any status" style={{ width: '100%' }} value={status} onChange={v => { setStatus(v); setPage(1) }}
            options={['Pending', 'Approved', 'Rejected', 'Cancelled'].map(s => ({ value: s, label: s }))} />
        </Col>
      </Row>
      <Table<LeaveRequest> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
        onRow={r => ({ onClick: () => setOpen(r), style: { cursor: 'pointer' } })}
        pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
        columns={[
          { title: 'Employee', render: (_, r) => <><b>{r.employeeName}</b><div><Typography.Text type="secondary">{r.employeeCode} · {r.entityName}</Typography.Text></div></> },
          { title: 'Type', dataIndex: 'leaveType' },
          { title: 'Dates', render: (_, r) => leaveDates(r) },
          { title: 'Days', dataIndex: 'days' },
          { title: 'Status', render: (_, r) => <LeaveStatusTag status={r.status} /> },
          { title: 'Current step', render: (_, r) => r.status === 'Pending' ? r.approvals.find(a => a.status === 'Pending')?.stepName : '' },
        ]} />
      {open && <RequestDrawer request={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}

function LeaveTypes() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [editing, setEditing] = useState<LeaveType | 'new' | null>(null)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['leave-types'], queryFn: async () => (await api.get<LeaveType[]>('/hr/leave/types')).data })

  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/hr/leave/types', v)
      else if (editing) await api.put(`/hr/leave/types/${editing.id}`, v)
      message.success('Saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['leave-types'] })
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Card extra={<Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add type</Button>}>
      <Typography.Paragraph type="secondary">
        Defaults follow common Pakistani practice (14 annual, 10 casual, 8 sick). Check them against the Shops &amp; Establishments
        law of your province and your HR policy. Entitlement is prorated in the year an employee joins.
      </Typography.Paragraph>
      <Table<LeaveType> rowKey="id" loading={isLoading} dataSource={data} pagination={false}
        onRow={t => ({ onClick: () => setEditing(t), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name' },
          { title: 'Days / year', dataIndex: 'daysPerYear', render: (d: number) => d || 'No limit' },
          { title: 'Paid', dataIndex: 'isPaid', render: (p: boolean) => p ? 'Paid' : <Tag>Unpaid</Tag> },
          { title: 'Half day', dataIndex: 'allowHalfDay', render: (h: boolean) => h ? 'Yes' : 'No' },
          { title: 'Only for', dataIndex: 'onlyForGender' },
          { title: 'Active', dataIndex: 'isActive', render: (a: boolean) => a ? 'Yes' : <Tag>Inactive</Tag> },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'New leave type' : 'Edit leave type'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false}
          initialValues={editing === 'new' ? { isPaid: true, allowHalfDay: true, isActive: true, daysPerYear: 0 } : editing ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="daysPerYear" label="Days per year" extra="0 = no balance tracking"><InputNumber min={0} max={366} step={0.5} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="onlyForGender" label="Only for"><Select allowClear options={['Male', 'Female'].map(g => ({ value: g, label: g }))} /></Form.Item></Col>
            <Col span={8}><Form.Item name="isPaid" valuePropName="checked"><Checkbox>Paid</Checkbox></Form.Item></Col>
            <Col span={8}><Form.Item name="allowHalfDay" valuePropName="checked"><Checkbox>Half day allowed</Checkbox></Form.Item></Col>
            <Col span={8}><Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </Card>
  )
}

function Workflow() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [] } = useQuery({ queryKey: ['leave-workflow'], queryFn: async () => (await api.get<LeaveStep[]>('/hr/leave/workflow')).data })
  const { data: users } = useQuery({ queryKey: ['users', 'pick'], queryFn: async () => (await api.get<PagedResult<User>>('/users', { params: { pageSize: 200 } })).data })
  const [steps, setSteps] = useState<{ name: string; approverType: ApproverType; approverUserId?: string }[]>([])
  const [busy, setBusy] = useState(false)
  useEffect(() => setSteps(data.map(s => ({ name: s.name, approverType: s.approverType, approverUserId: s.approverUserId }))), [data])

  const update = (i: number, patch: Partial<(typeof steps)[number]>) => setSteps(s => s.map((x, j) => (j === i ? { ...x, ...patch } : x)))
  const move = (i: number, d: number) => setSteps(s => { const n = [...s]; [n[i], n[i + d]] = [n[i + d], n[i]]; return n })

  const save = async () => {
    setBusy(true)
    try {
      await api.put('/hr/leave/workflow', { steps })
      message.success('Workflow saved. New requests will follow it.')
      await qc.invalidateQueries({ queryKey: ['leave-workflow'] })
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Card extra={<Button type="primary" loading={busy} onClick={save}>Save workflow</Button>}>
      <Typography.Paragraph type="secondary">
        Every leave request passes these steps in order. A step is skipped when it has no approver (e.g. no line manager set) or
        the approver is the requester. Nobody can approve two steps of the same request, and if every step is skipped the request
        goes to HR, so leave is never approved automatically.
      </Typography.Paragraph>
      <Space orientation="vertical" style={{ width: '100%' }}>
        {steps.map((s, i) => (
          <Card size="small" key={i}>
            <Row gutter={[8, 8]} align="middle">
              <Col flex="40px"><Tag color="blue">{i + 1}</Tag></Col>
              <Col xs={24} md={6}><Input value={s.name} onChange={e => update(i, { name: e.target.value })} placeholder="Step name" /></Col>
              <Col xs={24} md={8}>
                <Select style={{ width: '100%' }} value={s.approverType} onChange={v => update(i, { approverType: v })}
                  options={Object.entries(APPROVER_LABELS).map(([value, label]) => ({ value, label }))} />
              </Col>
              {s.approverType === 'SpecificUser' && (
                <Col xs={24} md={5}>
                  <Select style={{ width: '100%' }} showSearch={{ optionFilterProp: 'label' }} value={s.approverUserId} onChange={v => update(i, { approverUserId: v })}
                    placeholder="Approver" options={users?.items.map(u => ({ value: u.id, label: u.fullName }))} />
                </Col>
              )}
              <Col flex="auto" style={{ textAlign: 'right' }}>
                <Space>
                  <Button size="small" icon={<ArrowUpOutlined />} disabled={i === 0} onClick={() => move(i, -1)} />
                  <Button size="small" icon={<ArrowDownOutlined />} disabled={i === steps.length - 1} onClick={() => move(i, 1)} />
                  <Popconfirm title="Remove step?" onConfirm={() => setSteps(st => st.filter((_, j) => j !== i))}>
                    <Button size="small" danger icon={<DeleteOutlined />} disabled={steps.length === 1} />
                  </Popconfirm>
                </Space>
              </Col>
            </Row>
          </Card>
        ))}
        {steps.length < 6 && (
          <Button icon={<PlusOutlined />} onClick={() => setSteps(s => [...s, { name: 'HR', approverType: 'HrPermission' }])}>Add step</Button>
        )}
      </Space>
    </Card>
  )
}
