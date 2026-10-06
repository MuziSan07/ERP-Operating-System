import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Popconfirm, Row, Segmented, Space,
  Statistic, Switch, Table, Tabs, Tag, Typography, Upload,
} from 'antd'
import { CheckOutlined, DisconnectOutlined, LinkOutlined, PlusOutlined, PrinterOutlined, ThunderboltOutlined, UploadOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { amount, type Reconciliation, type ReconciliationListItem, type StatementLine, type WhtCertificate, type WhtDeposit, type WhtRate, type WhtSummary } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect, useWhtRates } from '../../components/FinancePickers'

const d8 = (d?: Dayjs | null) => d?.format('YYYY-MM-DD')
const pct = (r: number) => `${+(r * 100).toFixed(2)}%`
const esc = (x?: string | number | null) => String(x ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)

// ======================= Withholding tax =======================

export function WithholdingPage() {
  const { can } = useAuth()
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Withholding tax</Typography.Title></div>
      <Card>
        <Tabs items={[
          { key: 'register', label: 'Deductions & deposits', children: <WhtRegister /> },
          { key: 'certificates', label: 'Certificates', children: <WhtCertificates /> },
          ...(can('finance.settings.manage') ? [{ key: 'rates', label: 'Rates', children: <WhtRates /> }] : []),
        ]} />
      </Card>
    </>
  )
}

function WhtRegister() {
  const qc = useQueryClient()
  const { can } = useAuth()
  const [month, setMonth] = useState<Dayjs>(dayjs().startOf('month'))
  const [depositing, setDepositing] = useState(false)
  const from = month.startOf('month'), to = month.endOf('month')
  const { data, isFetching } = useQuery({ queryKey: ['fin-wht', d8(from)], queryFn: async () => (await api.get<WhtSummary>('/finance/withholding/deductions', { params: { from: d8(from), to: d8(to) } })).data })
  const { data: deposits = [] } = useQuery({ queryKey: ['fin-wht-deposits'], queryFn: async () => (await api.get<WhtDeposit[]>('/finance/withholding/deposits')).data })
  return (
    <>
      <Space wrap style={{ marginBottom: 16 }}>
        <DatePicker picker="month" value={month} onChange={d => d && setMonth(d)} allowClear={false} format="MMMM YYYY" />
        {can('finance.payments.create') && <Button type="primary" disabled={!data?.undeposited} onClick={() => setDepositing(true)}>Deposit {data?.undeposited ? amount(data.undeposited, 0) : ''} with FBR</Button>}
      </Space>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}><Statistic title="Payments subject to tax" value={amount(data?.totalBase, 0)} /></Col>
        <Col xs={12} md={6}><Statistic title="Tax withheld" value={amount(data?.totalTax, 0)} /></Col>
        <Col xs={12} md={6}><Statistic title="Deposited" value={amount(data?.deposited, 0)} /></Col>
        <Col xs={12} md={6}><Statistic title="To deposit" value={amount(data?.undeposited, 0)} styles={{ content: { color: data?.undeposited ? '#d46b08' : undefined } }} /></Col>
      </Row>
      <Table size="small" rowKey="paymentId" loading={isFetching} dataSource={data?.deductions} pagination={false} scroll={{ x: 900 }} locale={{ emptyText: 'No tax withheld this month' }}
        columns={[{ title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'Payment', dataIndex: 'paymentNumber' },
          { title: 'Supplier', render: (_, d) => <>{d.vendorName}{d.nonFiler && <Tag color="red" style={{ marginLeft: 6 }}>Non-ATL</Tag>}<div><Typography.Text type="secondary">{d.vendorNtn ? `NTN ${d.vendorNtn}` : d.vendorCnic ? `CNIC ${d.vendorCnic}` : 'No NTN/CNIC'}</Typography.Text></div></> },
          { title: 'Section', dataIndex: 'section' }, { title: 'Rate', render: (_, d) => pct(d.rate) },
          { title: 'Amount', align: 'right', render: (_, d) => amount(d.taxBase, 0) }, { title: 'Tax', align: 'right', render: (_, d) => amount(d.tax, 0) },
          { title: 'Deposited', render: (_, d) => d.cprNumber ? <Tag color="green">CPR {d.cprNumber}</Tag> : <Tag color="orange">Pending</Tag> }]} />
      <Typography.Title level={5} style={{ marginTop: 24 }}>Deposits</Typography.Title>
      <Table size="small" rowKey="id" dataSource={deposits} pagination={false} locale={{ emptyText: 'None yet' }}
        columns={[{ title: 'Month', render: (_, d) => dayjs(new Date(d.year, d.month - 1, 1)).format('MMMM YYYY') }, { title: 'Paid on', dataIndex: 'date', render: fmtDate },
          { title: 'CPR', dataIndex: 'cprNumber' }, { title: 'Deductions', dataIndex: 'deductions' }, { title: 'Amount', align: 'right', render: (_, d) => amount(d.amount, 0) }]} />
      {depositing && <DepositModal month={month} total={data?.undeposited ?? 0} onClose={() => setDepositing(false)} onDone={() => Promise.all(['fin-wht', 'fin-wht-deposits'].map(k => qc.invalidateQueries({ queryKey: [k] })))} />}
    </>
  )
}

