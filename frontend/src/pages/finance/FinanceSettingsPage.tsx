import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Card, Checkbox, Col, DatePicker, Form, Input, InputNumber, Modal, Row, Select, Space, Table, Tabs, Tag, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { SUBTYPES_BY_TYPE, splitWords, type Account, type AccountType, type ExchangeRate, type FinanceSettings, type TaxRate } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import { useAuth } from '../../auth/AuthContext'
import { AccountSelect, useAccounts, useFinanceSettings, useTaxRates } from '../../components/FinancePickers'

export default function FinanceSettingsPage() {
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Accounting setup</Typography.Title></div>
      <Tabs items={[
        { key: 'coa', label: 'Chart of accounts', children: <ChartOfAccounts /> },
        { key: 'general', label: 'General & default accounts', children: <General /> },
        { key: 'tax', label: 'Sales tax rates', children: <TaxRates /> },
        { key: 'fx', label: 'Exchange rates', children: <Rates /> },
      ]} />
    </>
  )
}

type AccountNode = Account & { children?: AccountNode[] }

function ChartOfAccounts() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isLoading } = useAccounts()
  const [editing, setEditing] = useState<Account | 'new' | null>(null)
  const [form] = Form.useForm()
  const type = Form.useWatch('type', form) as AccountType | undefined
  const isGroup = Form.useWatch('isGroup', form) as boolean | undefined

  const tree = useMemo(() => {
    const map = new Map<string, AccountNode>(data.map(a => [a.id, { ...a }]))
    const roots: AccountNode[] = []
    map.forEach(n => {
      const p = n.parentId ? map.get(n.parentId) : undefined
      if (p) (p.children ??= []).push(n)
      else roots.push(n)
    })
    return roots
  }, [data])

  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/finance/accounts', v)
      else if (editing) await api.put(`/finance/accounts/${editing.id}`, v)
      message.success('Account saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['fin-accounts'] })
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Card extra={can('finance.accounts.create') && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add account</Button>}>
      <Table<AccountNode> rowKey="id" loading={isLoading} dataSource={tree} pagination={false} expandable={{ defaultExpandAllRows: true }} size="small"
        key={data.length}
        onRow={a => ({ onClick: () => can('finance.accounts.edit') && setEditing(a), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Account', render: (_, a) => <span style={{ fontWeight: a.isGroup ? 600 : 400 }}>{a.code} {a.name}</span> },
          { title: 'Type', dataIndex: 'type' },
          { title: 'Classification', render: (_, a) => a.isGroup ? <Tag>Group</Tag> : splitWords(a.subType) },
          { title: '', render: (_, a) => <Space>{a.currency && <Tag color="purple">{a.currency}</Tag>}{a.isSystem && <Tag color="blue">System</Tag>}{!a.isActive && <Tag>Inactive</Tag>}</Space> },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'New account' : 'Edit account'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { type: 'Expense', subType: 'OperatingExpense', isActive: true, isGroup: false } : editing ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}>
              <Form.Item name="type" label="Type" rules={[{ required: true }]}>
                <Select onChange={(t: AccountType) => form.setFieldsValue({ subType: SUBTYPES_BY_TYPE[t][0], parentId: undefined })}
                  options={(['Asset', 'Liability', 'Equity', 'Income', 'Expense'] as AccountType[]).map(t => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="subType" label="Classification" hidden={isGroup}>
                <Select options={(type ? SUBTYPES_BY_TYPE[type] : []).map(s => ({ value: s, label: splitWords(s) }))} />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item name="parentId" label="Under group">
                <Select allowClear options={data.filter(a => a.isGroup && a.type === type).map(a => ({ value: a.id, label: `${a.code} ${a.name}` }))} />
              </Form.Item>
            </Col>
            <Col span={12}><Form.Item name="currency" label="Currency (bank/cash only)" extra="e.g. USD for a foreign currency account"><Input maxLength={3} /></Form.Item></Col>
            <Col span={12} style={{ paddingTop: 30 }}>
              <Form.Item name="isGroup" valuePropName="checked" noStyle><Checkbox>Group (heading)</Checkbox></Form.Item>
              <Form.Item name="isActive" valuePropName="checked" noStyle><Checkbox>Active</Checkbox></Form.Item>
            </Col>
            <Col span={24}><Form.Item name="description" label="Description"><Input /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </Card>
  )
}

const DEFAULTS: [keyof FinanceSettings, string, Parameters<typeof AccountSelect>[0]['types']][] = [
  ['receivableAccountId', 'Trade receivables', ['Asset']], ['payableAccountId', 'Trade payables', ['Liability']],
  ['retainedEarningsAccountId', 'Retained earnings', ['Equity']], ['exchangeGainLossAccountId', 'Exchange gain / loss', ['Income', 'Expense']],
  ['salaryExpenseAccountId', 'Salaries expense', ['Expense']], ['salaryPayableAccountId', 'Salaries payable', ['Liability']],
  ['salaryTaxPayableAccountId', 'Income tax withheld on salaries', ['Liability']], ['eobiExpenseAccountId', 'EOBI expense', ['Expense']],
  ['eobiPayableAccountId', 'EOBI payable', ['Liability']], ['pfExpenseAccountId', 'Provident fund expense', ['Expense']],
  ['pfPayableAccountId', 'Provident fund payable', ['Liability']], ['socialSecurityExpenseAccountId', 'Social security expense', ['Expense']],
  ['socialSecurityPayableAccountId', 'Social security payable', ['Liability']], ['otherPayrollDeductionsAccountId', 'Other payroll deductions', ['Liability', 'Asset']],
]

function General() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data } = useFinanceSettings()
  const [form] = Form.useForm()
  useEffect(() => { if (data) form.setFieldsValue({ ...data, lockedThrough: data.lockedThrough ? dayjs(data.lockedThrough) : null }) }, [data, form])
  const save = async () => {
    const v = await form.validateFields()
    try {
      await api.put('/finance/settings', { ...data, ...v, lockedThrough: v.lockedThrough ? v.lockedThrough.format('YYYY-MM-DD') : null })
      message.success('Settings saved')
      await qc.invalidateQueries({ queryKey: ['fin-settings'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const editable = can('finance.settings.manage')
  return (
    <Card extra={editable && <Button type="primary" onClick={save}>Save</Button>}>
      <Form form={form} layout="vertical" disabled={!editable}>
        <Row gutter={12}>
          <Col xs={12} md={4}><Form.Item name="baseCurrency" label="Base currency" extra="Fixed once posted"><Input maxLength={3} /></Form.Item></Col>
          <Col xs={12} md={5}>
            <Form.Item name="fiscalYearStartMonth" label="Fiscal year starts">
              <Select options={Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: dayjs().month(i).format('MMMM') }))} />
            </Form.Item>
          </Col>
          <Col xs={12} md={5}><Form.Item name="lockedThrough" label="Books closed through" extra="No postings on or before"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="ntn" label="Organization NTN"><Input /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="strn" label="STRN (on tax invoices)"><Input /></Form.Item></Col>
        </Row>
        <Typography.Title level={5}>Default posting accounts</Typography.Title>
        <Typography.Paragraph type="secondary">Used by invoices, bills, payments and payroll when they post automatically.</Typography.Paragraph>
        <Row gutter={12}>
          {DEFAULTS.map(([key, label, types]) => (
            <Col xs={24} md={12} key={key}><Form.Item name={key} label={label}><AccountSelect types={types} /></Form.Item></Col>
          ))}
        </Row>
      </Form>
    </Card>
  )
}

function TaxRates() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isLoading } = useTaxRates()
  const [editing, setEditing] = useState<TaxRate | 'new' | null>(null)
  const [form] = Form.useForm()
  const save = async () => {
    const v = await form.validateFields()
    const body = { ...v, rate: v.ratePct / 100 }
    try {
      if (editing === 'new') await api.post('/finance/tax-rates', body)
      else if (editing) await api.put(`/finance/tax-rates/${editing.id}`, body)
      message.success('Saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['fin-tax-rates'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <Alert type="warning" showIcon style={{ marginBottom: 16 }}
        title="Check rates against current notifications before filing."
        description="Federal GST is 18% on goods. Provincial sales tax on services (PRA, SRB, KPRA, BRA, ICT) is typically 13–16% and varies by service category — the seeded rates are common headline rates." />
      <Card extra={can('finance.settings.manage') && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add rate</Button>}>
        <Table<TaxRate> rowKey="id" loading={isLoading} dataSource={data} pagination={false}
          onRow={t => ({ onClick: () => can('finance.settings.manage') && setEditing(t), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Code', dataIndex: 'code' },
            { title: 'Name', dataIndex: 'name' },
            { title: 'Authority', dataIndex: 'authority' },
            { title: 'Rate', dataIndex: 'rate', render: (r: number) => `${+(r * 100).toFixed(2)}%` },
            { title: 'Active', dataIndex: 'isActive', render: (a: boolean) => a ? 'Yes' : <Tag>No</Tag> },
          ]} />
      </Card>
      <Modal open={!!editing} title={editing === 'new' ? 'New tax rate' : 'Edit tax rate'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { isActive: true, ratePct: 18 } : editing ? { ...editing, ratePct: +(editing.rate * 100).toFixed(4) } : {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="ratePct" label="Rate" rules={[{ required: true }]} extra="A rate used on documents can't change; add a new one."><InputNumber min={0} max={100} addonAfter="%" style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="authority" label="Authority"><Input placeholder="FBR, PRA, SRB…" /></Form.Item></Col>
            <Col span={12}><Form.Item name="outputAccountId" label="Output tax (payable)" rules={[{ required: true }]}><AccountSelect types={['Liability']} /></Form.Item></Col>
            <Col span={12}><Form.Item name="inputAccountId" label="Input tax (recoverable)" rules={[{ required: true }]}><AccountSelect types={['Asset']} /></Form.Item></Col>
            <Col span={24}><Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

function Rates() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: settings } = useFinanceSettings()
  const { data = [], isLoading } = useQuery({ queryKey: ['fin-rates'], queryFn: async () => (await api.get<ExchangeRate[]>('/finance/exchange-rates')).data })
  const [form] = Form.useForm()
  const add = async (v: { currency: string; date: dayjs.Dayjs; rate: number }) => {
    try {
      await api.put('/finance/exchange-rates', { currency: v.currency, date: v.date.format('YYYY-MM-DD'), rate: v.rate })
      message.success('Rate saved')
      form.resetFields(['rate'])
      await qc.invalidateQueries({ queryKey: ['fin-rates'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Card>
      <Typography.Paragraph type="secondary">
        {settings?.baseCurrency} per one unit of the foreign currency (e.g. USD 280.50). Documents use the latest rate on or before their date unless a rate is typed on the document.
      </Typography.Paragraph>
      {can('finance.settings.manage') && (
        <Form form={form} layout="inline" onFinish={add} initialValues={{ date: dayjs(), currency: 'USD' }} style={{ marginBottom: 16, rowGap: 8 }}>
          <Form.Item name="currency" rules={[{ required: true, len: 3 }]}><Input placeholder="USD" style={{ width: 80 }} maxLength={3} /></Form.Item>
          <Form.Item name="date" rules={[{ required: true }]}><DatePicker format="DD MMM YYYY" /></Form.Item>
          <Form.Item name="rate" rules={[{ required: true }]}><InputNumber min={0} step={0.01} placeholder="Rate" style={{ width: 140 }} /></Form.Item>
          <Button htmlType="submit" icon={<PlusOutlined />}>Save rate</Button>
        </Form>
      )}
      <Table<ExchangeRate> rowKey="id" size="small" loading={isLoading} dataSource={data}
        columns={[
          { title: 'Currency', dataIndex: 'currency' },
          { title: 'Date', dataIndex: 'date', render: fmtDate },
          { title: `${settings?.baseCurrency ?? ''} per unit`, dataIndex: 'rate', align: 'right' },
        ]} />
    </Card>
  )
}
