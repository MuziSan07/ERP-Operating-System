import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { App, Button, Card, Checkbox, DatePicker, Form, Input, Modal, Select, Table, Tag, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { RUN_COLORS, money, monthName, type PayrollRun, type PayrollRunDetail } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'

export default function PayrollRunsPage() {
  const { can, me } = useAuth()
  const navigate = useNavigate()
  const { message } = App.useApp()
  const [year, setYear] = useState(dayjs().year())
  const [creating, setCreating] = useState(false)
  const [busy, setBusy] = useState(false)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['payroll-runs', year], queryFn: async () => (await api.get<PayrollRun[]>('/payroll/runs', { params: { year } })).data })

  const create = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const { data: run } = await api.post<PayrollRunDetail>('/payroll/runs', {
        entityId: v.entityId, year: v.month.year(), month: v.month.month() + 1, includeSubEntities: v.includeSubEntities, notes: v.notes,
      })
      navigate(`/payroll/runs/${run.run.id}`)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Payroll</Typography.Title>
        {can(P.payrollCreate) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Run payroll</Button>}
      </div>
      <Card title={<Select value={year} onChange={setYear} options={[-1, 0, 1].map(o => ({ value: dayjs().year() + o, label: dayjs().year() + o }))} />}>
        <Table<PayrollRun> rowKey="id" loading={isLoading} dataSource={data} scroll={{ x: 900 }} pagination={false}
          onRow={r => ({ onClick: () => navigate(`/payroll/runs/${r.id}`), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Month', render: (_, r) => <b>{monthName(r.year, r.month)}</b> },
            { title: 'Entity', render: (_, r) => <>{r.entityName}{r.includeSubEntities && <Typography.Text type="secondary"> + sub-entities</Typography.Text>}</> },
            { title: 'Status', dataIndex: 'status', render: (s: PayrollRun['status']) => <Tag color={RUN_COLORS[s]}>{s}</Tag> },
            { title: 'Employees', dataIndex: 'employeeCount' },
            { title: 'Gross', align: 'right', render: (_, r) => money(r.totalGross) },
            { title: 'Net pay', align: 'right', render: (_, r) => <b>{money(r.totalNet)}</b> },
            { title: 'Warnings', render: (_, r) => r.warnings.length ? <Tag color="orange">{r.warnings.length}</Tag> : null },
          ]} />
      </Card>
      <Modal open={creating} title="Run payroll" onCancel={() => setCreating(false)} onOk={create} okText="Calculate" confirmLoading={busy} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false}
          initialValues={{ month: dayjs().subtract(dayjs().date() < 20 ? 1 : 0, 'month'), includeSubEntities: true, entityId: me?.entities.find(e => e.permissions.includes(P.payrollCreate))?.id }}>
          <Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission={P.payrollCreate} /></Form.Item>
          <Form.Item name="month" label="Month" rules={[{ required: true }]}><DatePicker picker="month" style={{ width: '100%' }} format="MMMM YYYY" /></Form.Item>
          <Form.Item name="includeSubEntities" valuePropName="checked"><Checkbox>Include employees of sub-entities</Checkbox></Form.Item>
          <Form.Item name="notes" label="Notes"><Input /></Form.Item>
        </Form>
        <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
          Uses attendance, approved unpaid leave, current salaries and the FBR tax table for the month's tax year. You can review,
          add bonuses or deductions and recalculate before approval.
        </Typography.Paragraph>
      </Modal>
    </>
  )
}