function DepositModal({ month, total, onClose, onDone }: { month: Dayjs; total: number; onClose: () => void; onDone: () => void }) {
  const { message } = App.useApp()
  const { me } = useAuth()
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const r = (await api.post<WhtDeposit>('/finance/withholding/deposits', { ...v, year: month.year(), month: month.month() + 1, date: d8(v.date) })).data
      message.success(`${amount(r.amount, 0)} deposited (${r.deductions} deductions)`); onDone(); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={`Deposit tax withheld in ${month.format('MMMM YYYY')}`} onCancel={onClose} onOk={ok} okText={`Record deposit of ${amount(total, 0)}`}>
      <Alert type="info" showIcon style={{ marginBottom: 16 }} title="Pay through FBR's e-payment (PSID) first, then record the CPR number here. Monthly deposits are due by the 15th of the following month." />
      <Form form={form} layout="vertical" initialValues={{ date: dayjs(), entityId: me?.entities.find(e => e.permissions.includes('finance.payments.create'))?.id }}>
        <Form.Item name="cprNumber" label="CPR number" rules={[{ required: true }]}><Input placeholder="IT-20261015-0123-456789" /></Form.Item>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="date" label="Paid on" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={12}><Form.Item name="bankAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Bank']} /></Form.Item></Col>
        </Row>
        <Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="finance.payments.create" /></Form.Item>
      </Form>
    </Modal>
  )
}

