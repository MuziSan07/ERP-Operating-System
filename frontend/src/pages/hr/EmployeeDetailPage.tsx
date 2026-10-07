import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Button, Card, Col, DatePicker, Descriptions, Empty, Form, Input, InputNumber, Modal, Popconfirm, Row, Space, Table,
  Tabs, Tag, Typography,
} from 'antd'
import { ArrowLeftOutlined, CalendarOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import {
  fmtDate, money, monthName, toIsoDate, type Employee, type LeaveBalance, type MonthlyAttendance, type PayComponent, type Salary,
  type SalaryLine,
} from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import { AttendanceCalendar } from '../../components/HrWidgets'
import Attachments from '../../components/Attachments'
import { EmployeeDrawer } from './EmployeesPage'
import { ApplyLeaveModal } from './MyWorkspacePage'

export default function EmployeeDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [editing, setEditing] = useState(false)
  const { data: e, isLoading } = useQuery({ queryKey: ['employee', id], queryFn: async () => (await api.get<Employee>(`/hr/employees/${id}`)).data })

  if (isLoading || !e) return <Card loading />

  const remove = async () => {
    try {
      await api.delete(`/hr/employees/${e.id}`)
      message.success('Employee deleted')
      navigate('/hr/employees')
    } catch (err) { message.error(errorMessage(err)) }
  }

  return (
    <>
      <div className="page-header">
        <Space wrap>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/hr/employees')} />
          <Typography.Title level={2}>{e.fullName}</Typography.Title>
          <Tag>{e.employeeCode}</Tag>
          <Tag color={e.status === 'Active' ? 'green' : 'default'}>{e.status}</Tag>
        </Space>
        <Space>
          {can(P.employeesEdit, e.entityId) && <Button icon={<EditOutlined />} onClick={() => setEditing(true)}>Edit</Button>}
          {can(P.employeesDelete, e.entityId) && (
            <Popconfirm title="Delete this employee and their login?" description="Employees with payslips can't be deleted; set them to Resigned instead." onConfirm={remove}>
              <Button danger icon={<DeleteOutlined />} />
            </Popconfirm>
          )}
        </Space>
      </div>
      <Tabs items={[
        {
          key: 'profile', label: 'Profile', children: (
            <Card>
              <Descriptions column={{ xs: 1, md: 2, xl: 3 }} size="small" bordered>
                <Descriptions.Item label="Email / login">{e.email}</Descriptions.Item>
                <Descriptions.Item label="User type">{e.userType}</Descriptions.Item>
                <Descriptions.Item label="Phone">{e.phone ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Entity">{e.entityName}</Descriptions.Item>
                <Descriptions.Item label="Department">{e.department ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Designation">{e.designation ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Line manager">{e.managerName ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Employment">{e.employmentType}</Descriptions.Item>
                <Descriptions.Item label="Joined">{fmtDate(e.joinDate)}</Descriptions.Item>
                <Descriptions.Item label="Confirmed">{fmtDate(e.confirmationDate)}</Descriptions.Item>
                {e.exitDate && <Descriptions.Item label="Exit">{fmtDate(e.exitDate)}</Descriptions.Item>}
                <Descriptions.Item label="Father's name">{e.fatherName ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="CNIC">{e.cnic ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Gender">{e.gender ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Date of birth">{fmtDate(e.dateOfBirth)}</Descriptions.Item>
                <Descriptions.Item label="Address">{[e.address, e.city].filter(Boolean).join(', ') || '—'}</Descriptions.Item>
                <Descriptions.Item label="Emergency">{[e.emergencyContactName, e.emergencyContactPhone].filter(Boolean).join(' · ') || '—'}</Descriptions.Item>
                <Descriptions.Item label="Bank">{[e.bankName, e.bankAccountTitle].filter(Boolean).join(' · ') || '—'}</Descriptions.Item>
                <Descriptions.Item label="IBAN">{e.iban ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="NTN">{e.ntn ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Statutory">
                  <Space wrap>{e.eobiMember && <Tag>EOBI {e.eobiNumber}</Tag>}{e.providentFundMember && <Tag>PF</Tag>}{e.socialSecurityMember && <Tag>Social security</Tag>}</Space>
                </Descriptions.Item>
              </Descriptions>
            </Card>
          ),
        },
        ...(can(P.salaryView, e.entityId) ? [{ key: 'salary', label: 'Salary', children: <SalaryTab employee={e} /> }] : []),
        { key: 'leave', label: 'Leave', children: <LeaveTab employee={e} /> },
        { key: 'attendance', label: 'Attendance', children: <AttendanceTab employeeId={e.id} /> },
        { key: 'documents', label: 'Documents', children: <Card><Attachments recordType="employee" recordId={e.id} canEdit={can(P.employeesEdit, e.entityId)} /></Card> },
      ]} />
      {editing && <EmployeeDrawer employee={e} onClose={() => setEditing(false)} />}
    </>
  )
}

function SalaryTab({ employee }: { employee: Employee }) {
  const { can } = useAuth()
  const [revising, setRevising] = useState(false)
  const { data = [], isLoading } = useQuery({ queryKey: ['salary', employee.id], queryFn: async () => (await api.get<Salary[]>(`/hr/employees/${employee.id}/salary`)).data })
  const current = data.find(s => !dayjs(s.effectiveFrom).isAfter(dayjs(), 'day')) ?? data[0]

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card title="Current salary" loading={isLoading}
          extra={can(P.salaryCreate, employee.entityId) && <Button icon={<PlusOutlined />} onClick={() => setRevising(true)}>Revise</Button>}>
          {current ? (
            <>
              <Typography.Text type="secondary">Effective {fmtDate(current.effectiveFrom)}</Typography.Text>
              <Table size="small" pagination={false} rowKey="code" style={{ marginTop: 8 }} dataSource={current.lines}
                columns={[
                  { title: 'Head', dataIndex: 'name' },
                  { title: 'Type', dataIndex: 'kind', render: (k: string) => <Tag color={k === 'Earning' ? 'green' : 'red'}>{k}</Tag> },
                  { title: 'Monthly PKR', dataIndex: 'amount', align: 'right', render: money },
                ]}
                summary={() => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={2}><b>Gross</b></Table.Summary.Cell><Table.Summary.Cell index={2} align="right"><b>{money(current.gross)}</b></Table.Summary.Cell></Table.Summary.Row>} />
            </>
          ) : <Empty description="No salary defined — this employee will be skipped in payroll" />}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card title="History">
          <Table size="small" pagination={false} rowKey="id" dataSource={data}
            columns={[
              { title: 'Effective from', dataIndex: 'effectiveFrom', render: fmtDate },
              { title: 'Gross', dataIndex: 'gross', align: 'right', render: money },
              { title: 'Remarks', dataIndex: 'remarks' },
            ]} />
        </Card>
      </Col>
      {revising && <SalaryModal employee={employee} current={current} onClose={() => setRevising(false)} />}
    </Row>
  )
}

function SalaryModal({ employee, current, onClose }: { employee: Employee; current?: Salary; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: components = [] } = useQuery({ queryKey: ['pay-components'], queryFn: async () => (await api.get<PayComponent[]>('/payroll/components')).data })
  const [amounts, setAmounts] = useState<Record<string, number>>(Object.fromEntries((current?.lines ?? []).map(l => [l.payComponentId, l.amount])))
  const [effective, setEffective] = useState(dayjs().startOf('month'))
  const [remarks, setRemarks] = useState<string>()
  const [gross, setGross] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const split = async () => {
    if (!gross) return
    const { data } = await api.get<SalaryLine[]>('/payroll/salary-split', { params: { gross } })
    setAmounts(prev => ({ ...Object.fromEntries(Object.entries(prev).filter(([id]) => components.find(c => c.id === id)?.kind === 'Deduction')), ...Object.fromEntries(data.map(l => [l.payComponentId, l.amount])) }))
  }
  const total = components.filter(c => c.kind === 'Earning').reduce((s, c) => s + (amounts[c.id] ?? 0), 0)

  const save = async () => {
    setBusy(true)
    try {
      await api.post(`/hr/employees/${employee.id}/salary`, {
        effectiveFrom: toIsoDate(effective), remarks,
        lines: Object.entries(amounts).filter(([, a]) => a > 0).map(([payComponentId, amount]) => ({ payComponentId, amount })),
      })
      message.success('Salary saved')
      await qc.invalidateQueries({ queryKey: ['salary', employee.id] })
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Modal open title={`Salary revision — ${employee.fullName}`} onCancel={onClose} onOk={save} confirmLoading={busy} width={620}>
      <Space wrap style={{ marginBottom: 12 }}>
        <DatePicker value={effective} onChange={v => v && setEffective(v)} format="DD MMM YYYY" allowClear={false} />
        <InputNumber placeholder="Gross to split" value={gross} onChange={setGross} min={0} step={5000} style={{ width: 160 }} />
        <Button onClick={split} disabled={!gross}>Split gross</Button>
      </Space>
      <Table size="small" pagination={false} rowKey="id" dataSource={components.filter(c => c.isActive)}
        columns={[
          { title: 'Head', dataIndex: 'name', render: (n, c) => <Space>{n}{c.isBasic && <Tag>Basic</Tag>}{!c.isTaxable && <Tag>Non-taxable</Tag>}</Space> },
          { title: 'Type', dataIndex: 'kind', render: (k: string) => <Tag color={k === 'Earning' ? 'green' : 'red'}>{k}</Tag> },
          {
            title: 'Monthly PKR', align: 'right', render: (_, c) => (
              <InputNumber min={0} value={amounts[c.id]} onChange={v => setAmounts(a => ({ ...a, [c.id]: v ?? 0 }))} style={{ width: 140 }} />
            ),
          },
        ]}
        summary={() => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={2}><b>Gross earnings</b></Table.Summary.Cell><Table.Summary.Cell index={2} align="right"><b>{money(total)}</b></Table.Summary.Cell></Table.Summary.Row>} />
      <Form.Item label="Remarks" style={{ marginTop: 12, marginBottom: 0 }}><Input value={remarks} onChange={e => setRemarks(e.target.value)} placeholder="e.g. Annual increment 10%" /></Form.Item>
    </Modal>
  )
}

function LeaveTab({ employee }: { employee: Employee }) {
  const { can } = useAuth()
  const [applying, setApplying] = useState(false)
  const year = dayjs().year()
  const { data = [], isLoading } = useQuery({
    queryKey: ['balances', employee.id, year], queryFn: async () => (await api.get<LeaveBalance[]>(`/hr/employees/${employee.id}/leave-balances`, { params: { year } })).data,
  })
  return (
    <Card title={`Leave balances ${year}`} loading={isLoading}
      extra={can(P.leaveEdit, employee.entityId) && <Button icon={<PlusOutlined />} onClick={() => setApplying(true)}>Record leave</Button>}>
      <Table size="small" pagination={false} rowKey="leaveTypeId" dataSource={data}
        columns={[
          { title: 'Type', dataIndex: 'name', render: (n, b) => <Space>{n}{!b.isPaid && <Tag>Unpaid</Tag>}</Space> },
          { title: 'Entitled', dataIndex: 'entitled' },
          { title: 'Used', dataIndex: 'used' },
          { title: 'Pending', dataIndex: 'pending' },
          { title: 'Available', dataIndex: 'available', render: (a?: number) => a ?? '—' },
        ]} />
      {applying && <ApplyLeaveModal employeeId={employee.id} onClose={() => setApplying(false)} />}
    </Card>
  )
}

function AttendanceTab({ employeeId }: { employeeId: string }) {
  const [month, setMonth] = useState(dayjs().startOf('month'))
  const { data } = useQuery({
    queryKey: ['attendance-month', employeeId, month.format('YYYY-MM')],
    queryFn: async () => (await api.get<MonthlyAttendance>(`/hr/employees/${employeeId}/attendance`, { params: { year: month.year(), month: month.month() + 1 } })).data,
  })
  return (
    <Card title={<Space><CalendarOutlined />{monthName(month.year(), month.month() + 1)}</Space>}
      extra={<DatePicker picker="month" value={month} onChange={v => v && setMonth(v)} allowClear={false} />}>
      <AttendanceCalendar data={data} />
    </Card>
  )
}
