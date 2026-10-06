import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, Collapse, Form, Input, InputNumber, Modal, Popconfirm, Radio, Row, Select, Space,
  Statistic, Steps, Table, Tag, Typography,
} from 'antd'
import { ArrowLeftOutlined, CheckOutlined, DeleteOutlined, DownloadOutlined, PlusOutlined, ReloadOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { RUN_COLORS, money, monthName, type Payslip, type PayrollAdjustment, type PayrollRunDetail } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import { PayslipModal } from '../../components/HrWidgets'
import { JournalView, PaySalariesModal } from '../finance/LedgerPages'

export default function PayrollRunDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [slip, setSlip] = useState<Payslip | null>(null)
  const [adjusting, setAdjusting] = useState(false)
  const [busy, setBusy] = useState(false)
  const [journal, setJournal] = useState<string | null>(null)
  const [paying, setPaying] = useState(false)
  const key = ['payroll-run', id]
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: async () => (await api.get<PayrollRunDetail>(`/payroll/runs/${id}`)).data })

  if (isLoading || !data) return <Card loading />
  const { run, payslips, adjustments } = data
  const draft = run.status === 'Draft'

  const action = async (path: string, done: string) => {
    setBusy(true)
    try {
      const res = await api.post<PayrollRunDetail>(`/payroll/runs/${id}/${path}`)
      qc.setQueryData(key, res.data)
      await qc.invalidateQueries({ queryKey: ['payroll-runs'] })
      message.success(done)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  const removeAdj = async (adjId: string) => {
    try { const res = await api.delete<PayrollRunDetail>(`/payroll/runs/${id}/adjustments/${adjId}`); qc.setQueryData(key, res.data) } catch (e) { message.error(errorMessage(e)) }
  }

  /** Bank transfer sheet (CSV) for posted runs. */
  const exportBankSheet = () => {
    const rows = [['Employee Code', 'Name', 'CNIC', 'Bank', 'IBAN', 'Net Pay'], ...payslips.map(p => [p.employeeCode, p.employeeName, p.cnic ?? '', p.bankName ?? '', p.iban ?? '', p.netPay])]
    const csv = rows.map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `bank-transfer-${run.year}-${String(run.month).padStart(2, '0')}.csv`
    a.click()
  }

  const stepIndex = { Draft: 0, Approved: 1, Posted: 2, Cancelled: 0 }[run.status]
  const empName = (eid: string) => payslips.find(p => p.employeeId === eid)?.employeeName ?? eid

  return (
    <>
      <div className="page-header">
        <Space wrap>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/payroll')} />
          <Typography.Title level={2}>{monthName(run.year, run.month)}</Typography.Title>
          <Tag color={RUN_COLORS[run.status]}>{run.status}</Tag>
          <Typography.Text type="secondary">{run.entityName}{run.includeSubEntities && ' + sub-entities'}</Typography.Text>
        </Space>
        <Space wrap>
          {draft && can(P.payrollCreate, run.entityId) && <Button icon={<ReloadOutlined />} loading={busy} onClick={() => action('recalculate', 'Recalculated')}>Recalculate</Button>}
          {draft && can(P.payrollApprove, run.entityId) && (
            <Popconfirm title="Approve this payroll?" description="Attendance and leave for these employees will be locked for the month." onConfirm={() => action('approve', 'Payroll approved')}>
              <Button type="primary" icon={<CheckOutlined />} loading={busy}>Approve</Button>
            </Popconfirm>
          )}
          {run.status === 'Approved' && can(P.payrollPost, run.entityId) && (
            <Popconfirm title="Post payroll?" description="Posting is final. Employees will see their payslips." onConfirm={() => action('post', 'Payroll posted')}>
              <Button type="primary" icon={<SendOutlined />} loading={busy}>Post</Button>
            </Popconfirm>
          )}
          {(run.status === 'Draft' || run.status === 'Approved') && can(P.payrollApprove, run.entityId) && (
            <Popconfirm title="Cancel this payroll run?" onConfirm={() => action('cancel', 'Run cancelled')}><Button danger icon={<StopOutlined />}>Cancel run</Button></Popconfirm>
          )}
          {run.status !== 'Cancelled' && <Button icon={<DownloadOutlined />} onClick={exportBankSheet}>Bank sheet</Button>}
          {run.journalEntryId && can('finance.journals.view', run.entityId) && <Button onClick={() => setJournal(run.journalEntryId!)}>Accrual journal</Button>}
          {run.journalEntryId && !run.paymentJournalEntryId && can('finance.payments.create', run.entityId) && <Button type="primary" onClick={() => setPaying(true)}>Record salary transfer</Button>}
          {run.paymentJournalEntryId && can('finance.journals.view', run.entityId) && <Button onClick={() => setJournal(run.paymentJournalEntryId!)}>Salary payment journal</Button>}
        </Space>
      </div>

      {run.status !== 'Cancelled' && (
        <Card style={{ marginBottom: 16 }}>
          <Steps current={stepIndex} size="small" items={[
            { title: 'Draft', content: run.createdByName && `${run.createdByName}, ${dayjs(run.createdAt).format('DD MMM HH:mm')}` },
            { title: 'Approved', content: run.approvedByName && `${run.approvedByName}, ${dayjs(run.approvedAt).format('DD MMM HH:mm')}` },
            { title: 'Posted', content: run.postedByName && `${run.postedByName}, ${dayjs(run.postedAt).format('DD MMM HH:mm')}` },
          ]} />
        </Card>
      )}

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}><Card><Statistic title="Employees" value={run.employeeCount} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Gross (PKR)" value={money(run.totalGross)} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Net pay (PKR)" value={money(run.totalNet)} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Employer contributions" value={money(run.totalEmployerContributions)} /></Card></Col>
      </Row>

      {run.warnings.length > 0 && (
        <Collapse style={{ marginBottom: 16 }} items={[{
          key: 'w', label: <Space><Tag color="orange">{run.warnings.length}</Tag>Warnings</Space>,
          children: <ul style={{ margin: 0, paddingLeft: 18 }}>{run.warnings.map((w, i) => <li key={i}>{w}</li>)}</ul>,
        }]} />
      )}

      {(draft || adjustments.length > 0) && (
        <Card title="One-off adjustments" style={{ marginBottom: 16 }} size="small"
          extra={draft && can(P.payrollCreate, run.entityId) && <Button size="small" icon={<PlusOutlined />} onClick={() => setAdjusting(true)}>Add bonus / deduction</Button>}>
          {adjustments.length === 0 ? <Typography.Text type="secondary">Bonuses, arrears or advance recoveries for this month only.</Typography.Text> : (
            <Table<PayrollAdjustment> size="small" rowKey="id" pagination={false} dataSource={adjustments}
              columns={[
                { title: 'Employee', render: (_, a) => empName(a.employeeId) },
                { title: 'Item', dataIndex: 'name' },
                { title: 'Type', render: (_, a) => <Tag color={a.kind === 'Earning' ? 'green' : 'red'}>{a.kind}{a.kind === 'Earning' && !a.isTaxable && ' (non-taxable)'}</Tag> },
                { title: 'PKR', dataIndex: 'amount', align: 'right', render: money },
                draft ? { key: 'x', align: 'right' as const, render: (_: unknown, a: PayrollAdjustment) => <Button size="small" danger icon={<DeleteOutlined />} onClick={() => removeAdj(a.id)} /> } : {},
              ]} />
          )}
        </Card>
      )}

      <Card title="Payslips">
        <Table<Payslip> rowKey="id" dataSource={payslips} pagination={false} scroll={{ x: 1000 }} size="small"
          onRow={p => ({ onClick: () => setSlip(p), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Employee', fixed: 'left', render: (_, p) => <><b>{p.employeeName}</b><div><Typography.Text type="secondary">{p.employeeCode}</Typography.Text></div></> },
            { title: 'Paid days', render: (_, p) => <>{p.payableDays}/{p.daysInMonth}{p.unpaidDays > 0 && <Tag color="orange" style={{ marginLeft: 6 }}>−{p.unpaidDays}</Tag>}</> },
            { title: 'Gross', align: 'right', render: (_, p) => money(p.grossEarnings) },
            { title: 'Taxable', align: 'right', render: (_, p) => money(p.taxableIncome) },
            { title: 'Income tax', align: 'right', render: (_, p) => money(p.incomeTax) },
            { title: 'Other deductions', align: 'right', render: (_, p) => money(p.totalDeductions - p.incomeTax) },
            { title: 'Net pay', align: 'right', render: (_, p) => <b>{money(p.netPay)}</b> },
            { title: 'IBAN', dataIndex: 'iban', render: (i?: string) => i ?? <Tag color="orange">missing</Tag> },
          ]}
          summary={() => (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={2}><b>Total</b></Table.Summary.Cell>
              <Table.Summary.Cell index={2} align="right"><b>{money(run.totalGross)}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={3} />
              <Table.Summary.Cell index={4} align="right"><b>{money(payslips.reduce((s, p) => s + p.incomeTax, 0))}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={5} />
              <Table.Summary.Cell index={6} align="right"><b>{money(run.totalNet)}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={7} />
            </Table.Summary.Row>
          )} />
      </Card>

      {slip && <PayslipModal slip={slip} onClose={() => setSlip(null)} />}
      {journal && <JournalView id={journal} onClose={() => setJournal(null)} onOpen={setJournal} />}
      {paying && <PaySalariesModal runId={run.id} onClose={() => setPaying(false)} onDone={() => qc.invalidateQueries({ queryKey: key })} />}
      {adjusting && <AdjustmentModal runId={run.id} payslips={payslips} onClose={() => setAdjusting(false)} onSaved={d => qc.setQueryData(key, d)} />}
      {run.status === 'Cancelled' && <Alert type="warning" showIcon style={{ marginTop: 16 }} title="This run was cancelled. You can start a new run for the same month." />}
    </>
  )
}

function AdjustmentModal({ runId, payslips, onClose, onSaved }: { runId: string; payslips: Payslip[]; onClose: () => void; onSaved: (d: PayrollRunDetail) => void }) {
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const kind = Form.useWatch('kind', form)
  const save = async () => {
    const v = await form.validateFields()
    try {
      const res = await api.post<PayrollRunDetail>(`/payroll/runs/${runId}/adjustments`, v)
      onSaved(res.data)
      message.success('Added and recalculated')
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title="Bonus or deduction" onCancel={onClose} onOk={save} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ kind: 'Earning', isTaxable: true }}>
        <Form.Item name="employeeId" label="Employee" rules={[{ required: true }]}>
          <Select showSearch={{ optionFilterProp: 'label' }} options={payslips.map(p => ({ value: p.employeeId, label: `${p.employeeName} (${p.employeeCode})` }))} />
        </Form.Item>
        <Form.Item name="kind"><Radio.Group optionType="button" options={[{ value: 'Earning', label: 'Earning (bonus, arrears)' }, { value: 'Deduction', label: 'Deduction (advance, fine)' }]} /></Form.Item>
        <Form.Item name="name" label="Description" rules={[{ required: true }]}><Input placeholder={kind === 'Deduction' ? 'Salary advance recovery' : 'Performance bonus'} /></Form.Item>
        <Form.Item name="amount" label="Amount (PKR)" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
        {kind === 'Earning' && <Form.Item name="isTaxable" valuePropName="checked"><Checkbox>Taxable</Checkbox></Form.Item>}
      </Form>
    </Modal>
  )
}