function WhtCertificates() {
  const { message } = App.useApp()
  const [vendorId, setVendorId] = useState<string>()
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => {
    // Pakistani tax year: 1 July – 30 June.
    const now = dayjs()
    const start = now.month() >= 6 ? now.month(6).startOf('month') : now.subtract(1, 'year').month(6).startOf('month')
    return [start, now]
  })
  const print = async () => {
    if (!vendorId) return
    try {
      const c = (await api.get<WhtCertificate>('/finance/withholding/certificate', { params: { vendorId, from: d8(range[0]), to: d8(range[1]) } })).data
      if (!c.deductions.length) { message.info('No tax was withheld from this supplier in the period.'); return }
      const w = window.open('', '_blank', 'width=900,height=800')
      if (!w) return
      w.document.write(`<html><head><title>Certificate — ${esc(c.vendorName)}</title><style>body{font-family:sans-serif;padding:32px}table{width:100%;border-collapse:collapse;margin-top:16px}
        th,td{border:1px solid #999;padding:6px 8px;text-align:left}th{background:#f3f3f3}td.r{text-align:right}.sig{margin-top:56px;text-align:right}</style></head><body>
        <h2 style="margin:0">Certificate of collection / deduction of income tax</h2><div>(Section 164 of the Income Tax Ordinance, 2001)</div>
        <p><b>Withholding agent:</b> ${esc(c.organizationName)}${c.organizationNtn ? ` — NTN ${esc(c.organizationNtn)}` : ''}<br/>
        <b>Taxpayer:</b> ${esc(c.vendorName)}${c.vendorNtn ? ` — NTN ${esc(c.vendorNtn)}` : ''}${c.vendorCnic ? ` — CNIC ${esc(c.vendorCnic)}` : ''}<br/>
        <b>Period:</b> ${esc(fmtDate(c.from))} to ${esc(fmtDate(c.to))}</p>
        <table><tr><th>Date</th><th>Payment</th><th>Section</th><th>Rate</th><th>Amount paid</th><th>Tax deducted</th><th>CPR</th></tr>
        ${c.deductions.map(d => `<tr><td>${esc(fmtDate(d.date))}</td><td>${esc(d.paymentNumber)}</td><td>${esc(d.section)}</td><td>${esc(pct(d.rate))}</td><td class="r">${esc(amount(d.taxBase))}</td><td class="r">${esc(amount(d.tax))}</td><td>${esc(d.cprNumber ?? 'not yet deposited')}</td></tr>`).join('')}
        <tr><th colspan="4">Total</th><th class="r">${esc(amount(c.totalBase))}</th><th class="r">${esc(amount(c.totalTax))}</th><th></th></tr></table>
        <div class="sig">Authorised signature and stamp ____________________</div><script>window.print()</script></body></html>`)
      w.document.close()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Space orientation="vertical" style={{ maxWidth: 520, width: '100%' }}>
      <Typography.Paragraph type="secondary">A certificate of tax deducted (section 164) for a supplier, to claim against their own tax. The default period is the current tax year.</Typography.Paragraph>
      <ContactSelect vendors value={vendorId} onChange={setVendorId} />
      <DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} format="DD MMM YYYY" allowClear={false} style={{ width: '100%' }} />
      <Button type="primary" icon={<PrinterOutlined />} disabled={!vendorId} onClick={print}>Print certificate</Button>
    </Space>
  )
}

