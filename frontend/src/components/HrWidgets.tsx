import { Button, Card, Col, Descriptions, Empty, Modal, Row, Space, Steps, Table, Tag, Tooltip, Typography } from 'antd'
import { PrinterOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import {
  ATTENDANCE_COLORS, LEAVE_COLORS, fmtDate, money, monthName, type LeaveRequest, type MonthlyAttendance, type Payslip,
} from '../api/hr'

/** Printable payslip. Printing hides everything but the slip (see .print-area rules in index.css). */
export function PayslipModal({ slip, onClose }: { slip: Payslip; onClose: () => void }) {
  const earnings = slip.lines.filter(l => l.kind === 'Earning')
  const deductions = slip.lines.filter(l => l.kind === 'Deduction' && !l.isEmployerContribution)
  const employer = slip.lines.filter(l => l.isEmployerContribution)
  const rows = (lines: typeof earnings) => lines.map((l, i) => ({ key: i, name: l.name, amount: money(l.amount) }))
  const cols = [{ dataIndex: 'name', title: 'Head' }, { dataIndex: 'amount', title: 'PKR', align: 'right' as const }]

  return (
    <Modal open onCancel={onClose} width={760} title={`Payslip — ${monthName(slip.year, slip.month)}`}
      footer={<Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print / Save PDF</Button>}>
      <div className="print-area">
        <Typography.Title level={4} style={{ marginTop: 0 }}>{slip.entityName} — Salary slip for {monthName(slip.year, slip.month)}</Typography.Title>
        {slip.runStatus !== 'Posted' && <Tag color="orange">{slip.runStatus} — not final</Tag>}
        <Descriptions size="small" column={{ xs: 1, sm: 2 }} bordered style={{ margin: '12px 0' }}>
          <Descriptions.Item label="Employee">{slip.employeeName} ({slip.employeeCode})</Descriptions.Item>
          <Descriptions.Item label="Designation">{slip.designation ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Department">{slip.department ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="CNIC">{slip.cnic ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Bank">{slip.bankName ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="IBAN">{slip.iban ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Days">{slip.payableDays} paid of {slip.daysInMonth}{slip.unpaidDays > 0 && ` (${slip.unpaidDays} unpaid)`}</Descriptions.Item>
          <Descriptions.Item label="Monthly gross">{money(slip.monthlyGross)}</Descriptions.Item>
        </Descriptions>
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Table size="small" pagination={false} columns={cols} dataSource={rows(earnings)} title={() => <b>Earnings</b>}
              summary={() => <Table.Summary.Row><Table.Summary.Cell index={0}><b>Gross earnings</b></Table.Summary.Cell><Table.Summary.Cell index={1} align="right"><b>{money(slip.grossEarnings)}</b></Table.Summary.Cell></Table.Summary.Row>} />
          </Col>
          <Col xs={24} md={12}>
            <Table size="small" pagination={false} columns={cols} dataSource={rows(deductions)} title={() => <b>Deductions</b>}
              summary={() => <Table.Summary.Row><Table.Summary.Cell index={0}><b>Total deductions</b></Table.Summary.Cell><Table.Summary.Cell index={1} align="right"><b>{money(slip.totalDeductions)}</b></Table.Summary.Cell></Table.Summary.Row>} />
          </Col>
        </Row>
        <Card size="small" style={{ marginTop: 16, textAlign: 'center' }}>
          <Typography.Text type="secondary">Net pay</Typography.Text>
          <Typography.Title level={3} style={{ margin: 0 }}>PKR {money(slip.netPay)}</Typography.Title>
        </Card>
        <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 12 }}>
          Taxable income this month: {money(slip.taxableIncome)}. Income tax is withheld under section 149 on projected annual
          taxable income of {money(slip.projectedAnnualTaxable)} (annual tax {money(slip.projectedAnnualTax)}).
          {employer.length > 0 && <> Employer contributions (not deducted from pay): {employer.map(l => `${l.name} ${money(l.amount)}`).join(', ')}.</>}
        </Typography.Paragraph>
      </div>
    </Modal>
  )
}

/** Approval chain as a vertical timeline. */
export function ApprovalSteps({ request }: { request: LeaveRequest }) {
  const current = request.approvals.findIndex(a => a.status === 'Pending')
  return (
    <Steps size="small" orientation="vertical" current={current < 0 ? request.approvals.length : current}
      items={request.approvals.map(a => ({
        title: <Space>{a.stepName}{a.status !== 'Waiting' && a.status !== 'Pending' && <Tag color={a.status === 'Approved' ? 'green' : a.status === 'Rejected' ? 'red' : 'default'}>{a.status}</Tag>}</Space>,
        status: a.status === 'Approved' ? 'finish' : a.status === 'Rejected' ? 'error' : a.status === 'Pending' ? 'process' : 'wait',
        content: (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {a.status === 'Skipped' ? 'Skipped (no approver for this step)' : a.actedBy
              ? <>{a.actedBy}, {dayjs(a.actedAt).format('DD MMM HH:mm')}{a.comment && <> — “{a.comment}”</>}</>
              : `Waiting for ${a.approverName ?? '—'}`}
          </Typography.Text>
        ),
      }))} />
  )
}

export function LeaveStatusTag({ status }: { status: LeaveRequest['status'] }) {
  return <Tag color={LEAVE_COLORS[status]}>{status}</Tag>
}

export const leaveDates = (r: LeaveRequest) =>
  r.fromDate === r.toDate ? `${fmtDate(r.fromDate)}${r.isHalfDay ? ' (half day)' : ''}` : `${fmtDate(r.fromDate)} → ${fmtDate(r.toDate)}`

/** Month grid of one employee's attendance. */
export function AttendanceCalendar({ data }: { data?: MonthlyAttendance }) {
  if (!data) return <Empty />
  const first = dayjs(data.days[0]?.date)
  const blanks = (first.day() + 6) % 7 // Monday-first grid
  return (
    <>
      <Space wrap style={{ marginBottom: 12 }}>
        {Object.entries(data.summary).map(([k, v]) => <Tag key={k}>{k}: <b>{v}</b></Tag>)}
      </Space>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: 4 }}>
        {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map(d => <Typography.Text key={d} type="secondary" style={{ textAlign: 'center', fontSize: 12 }}>{d}</Typography.Text>)}
        {Array.from({ length: blanks }).map((_, i) => <div key={`b${i}`} />)}
        {data.days.map(d => {
          const label = d.leaveType ? 'Leave' : d.status ?? (d.dayType ? (d.dayType.startsWith('Holiday') ? 'Holiday' : d.dayType === 'Weekly off' ? 'Off' : '') : dayjs(d.date).isAfter(dayjs(), 'day') ? '' : 'Present')
          const color = d.leaveType ? 'purple' : d.status ? ATTENDANCE_COLORS[d.status] : label === 'Holiday' ? 'cyan' : label === 'Present' ? 'green' : undefined
          return (
            <Tooltip key={d.date} title={[d.dayType, d.leaveType && `Leave: ${d.leaveType}`, d.remarks].filter(Boolean).join(' · ') || undefined}>
              <Card size="small" styles={{ body: { padding: 6, minHeight: 52 } }}>
                <div style={{ fontSize: 12, fontWeight: 600 }}>{dayjs(d.date).date()}</div>
                {label && <Tag color={color} style={{ marginTop: 2, fontSize: 10, maxWidth: '100%', overflow: 'hidden' }}>{label}</Tag>}
              </Card>
            </Tooltip>
          )
        })}
      </div>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8 }}>Unmarked working days count as present.</Typography.Paragraph>
    </>
  )
}
