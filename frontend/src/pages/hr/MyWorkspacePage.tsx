import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Form, Input, Modal, Popconfirm, Progress, Row,
  Select, Space, Table, Tabs, Typography,
} from 'antd'
import { CalendarOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import {
  fmtDate, money, monthName, toIsoDate, type LeaveRequest, type LeaveType, type MonthlyAttendance, type MyHr, type Payslip,
} from '../../api/hr'
import { AttendanceCalendar, ApprovalSteps, LeaveStatusTag, PayslipModal, leaveDates } from '../../components/HrWidgets'
import { useAuth } from '../../auth/AuthContext'

export default function MyWorkspacePage() {
  const { me } = useAuth()
  const { data, isLoading } = useQuery({ queryKey: ['me-hr'], queryFn: async () => (await api.get<MyHr>('/me/hr')).data })
  const emp = data?.employee

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>My Workspace</Typography.Title>
      </div>
      {!isLoading && !emp && (
        <Alert type="info" showIcon title="Your login isn't linked to an employee record"
          description="Leave, attendance and payslips appear here once HR creates your employee profile." />
      )}
      {emp && (
        <Tabs items={[
          { key: 'leave', label: 'Leave', children: <MyLeave data={data!} /> },
          { key: 'attendance', label: 'Attendance', children: <MyAttendance employeeId={emp.id} /> },
          { key: 'payslips', label: 'Payslips', children: <MyPayslips /> },
          {
            key: 'profile', label: 'Profile', children: (
              <Card>
                <Descriptions column={{ xs: 1, md: 2 }} size="small" bordered>
                  <Descriptions.Item label="Name">{me?.user.fullName}</Descriptions.Item>
                  <Descriptions.Item label="Employee code">{emp.employeeCode}</Descriptions.Item>
                  <Descriptions.Item label="Designation">{emp.designation ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label="Department">{emp.department ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label="Entity">{emp.entityName}</Descriptions.Item>
                  <Descriptions.Item label="Line manager">{emp.managerName ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label="Joined">{fmtDate(emp.joinDate)}</Descriptions.Item>
                  <Descriptions.Item label="Employment">{emp.employmentType}</Descriptions.Item>
                  <Descriptions.Item label="Bank">{emp.bankName ?? '—'}</Descriptions.Item>
                  <Descriptions.Item label="IBAN">{emp.iban ?? '—'}</Descriptions.Item>
                </Descriptions>
                <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>To correct your details, contact HR.</Typography.Paragraph>
              </Card>
            ),
          },
        ]} />
      )}
    </>
  )
}

function MyLeave({ data }: { data: MyHr }) {
  const [applying, setApplying] = useState(false)
  const [viewing, setViewing] = useState<LeaveRequest | null>(null)
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const year = dayjs().year()
  const { data: requests = [], isLoading } = useQuery({
    queryKey: ['my-leave', year], queryFn: async () => (await api.get<LeaveRequest[]>('/me/leave', { params: { year } })).data,
  })
  const cancel = useMutation({
    mutationFn: (id: string) => api.post(`/hr/leave/requests/${id}/cancel`),
    onSuccess: () => { message.success('Request cancelled'); void qc.invalidateQueries({ queryKey: ['my-leave'] }); void qc.invalidateQueries({ queryKey: ['me-hr'] }) },
    onError: e => message.error(errorMessage(e)),
  })
  const canApply = data.employee && can('hr.leave.create', data.employee.entityId)

  return (
    <>
      <Row gutter={[16, 16]}>
        {data.balances.map(b => (
          <Col key={b.leaveTypeId} xs={12} md={8} lg={4}>
            <Card size="small">
              <Typography.Text type="secondary">{b.name}</Typography.Text>
              {b.available != null ? (
                <>
                  <Typography.Title level={3} style={{ margin: '4px 0' }}>{b.available}<Typography.Text type="secondary" style={{ fontSize: 14 }}> / {b.entitled}</Typography.Text></Typography.Title>
                  <Progress percent={b.entitled ? Math.round(((b.used + b.pending) / b.entitled) * 100) : 0} showInfo={false} size="small" />
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>{b.used} used{b.pending ? `, ${b.pending} pending` : ''}</Typography.Text>
                </>
              ) : <Typography.Title level={5} style={{ margin: '4px 0' }}>{b.used} days taken</Typography.Title>}
            </Card>
          </Col>
        ))}
      </Row>
      <Card style={{ marginTop: 16 }} title={`My requests ${year}`}
        extra={canApply && <Button type="primary" icon={<PlusOutlined />} onClick={() => setApplying(true)}>Apply for leave</Button>}>
        <Table<LeaveRequest> rowKey="id" loading={isLoading} dataSource={requests} pagination={false} scroll={{ x: 640 }}
          onRow={r => ({ onClick: () => setViewing(r), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Type', dataIndex: 'leaveType' },
            { title: 'Dates', render: (_, r) => leaveDates(r) },
            { title: 'Days', dataIndex: 'days' },
            { title: 'Status', render: (_, r) => <LeaveStatusTag status={r.status} /> },
            { title: 'Waiting for', render: (_, r) => r.status === 'Pending' ? r.approvals.find(a => a.status === 'Pending')?.approverName : '' },
            {
              key: 'x', align: 'right', render: (_, r) => (r.status === 'Pending' || (r.status === 'Approved' && dayjs(r.fromDate).isAfter(dayjs(), 'day'))) && (
                <Popconfirm title="Cancel this request?" onConfirm={e => { e?.stopPropagation(); cancel.mutate(r.id) }} onCancel={e => e?.stopPropagation()}>
                  <Button size="small" onClick={e => e.stopPropagation()}>Cancel</Button>
                </Popconfirm>
              ),
            },
          ]} />
      </Card>
      {applying && <ApplyLeaveModal onClose={() => setApplying(false)} />}
      {viewing && (
        <Drawer open onClose={() => setViewing(null)} title={`${viewing.leaveType}: ${leaveDates(viewing)}`}>
          <Space orientation="vertical" style={{ width: '100%' }}>
            <div><LeaveStatusTag status={viewing.status} /> {viewing.days} day(s){!viewing.isPaid && ' — unpaid'}</div>
            {viewing.reason && <Typography.Paragraph>“{viewing.reason}”</Typography.Paragraph>}
            <ApprovalSteps request={viewing} />
          </Space>
        </Drawer>
      )}
    </>
  )
}

/** Shared by self-service and HR (pass employeeId to apply on someone's behalf). */
export function ApplyLeaveModal({ onClose, employeeId }: { onClose: () => void; employeeId?: string }) {
  const [form] = Form.useForm()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)
  const { data: types = [] } = useQuery({ queryKey: ['leave-types'], queryFn: async () => (await api.get<LeaveType[]>('/hr/leave/types')).data })
  const typeId = Form.useWatch('leaveTypeId', form)
  const range = Form.useWatch('range', form) as [Dayjs, Dayjs] | undefined
  const selected = types.find(t => t.id === typeId)
  const singleDay = range && range[0].isSame(range[1], 'day')

  const submit = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      await api.post('/hr/leave/requests', {
        leaveTypeId: v.leaveTypeId, fromDate: toIsoDate(v.range[0]), toDate: toIsoDate(v.range[1]),
        isHalfDay: !!v.isHalfDay && singleDay, reason: v.reason, employeeId,
      })
      message.success('Leave request submitted for approval')
      await Promise.all(['my-leave', 'me-hr', 'leave-requests', 'leave-inbox'].map(k => qc.invalidateQueries({ queryKey: [k] })))
      onClose()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open title="Apply for leave" onCancel={onClose} onOk={submit} okText="Submit" confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false}>
        <Form.Item name="leaveTypeId" label="Leave type" rules={[{ required: true }]}>
          <Select options={types.filter(t => t.isActive).map(t => ({ value: t.id, label: `${t.name}${t.isPaid ? '' : ' (unpaid)'}` }))} />
        </Form.Item>
        <Form.Item name="range" label="Dates" rules={[{ required: true }]} extra="Weekly offs and holidays inside the range are not counted.">
          <DatePicker.RangePicker style={{ width: '100%' }} format="DD MMM YYYY" />
        </Form.Item>
        {selected?.allowHalfDay && singleDay && (
          <Form.Item name="isHalfDay" valuePropName="checked"><Checkbox>Half day</Checkbox></Form.Item>
        )}
        <Form.Item name="reason" label="Reason"><Input.TextArea rows={3} maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  )
}

function MyAttendance({ employeeId }: { employeeId: string }) {
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

function MyPayslips() {
  const [open, setOpen] = useState<Payslip | null>(null)
  const { data = [], isLoading } = useQuery({ queryKey: ['my-payslips'], queryFn: async () => (await api.get<Payslip[]>('/me/payslips')).data })
  return (
    <Card>
      <Table<Payslip> rowKey="id" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 560 }}
        locale={{ emptyText: 'No payslips yet. They appear here once payroll is posted.' }}
        columns={[
          { title: 'Month', render: (_, p) => monthName(p.year, p.month) },
          { title: 'Gross', align: 'right', render: (_, p) => money(p.grossEarnings) },
          { title: 'Deductions', align: 'right', render: (_, p) => money(p.totalDeductions) },
          { title: 'Net pay', align: 'right', render: (_, p) => <b>{money(p.netPay)}</b> },
          { key: 'v', align: 'right', render: (_, p) => <Button size="small" onClick={() => setOpen(p)}>View</Button> },
        ]} />
      {open && <PayslipModal slip={open} onClose={() => setOpen(null)} />}
    </Card>
  )
}