function WhtRates() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isFetching } = useWhtRates()
  const [editing, setEditing] = useState<WhtRate | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (r: WhtRate | 'new') => { setEditing(r); form.resetFields(); form.setFieldsValue(r === 'new' ? { isActive: true } : { ...r, rate: +(r.rate * 100).toFixed(4) }) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, rate: v.rate / 100 }
      if (editing && editing !== 'new') await api.put(`/finance/withholding/rates/${editing.id}`, body); else await api.post('/finance/withholding/rates', body)
      message.success('Rate saved'); await qc.invalidateQueries({ queryKey: ['fin-wht-rates'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <Alert type="warning" showIcon style={{ marginBottom: 12 }} title="Withholding rates change with each Finance Act. Check them every July and update here; rates shown are for active taxpayers (non-ATL suppliers are charged double)." />
      <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add rate</Button>
      <Table size="small" rowKey="id" loading={isFetching} dataSource={data} pagination={false} onRow={r => ({ onClick: () => open(r), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Section', dataIndex: 'section' }, { title: 'Name', render: (_, r) => <>{r.name}{!r.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}</> },
          { title: 'Code', dataIndex: 'code' }, { title: 'Rate (ATL)', render: (_, r) => pct(r.rate) }, { title: 'Non-ATL', render: (_, r) => pct(r.rate * 2) },
          { title: 'Held in', dataIndex: 'payableAccountName' }]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add withholding rate' : 'Edit withholding rate'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={12}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="section" label="Section" rules={[{ required: true }]}><Input placeholder="153(1)(b)" /></Form.Item></Col>
            <Col span={24}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="rate" label="Rate for active taxpayers (%)" rules={[{ required: true }]}><InputNumber min={0.01} max={99} step={0.5} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
            <Col span={24}><Form.Item name="payableAccountId" label="Held in (liability)" extra="Blank = 2185 Income tax withheld from suppliers"><AccountSelect types={['Liability']} allowClear /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

// ======================= Bank reconciliation =======================

export function BankReconciliationPage() {
  const { can } = useAuth()
  const [open, setOpen] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['fin-recs'], queryFn: async () => (await api.get<ReconciliationListItem[]>('/finance/reconciliations')).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Bank reconciliation</Typography.Title>
        {can('finance.journals.post') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New reconciliation</Button>}
      </div>
      <Card>
        <Table<ReconciliationListItem> rowKey="id" loading={isFetching} dataSource={data} pagination={false} locale={{ emptyText: <Empty description="Import a bank statement to reconcile an account" /> }}
          onRow={r => ({ onClick: () => setOpen(r.id), style: { cursor: 'pointer' } })}
          columns={[{ title: 'Account', dataIndex: 'bankAccountName' }, { title: 'Statement date', dataIndex: 'statementDate', render: fmtDate },
            { title: 'Statement balance', align: 'right', render: (_, r) => amount(r.statementBalance) }, { title: 'Lines', dataIndex: 'lines' },
            { title: 'Status', render: (_, r) => r.status === 'Completed' ? <Tag color="green">Reconciled</Tag> : <Tag color="gold">In progress · {r.unmatched} unmatched</Tag> }]} />
      </Card>
      {creating && <NewReconciliation onClose={() => setCreating(false)} onCreated={setOpen} />}
      {open && <ReconciliationDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function NewReconciliation({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const ok = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const r = (await api.post<Reconciliation>('/finance/reconciliations', { ...v, statementDate: d8(v.statementDate) })).data
      message.success(`${r.lines.length} statement lines imported`); await qc.invalidateQueries({ queryKey: ['fin-recs'] }); onClose(); onCreated(r.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Modal open width={720} title="Import bank statement" onCancel={onClose} onOk={ok} okText="Import" confirmLoading={busy}>
      <Form form={form} layout="vertical" initialValues={{ statementDate: dayjs().subtract(1, 'month').endOf('month'), entityId: me?.entities.find(e => e.permissions.includes('finance.journals.post'))?.id }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="bankAccountId" label="Bank account" rules={[{ required: true }]}><AccountSelect subTypes={['Bank']} /></Form.Item></Col>
          <Col span={6}><Form.Item name="statementDate" label="Statement date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={6}><Form.Item name="statementBalance" label="Closing balance" rules={[{ required: true }]} tooltip="As printed on the statement (negative if overdrawn)"><InputNumber style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={24}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="finance.journals.post" /></Form.Item></Col>
          <Col span={24}>
            <Form.Item name="csv" label="Statement (CSV)" rules={[{ required: true, message: 'Paste or upload the bank’s CSV export' }]}
              extra="Header row with Date, Description, Reference and either Amount (money in positive) or Debit/Credit columns. Most Pakistani banks’ internet-banking exports work as they are.">
              <Input.TextArea rows={8} placeholder={'Date,Description,Reference,Debit,Credit\n30/09/2026,Cheque 004512,PAY-2027-00012,107000,\n30/09/2026,Bank charges,,500,'} style={{ fontFamily: 'monospace', fontSize: 12 }} />
            </Form.Item>
            <Upload accept=".csv,text/csv" showUploadList={false} beforeUpload={async file => { form.setFieldValue('csv', await file.text()); return false }}>
              <Button icon={<UploadOutlined />}>Load a CSV file</Button>
            </Upload>
          </Col>
        </Row>
      </Form>
    </Modal>
  )
}

function ReconciliationDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [selected, setSelected] = useState<string | null>(null)
  const [posting, setPosting] = useState<StatementLine | null>(null)
  const [show, setShow] = useState<'unmatched' | 'all'>('unmatched')
  const { data: r, isLoading } = useQuery({ queryKey: ['fin-rec', id], queryFn: async () => (await api.get<Reconciliation>(`/finance/reconciliations/${id}`)).data })
  const set = (d: Reconciliation) => { qc.setQueryData(['fin-rec', id], d); void qc.invalidateQueries({ queryKey: ['fin-recs'] }) }
  const act = async (fn: () => Promise<Reconciliation>, ok?: string) => {
    try { set(await fn()); if (ok) message.success(ok) } catch (e) { message.error(errorMessage(e)) }
  }
  const draft = r?.status === 'Draft' && can('finance.journals.post')
  const sel = r?.lines.find(l => l.id === selected)
  return (
    <Drawer open onClose={onClose} size={1150} loading={isLoading} title={r && <Space>{r.bankAccountName} · {fmtDate(r.statementDate)}{r.status === 'Completed' ? <Tag color="green">Reconciled</Tag> : <Tag color="gold">In progress</Tag>}</Space>}
      extra={draft && <Space wrap>
        <Button icon={<ThunderboltOutlined />} onClick={() => act(async () => { const res = (await api.post<{ matched: number; reconciliation: Reconciliation }>(`/finance/reconciliations/${id}/auto-match`)).data; message.success(`${res.matched} line(s) matched`); return res.reconciliation })}>Auto-match</Button>
        <Popconfirm title="Delete this reconciliation? Matches are released." onConfirm={async () => { try { await api.delete(`/finance/reconciliations/${id}`); await qc.invalidateQueries({ queryKey: ['fin-recs'] }); onClose() } catch (e) { message.error(errorMessage(e)) } }}><Button danger>Delete</Button></Popconfirm>
        <Button type="primary" icon={<CheckOutlined />} disabled={!r?.canComplete} onClick={() => act(async () => (await api.post<Reconciliation>(`/finance/reconciliations/${id}/complete`)).data, 'Reconciliation completed')}>Complete</Button>
      </Space>}>
      {r && <>
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={12} md={4}><Statistic title="Statement balance" value={amount(r.statementBalance)} /></Col>
          <Col xs={12} md={5}><Statistic title="+ Deposits in transit" value={amount(r.depositsInTransit)} /></Col>
          <Col xs={12} md={5}><Statistic title="− Unpresented payments" value={amount(r.outstandingPayments)} /></Col>
          <Col xs={12} md={5}><Statistic title="Balance per books" value={amount(r.bookBalance)} /></Col>
          <Col xs={24} md={5}><Statistic title="Difference" value={amount(r.difference)} styles={{ content: { color: r.difference === 0 ? '#389e0d' : '#cf1322' } }} /></Col>
        </Row>
        {r.status === 'Draft' && <Alert style={{ marginBottom: 16 }} showIcon type={r.canComplete ? 'success' : 'info'}
          title={r.canComplete ? 'Everything agrees — complete the reconciliation.' : `${r.unmatchedLines} statement line(s) to match. Select a statement line, then click "Match" on the book entry with the same amount — or post it (bank charges, profit) if it isn't in the books.`} />}
        <Row gutter={16}>
          <Col xs={24} xl={12}>
            <Space style={{ marginBottom: 8 }}><Typography.Title level={5} style={{ margin: 0 }}>Bank statement</Typography.Title>
              <Segmented size="small" value={show} onChange={v => setShow(v as typeof show)} options={[{ value: 'unmatched', label: 'Unmatched' }, { value: 'all', label: 'All' }]} /></Space>
            <Table size="small" rowKey="id" pagination={false} scroll={{ y: 420 }} dataSource={r.lines.filter(l => show === 'all' || !l.journalLineId)}
              rowClassName={l => (l.id === selected ? 'ant-table-row-selected' : '')}
              onRow={l => ({ onClick: () => !l.journalLineId && draft && setSelected(l.id === selected ? null : l.id), style: { cursor: !l.journalLineId && draft ? 'pointer' : undefined } })}
              columns={[{ title: 'Date', dataIndex: 'date', width: 95, render: (d: string) => dayjs(d).format('DD MMM') },
                { title: 'Description', render: (_, l) => <>{l.description}{l.reference && <div><Typography.Text type="secondary" style={{ fontSize: 12 }}>{l.reference}</Typography.Text></div>}</> },
                { title: 'Amount', align: 'right', width: 120, render: (_, l) => <Typography.Text type={l.amount < 0 ? undefined : 'success'}>{amount(l.amount)}</Typography.Text> },
                { title: '', width: 150, render: (_, l) => l.journalLineId
                  ? <Space size={4}><Tag color="green">{l.matchedTo ?? 'matched'}</Tag>{draft && <Button size="small" type="text" icon={<DisconnectOutlined />} title="Unmatch"
                      onClick={e => { e.stopPropagation(); void act(async () => (await api.post<Reconciliation>(`/finance/reconciliations/${id}/unmatch`, { statementLineId: l.id })).data) }} />}</Space>
                  : draft && <Button size="small" onClick={e => { e.stopPropagation(); setPosting(l) }}>Post…</Button> }]} />
          </Col>
          <Col xs={24} xl={12}>
            <Typography.Title level={5}>In the books, not yet on a statement</Typography.Title>
            <Table size="small" rowKey="journalLineId" pagination={false} scroll={{ y: 420 }} dataSource={r.bookItems.filter(b => !b.matched)}
              locale={{ emptyText: 'Nothing outstanding' }}
              columns={[{ title: 'Date', dataIndex: 'date', width: 95, render: (d: string) => dayjs(d).format('DD MMM') },
                { title: 'Entry', render: (_, b) => <>{b.number}<div><Typography.Text type="secondary" style={{ fontSize: 12 }}>{b.description}</Typography.Text></div></> },
                { title: 'Amount', align: 'right', width: 120, render: (_, b) => amount(b.amount) },
                { title: '', width: 90, render: (_, b) => sel && <Button size="small" type={sel.amount === b.amount ? 'primary' : 'default'} icon={<LinkOutlined />} disabled={sel.amount !== b.amount}
                  onClick={() => act(async () => { const d = (await api.post<Reconciliation>(`/finance/reconciliations/${id}/match`, { statementLineId: sel.id, journalLineId: b.journalLineId })).data; setSelected(null); return d })}>Match</Button> }]} />
          </Col>
        </Row>
      </>}
      {posting && <PostLineModal line={posting} onClose={() => setPosting(null)} onPost={(accountId, description) =>
        act(async () => { const d = (await api.post<Reconciliation>(`/finance/reconciliations/${id}/create-entry`, { statementLineId: posting.id, accountId, description })).data; setPosting(null); return d }, 'Entry posted and matched')} />}
    </Drawer>
  )
}

function PostLineModal({ line, onClose, onPost }: { line: StatementLine; onClose: () => void; onPost: (accountId: string, description?: string) => void }) {
  const [form] = Form.useForm()
  const ok = async () => { const v = await form.validateFields(); onPost(v.accountId, v.description) }
  return (
    <Modal open title="Post from the statement" onCancel={onClose} onOk={ok} okText="Post and match">
      <Descriptions size="small" column={1} style={{ marginBottom: 12 }} items={[{ label: 'Line', children: `${fmtDate(line.date)} · ${line.description}` },
        { label: 'Amount', children: `${amount(line.amount)} (${line.amount < 0 ? 'money out' : 'money in'})` }]} />
      <Form form={form} layout="vertical" initialValues={{ description: line.description }}>
        <Form.Item name="accountId" label={line.amount < 0 ? 'Charge to' : 'Credit to'} rules={[{ required: true }]} extra={line.amount < 0 ? 'e.g. 6800 Bank charges' : 'e.g. bank profit / other income'}>
          <AccountSelect types={line.amount < 0 ? ['Expense', 'Liability', 'Asset'] : ['Income', 'Liability', 'Asset']} />
        </Form.Item>
        <Form.Item name="description" label="Description"><Input /></Form.Item>
      </Form>
    </Modal>
  )
}
