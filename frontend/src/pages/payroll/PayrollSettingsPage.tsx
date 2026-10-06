import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, Descriptions, Form, Input, InputNumber, Modal, Row, Select, Space, Switch, Table, Tabs, Tag,
  Typography,
} from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../../api/client'
import { money, pct, type HrSettings, type PayComponent, type TaxPreview, type TaxSlab, type TaxYear } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

export default function PayrollSettingsPage() {
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>HR & Payroll Settings</Typography.Title></div>
      <Tabs items={[
        { key: 'statutory', label: 'Statutory & calendar', children: <Statutory /> },
        { key: 'tax', label: 'Income tax tables', children: <TaxTables /> },
        { key: 'components', label: 'Salary heads', children: <Components /> },
      ]} />
    </>
  )
}

function Statutory() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const { data } = useQuery({ queryKey: ['payroll-settings'], queryFn: async () => (await api.get<HrSettings>('/payroll/settings')).data })
  const [offs, setOffs] = useState<string[]>([])
  useEffect(() => {
    if (!data) return
    setOffs(data.weeklyOffDays)
    // Rates are stored as fractions; edit them as percentages.
    form.setFieldsValue({ ...data, eobiEmployeeRate: data.eobiEmployeeRate * 100, eobiEmployerRate: data.eobiEmployerRate * 100,
      providentFundEmployeeRate: data.providentFundEmployeeRate * 100, providentFundEmployerRate: data.providentFundEmployerRate * 100,
      socialSecurityEmployerRate: data.socialSecurityEmployerRate * 100 })
  }, [data, form])

  const savePayroll = async () => {
    const v = await form.validateFields()
    try {
      await api.put('/payroll/settings', { ...data, ...v, eobiEmployeeRate: v.eobiEmployeeRate / 100, eobiEmployerRate: v.eobiEmployerRate / 100,
        providentFundEmployeeRate: v.providentFundEmployeeRate / 100, providentFundEmployerRate: v.providentFundEmployerRate / 100,
        socialSecurityEmployerRate: v.socialSecurityEmployerRate / 100 })
      message.success('Payroll settings saved')
      await qc.invalidateQueries({ queryKey: ['payroll-settings'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const saveOffs = async () => {
    try { await api.put('/payroll/settings/weekly-offs', offs); message.success('Weekly offs saved'); await qc.invalidateQueries({ queryKey: ['payroll-settings'] }) } catch (e) { message.error(errorMessage(e)) }
  }
  const pctInput = <InputNumber min={0} max={100} step={0.01} addonAfter="%" style={{ width: '100%' }} />
  const editable = can(P.payrollSettings)

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={15}>
        <Card title="Statutory contributions" extra={editable && <Button type="primary" onClick={savePayroll}>Save</Button>}>
          <Form form={form} layout="vertical" disabled={!editable}>
            <Row gutter={12}>
              <Col xs={24} sm={12}>
                <Form.Item name="minimumWage" label="Minimum wage (PKR / month)" extra="Federal Rs 40,700 from July 2026; provinces notify their own. EOBI is calculated on this.">
                  <InputNumber min={1} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}><Form.Item name="payrollRequiresSecondApprover" valuePropName="checked" label="Controls"><Checkbox>Approver must differ from preparer</Checkbox></Form.Item></Col>
              <Col span={24}><Form.Item name="eobiEnabled" valuePropName="checked" noStyle><Checkbox><b>EOBI</b> (organizations with 5+ employees)</Checkbox></Form.Item></Col>
              <Col xs={12}><Form.Item name="eobiEmployeeRate" label="Employee share">{pctInput}</Form.Item></Col>
              <Col xs={12}><Form.Item name="eobiEmployerRate" label="Employer share">{pctInput}</Form.Item></Col>
              <Col span={24}><Form.Item name="providentFundEnabled" valuePropName="checked" noStyle><Checkbox><b>Provident fund</b> (% of basic)</Checkbox></Form.Item></Col>
              <Col xs={12}><Form.Item name="providentFundEmployeeRate" label="Employee">{pctInput}</Form.Item></Col>
              <Col xs={12}><Form.Item name="providentFundEmployerRate" label="Employer">{pctInput}</Form.Item></Col>
              <Col span={24}><Form.Item name="socialSecurityEnabled" valuePropName="checked" noStyle><Checkbox><b>Provincial social security</b> (PESSI / SESSI, employer only)</Checkbox></Form.Item></Col>
              <Col xs={12}><Form.Item name="socialSecurityEmployerRate" label="Employer rate">{pctInput}</Form.Item></Col>
              <Col xs={12}><Form.Item name="socialSecurityWageCeiling" label="Applies to wages up to (PKR)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            </Row>
          </Form>
        </Card>
      </Col>
      <Col xs={24} lg={9}>
        <Card title="Weekly off days" extra={can(P.hrSettings) && <Button onClick={saveOffs}>Save</Button>}>
          <Checkbox.Group value={offs} onChange={v => setOffs(v as string[])} disabled={!can(P.hrSettings)}
            options={DAYS.map(d => ({ value: d, label: d }))} style={{ display: 'grid', gap: 8 }} />
          <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>Used to count leave days and to skip days on the attendance sheet.</Typography.Paragraph>
        </Card>
      </Col>
    </Row>
  )
}

function TaxTables() {
  const { can } = useAuth()
  const { data = [], isLoading } = useQuery({ queryKey: ['tax-years'], queryFn: async () => (await api.get<TaxYear[]>('/payroll/tax-years')).data })
  const [editing, setEditing] = useState<TaxYear | 'new' | null>(null)
  return (
    <>
      <Alert type="warning" showIcon style={{ marginBottom: 16 }}
        title="Salaried income tax slabs change with each Finance Act (July). Check them against the FBR before running payroll in a new tax year."
        description="Tax year 2027 = July 2026 – June 2027. Seeded from the Finance Act 2026 as published; edit if the FBR notification differs." />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={15}>
          {isLoading ? <Card loading /> : data.map(t => (
            <Card key={t.id} style={{ marginBottom: 16 }}
              title={<Space>Tax year {t.year}<Typography.Text type="secondary">July {t.year - 1} – June {t.year}</Typography.Text></Space>}
              extra={can(P.payrollSettings) && <Button size="small" onClick={() => setEditing(t)}>Edit</Button>}>
              {t.notes && <Typography.Paragraph type="secondary">{t.notes}</Typography.Paragraph>}
              <SlabTable slabs={t.slabs} />
              {t.surchargeThreshold && <Typography.Paragraph style={{ marginTop: 8 }}>Surcharge: {pct(t.surchargeRate)} of tax when income exceeds {money(t.surchargeThreshold)}</Typography.Paragraph>}
            </Card>
          ))}
          {can(P.payrollSettings) && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add tax year</Button>}
        </Col>
        <Col xs={24} lg={9}><TaxCalculator years={data.map(t => t.year)} /></Col>
      </Row>
      {editing && <TaxYearModal table={editing === 'new' ? null : editing} latest={data[0]} onClose={() => setEditing(null)} />}
    </>
  )
}

function SlabTable({ slabs }: { slabs: TaxSlab[] }) {
  return (
    <Table size="small" pagination={false} rowKey="from" dataSource={slabs}
      columns={[
        { title: 'Annual taxable income (PKR)', render: (_, s) => s.to ? `${money(s.from)} – ${money(s.to)}` : `Above ${money(s.from)}` },
        { title: 'Tax', render: (_, s) => s.rate === 0 && s.fixedTax === 0 ? 'Nil' : `${s.fixedTax ? `${money(s.fixedTax)} + ` : ''}${pct(s.rate)} of amount over ${money(s.from)}` },
      ]} />
  )
}

function TaxCalculator({ years }: { years: number[] }) {
  const [year, setYear] = useState<number>()
  const [monthly, setMonthly] = useState<number | null>(150000)
  const y = year ?? years[0]
  const { data } = useQuery({
    queryKey: ['tax-preview', y, monthly], enabled: !!y && !!monthly,
    queryFn: async () => (await api.get<TaxPreview>('/payroll/tax-preview', { params: { taxYear: y, monthlyTaxable: monthly } })).data,
  })
  return (
    <Card title="Tax calculator">
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Select value={y} onChange={setYear} style={{ width: '100%' }} options={years.map(v => ({ value: v, label: `Tax year ${v}` }))} />
        <InputNumber value={monthly} onChange={setMonthly} min={0} step={10000} style={{ width: '100%' }} addonBefore="Monthly taxable" />
        {data && (
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Annual income">{money(data.annualIncome)}</Descriptions.Item>
            <Descriptions.Item label="Annual tax">{money(data.annualTax)}</Descriptions.Item>
            <Descriptions.Item label="Monthly tax">{money(data.monthlyTax)}</Descriptions.Item>
            <Descriptions.Item label="Effective rate">{pct(data.effectiveRate)}</Descriptions.Item>
          </Descriptions>
        )}
      </Space>
    </Card>
  )
}

function TaxYearModal({ table, latest, onClose }: { table: TaxYear | null; latest?: TaxYear; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const source = table ?? latest
  const [year, setYear] = useState<number>(table?.year ?? (latest ? latest.year + 1 : new Date().getFullYear() + 1))
  const [slabs, setSlabs] = useState<TaxSlab[]>((source?.slabs ?? [{ from: 0, to: null, fixedTax: 0, rate: 0 }]).map(s => ({ ...s })))
  const [notes, setNotes] = useState(table?.notes ?? '')
  const [surchargeOn, setSurchargeOn] = useState(!!table?.surchargeThreshold)
  const [threshold, setThreshold] = useState<number | null>(table?.surchargeThreshold ?? 10_000_000)
  const [surcharge, setSurcharge] = useState<number | null>((table?.surchargeRate ?? 0.09) * 100)

  const setSlab = (i: number, patch: Partial<TaxSlab>) => setSlabs(s => s.map((x, j) => (j === i ? { ...x, ...patch } : x)))
  // Keep slabs contiguous: each slab ends where the next begins.
  const normalized = slabs.map((s, i) => ({ ...s, to: i + 1 < slabs.length ? slabs[i + 1].from : null }))

  const save = async () => {
    try {
      await api.put('/payroll/tax-years', {
        year, notes, slabs: normalized,
        surchargeThreshold: surchargeOn ? threshold : null, surchargeRate: surchargeOn ? (surcharge ?? 0) / 100 : 0,
      })
      message.success(`Tax year ${year} saved`)
      await qc.invalidateQueries({ queryKey: ['tax-years'] })
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Modal open width={760} title={table ? `Edit tax year ${table.year}` : 'New tax year'} onCancel={onClose} onOk={save}>
      <Space wrap style={{ marginBottom: 12 }}>
        <InputNumber value={year} onChange={v => v && setYear(v)} disabled={!!table} addonBefore="Tax year" />
        <Typography.Text type="secondary">July {year - 1} – June {year}</Typography.Text>
      </Space>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={normalized}
        columns={[
          { title: 'From (PKR)', render: (_, s, i) => <InputNumber value={s.from} disabled={i === 0} min={0} onChange={v => setSlab(i, { from: v ?? 0 })} style={{ width: 140 }} /> },
          { title: 'To', render: (_, s) => s.to ? money(s.to) : 'and above' },
          { title: 'Fixed tax', render: (_, s, i) => <InputNumber value={s.fixedTax} min={0} onChange={v => setSlab(i, { fixedTax: v ?? 0 })} style={{ width: 120 }} /> },
          { title: 'Rate on excess', render: (_, s, i) => <InputNumber value={+(s.rate * 100).toFixed(4)} min={0} max={100} addonAfter="%" onChange={v => setSlab(i, { rate: (v ?? 0) / 100 })} style={{ width: 120 }} /> },
          { key: 'x', render: (_, __, i) => i > 0 && <Button size="small" danger onClick={() => setSlabs(s => s.filter((_, j) => j !== i))}>×</Button> },
        ]} />
      <Button size="small" style={{ marginTop: 8 }} icon={<PlusOutlined />}
        onClick={() => setSlabs(s => [...s, { from: (s[s.length - 1]?.from ?? 0) + 1_000_000, to: null, fixedTax: 0, rate: 0 }])}>Add slab</Button>
      <Space wrap style={{ marginTop: 16, display: 'flex' }}>
        <Switch checked={surchargeOn} onChange={setSurchargeOn} /> Surcharge
        {surchargeOn && <>
          <InputNumber value={surcharge} onChange={setSurcharge} addonAfter="% of tax" style={{ width: 150 }} />
          <InputNumber value={threshold} onChange={setThreshold} addonBefore="when income >" style={{ width: 240 }} />
        </>}
      </Space>
      <Input.TextArea style={{ marginTop: 12 }} rows={2} placeholder="Source / notes (e.g. Finance Act 2026, FBR circular)" value={notes} onChange={e => setNotes(e.target.value)} />
    </Modal>
  )
}

function Components() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [editing, setEditing] = useState<PayComponent | 'new' | null>(null)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['pay-components'], queryFn: async () => (await api.get<PayComponent[]>('/payroll/components')).data })

  const save = async () => {
    const v = await form.validateFields()
    const body = { ...v, exemptUpToFractionOfBasic: v.exemptPct ? v.exemptPct / 100 : null }
    try {
      if (editing === 'new') await api.post('/payroll/components', body)
      else if (editing) await api.put(`/payroll/components/${editing.id}`, body)
      message.success('Saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['pay-components'] })
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Card extra={can(P.payrollSettings) && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add head</Button>}>
      <Table<PayComponent> rowKey="id" loading={isLoading} dataSource={data} pagination={false}
        onRow={c => ({ onClick: () => can(P.payrollSettings) && setEditing(c), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Code', dataIndex: 'code' },
          { title: 'Name', dataIndex: 'name', render: (n, c) => <Space>{n}{c.isBasic && <Tag color="blue">Basic</Tag>}</Space> },
          { title: 'Type', dataIndex: 'kind', render: (k: string) => <Tag color={k === 'Earning' ? 'green' : 'red'}>{k}</Tag> },
          { title: 'Tax', render: (_, c) => c.kind === 'Deduction' ? '—' : !c.isTaxable ? 'Exempt' : c.exemptUpToFractionOfBasic ? `Exempt up to ${pct(c.exemptUpToFractionOfBasic)} of basic` : 'Taxable' },
          { title: 'Active', dataIndex: 'isActive', render: (a: boolean) => a ? 'Yes' : <Tag>No</Tag> },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'New salary head' : 'Edit salary head'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false}
          initialValues={editing === 'new' ? { kind: 'Earning', isTaxable: true, isActive: true, sortOrder: 10 }
            : editing ? { ...editing, exemptPct: editing.exemptUpToFractionOfBasic ? editing.exemptUpToFractionOfBasic * 100 : undefined } : {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="kind" label="Type"><Select options={[{ value: 'Earning', label: 'Earning' }, { value: 'Deduction', label: 'Deduction' }]} /></Form.Item></Col>
            <Col span={12}><Form.Item name="sortOrder" label="Order"><InputNumber style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="exemptPct" label="Tax-exempt up to % of basic" extra="e.g. 10 for medical allowance"><InputNumber min={0} max={100} addonAfter="%" style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12} style={{ paddingTop: 30 }}>
              <Form.Item name="isTaxable" valuePropName="checked" noStyle><Checkbox>Taxable</Checkbox></Form.Item>
              <Form.Item name="isBasic" valuePropName="checked" noStyle><Checkbox>Basic salary</Checkbox></Form.Item>
              <Form.Item name="isActive" valuePropName="checked" noStyle><Checkbox>Active</Checkbox></Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </Card>
  )
}
