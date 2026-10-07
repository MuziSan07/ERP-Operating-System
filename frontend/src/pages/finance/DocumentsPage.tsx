import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Card, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Popconfirm, Row, Select, Space, Switch, Table, Tag, Typography } from 'antd'
import { CheckOutlined, DeleteOutlined, DollarOutlined, EditOutlined, PlusOutlined, PrinterOutlined, StopOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { DOC_COLORS, amount, type DocumentKind, type DocumentListItem, type DocumentStatus, type FinanceDocument, type PaymentKind } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect, CurrencyTag, TaxRateSelect, WhtRateSelect, useContacts, useFinanceSettings, useTaxRates, useWhtRates } from '../../components/FinancePickers'
import ExportButton, { fetchAllPages } from '../../components/ExportButton'

const perm = (kind: DocumentKind, action: string) => `finance.${kind === 'Invoice' ? 'invoices' : 'bills'}.${action}`

export default function DocumentsPage({ kind }: { kind: DocumentKind }) {
  const { can } = useAuth()
  const { data: settings } = useFinanceSettings()
  const path = kind === 'Invoice' ? 'invoices' : 'bills'
  const [status, setStatus] = useState<DocumentStatus>()
  const [overdue, setOverdue] = useState(false)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<FinanceDocument | 'new' | null>(null)
  const [viewing, setViewing] = useState<string | null>(null)

  useEffect(() => { setPage(1); setViewing(null); setEditing(null) }, [kind])
  const { data, isFetching } = useQuery({
    queryKey: ['fin-docs', kind, status, overdue, search, page],
    queryFn: async () => (await api.get<PagedResult<DocumentListItem>>(`/finance/${path}`, { params: { status, overdue, search, page, pageSize: 25 } })).data,
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>{kind === 'Invoice' ? 'Sales invoices' : 'Purchase bills'}</Typography.Title>
        <Space wrap>
          <ExportButton<DocumentListItem> fileName={kind === 'Invoice' ? 'sales-invoices' : 'purchase-bills'} title={kind === 'Invoice' ? 'Sales invoices' : 'Purchase bills'}
            rows={() => fetchAllPages<DocumentListItem>(`/finance/${path}`, { status, overdue, search })}
            columns={[{ title: 'Number', value: d => d.number ?? 'Draft' }, { title: 'Date', value: d => d.date, type: 'date' }, { title: 'Due', value: d => d.dueDate, type: 'date' },
              { title: kind === 'Invoice' ? 'Customer' : 'Vendor', value: d => d.contactName, width: 30 }, { title: 'Entity', value: d => d.entityName, width: 24 },
              { title: 'Reference', value: d => d.reference }, { title: 'Status', value: d => d.status }, { title: 'Currency', value: d => d.currency },
              { title: 'Total', value: d => d.total, type: 'money' }, { title: 'Balance', value: d => d.balance, type: 'money' }, { title: 'Days overdue', value: d => d.daysOverdue || null, type: 'number' }]} />
          {can(perm(kind, 'create')) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New {kind === 'Invoice' ? 'invoice' : 'bill'}</Button>}
        </Space>
      </div>
      <Card>
        <Row gutter={[12, 12]} style={{ marginBottom: 16 }} align="middle">
          <Col xs={24} md={6}>
            <Select allowClear placeholder="Any status" style={{ width: '100%' }} value={status} onChange={v => { setStatus(v); setPage(1) }}
              options={(['Draft', 'Open', 'PartiallyPaid', 'Paid', 'Void'] as DocumentStatus[]).map(s => ({ value: s, label: s === 'PartiallyPaid' ? 'Partially paid' : s }))} />
          </Col>
          <Col xs={24} md={8}><Input.Search allowClear placeholder="Number, contact or reference" onSearch={v => { setSearch(v); setPage(1) }} /></Col>
          <Col><Space><Switch checked={overdue} onChange={v => { setOverdue(v); setPage(1) }} />Overdue only</Space></Col>
        </Row>
        <Table<DocumentListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 900 }}
          onRow={d => ({ onClick: () => setViewing(d.id), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Number', dataIndex: 'number', render: (n?: string) => n ?? <Typography.Text type="secondary">Draft</Typography.Text> },
            { title: kind === 'Invoice' ? 'Customer' : 'Vendor', dataIndex: 'contactName' },
            { title: 'Date', dataIndex: 'date', render: fmtDate },
            { title: 'Due', render: (_, d) => <>{fmtDate(d.dueDate)}{d.daysOverdue > 0 && <Tag color="red" style={{ marginLeft: 6 }}>{d.daysOverdue}d overdue</Tag>}</> },
            { title: 'Reference', dataIndex: 'reference' },
            { title: 'Status', dataIndex: 'status', render: (s: DocumentStatus) => <Tag color={DOC_COLORS[s]}>{s === 'PartiallyPaid' ? 'Partially paid' : s}</Tag> },
            { title: 'Total', align: 'right', render: (_, d) => <Space>{amount(d.total)}<CurrencyTag currency={d.currency} base={settings?.baseCurrency} /></Space> },
            { title: 'Balance', align: 'right', render: (_, d) => d.status === 'Void' ? '—' : <b>{amount(d.balance)}</b> },
          ]} />
      </Card>
      {editing && <DocumentEditor kind={kind} doc={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={id => setViewing(id)} />}
      {viewing && <DocumentView kind={kind} id={viewing} onClose={() => setViewing(null)} onEdit={d => { setViewing(null); setEditing(d) }} />}
    </>
  )
}

interface LineDraft { description: string; accountId?: string; quantity: number; unitPrice: number; taxRateId?: string }

function DocumentEditor({ kind, doc, onClose, onSaved }: { kind: DocumentKind; doc?: FinanceDocument; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: settings } = useFinanceSettings()
  const { data: taxRates = [] } = useTaxRates()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [lines, setLines] = useState<LineDraft[]>(doc?.lines.map(l => ({ description: l.description, accountId: l.accountId, quantity: l.quantity, unitPrice: l.unitPrice, taxRateId: l.taxRateId }))
    ?? [{ description: '', quantity: 1, unitPrice: 0 }])
  const currency = Form.useWatch('currency', form) as string | undefined
  const isInvoice = kind === 'Invoice'

  const set = (i: number, patch: Partial<LineDraft>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))
  const lineAmount = (l: LineDraft) => Math.round(l.quantity * l.unitPrice * 100) / 100
  const lineTax = (l: LineDraft) => Math.round(lineAmount(l) * (taxRates.find(t => t.id === l.taxRateId)?.rate ?? 0) * 100) / 100
  const subtotal = lines.reduce((s, l) => s + lineAmount(l), 0)
  const taxTotal = lines.reduce((s, l) => s + lineTax(l), 0)

  const save = async () => {
    const v = await form.validateFields()
    if (lines.some(l => !l.accountId || !l.description)) { message.error('Every line needs a description and an account.'); return }
    setBusy(true)
    try {
      const body = {
        entityId: v.entityId, contactId: v.contactId, date: (v.date as Dayjs).format('YYYY-MM-DD'), dueDate: v.dueDate ? (v.dueDate as Dayjs).format('YYYY-MM-DD') : null,
        reference: v.reference, notes: v.notes, currency: v.currency || null, exchangeRate: v.exchangeRate || null, lines,
      }
      const path = isInvoice ? 'invoices' : 'bills'
      const res = doc ? await api.put<FinanceDocument>(`/finance/${path}/${doc.id}`, body) : await api.post<FinanceDocument>(`/finance/${path}`, body)
      message.success('Saved as draft')
      await qc.invalidateQueries({ queryKey: ['fin-docs'] })
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Drawer open onClose={onClose} size={980} title={doc ? `Edit draft ${isInvoice ? 'invoice' : 'bill'}` : `New ${isInvoice ? 'invoice' : 'bill'}`}
      extra={<Button type="primary" loading={busy} onClick={save}>Save draft</Button>}>
      <Form form={form} layout="vertical" initialValues={doc ? { ...doc, date: dayjs(doc.date), dueDate: dayjs(doc.dueDate), currency: doc.currency === settings?.baseCurrency ? undefined : doc.currency, exchangeRate: doc.exchangeRate === 1 ? undefined : doc.exchangeRate }
        : { entityId: me?.entities.find(e => e.permissions.includes(perm(kind, 'create')))?.id, date: dayjs() }}>
        <Row gutter={12}>
          <Col xs={24} md={10}><Form.Item name="contactId" label={isInvoice ? 'Customer' : 'Vendor'} rules={[{ required: true }]}><ContactSelect customers={isInvoice || undefined} vendors={!isInvoice || undefined} /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="entityId" label="Entity / branch" rules={[{ required: true }]}><EntityPicker permission={perm(kind, 'create')} /></Form.Item></Col>
          <Col xs={24} md={6}><Form.Item name="reference" label={isInvoice ? 'Customer PO / booking ref' : "Vendor's bill no."}><Input /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="dueDate" label="Due date" extra="Default: contact's terms"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="currency" label="Currency" extra={`Default: contact or ${settings?.baseCurrency}`}><Input maxLength={3} placeholder={settings?.baseCurrency} style={{ textTransform: 'uppercase' }} /></Form.Item></Col>
          {currency && currency.toUpperCase() !== settings?.baseCurrency && (
            <Col xs={12} md={5}><Form.Item name="exchangeRate" label={`Rate (${settings?.baseCurrency} per ${currency.toUpperCase()})`} extra="Blank = stored rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          )}
        </Row>
      </Form>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines} scroll={{ x: 860 }}
        columns={[
          { title: 'Description', width: 240, render: (_, l, i) => <Input value={l.description} onChange={e => set(i, { description: e.target.value })} /> },
          { title: isInvoice ? 'Income account' : 'Expense / asset account', width: 220, render: (_, l, i) => <AccountSelect value={l.accountId} onChange={v => set(i, { accountId: v })} types={isInvoice ? ['Income', 'Liability'] : ['Expense', 'Asset']} /> },
          { title: 'Qty', width: 80, render: (_, l, i) => <InputNumber min={0} value={l.quantity} onChange={v => set(i, { quantity: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Unit price', width: 120, render: (_, l, i) => <InputNumber min={0} value={l.unitPrice} onChange={v => set(i, { unitPrice: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Tax', width: 170, render: (_, l, i) => <TaxRateSelect value={l.taxRateId} onChange={v => set(i, { taxRateId: v })} /> },
          { title: 'Amount', align: 'right', render: (_, l) => amount(lineAmount(l)) },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length === 1} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(ls => [...ls, { description: '', quantity: 1, unitPrice: 0, accountId: ls[ls.length - 1]?.accountId, taxRateId: ls[ls.length - 1]?.taxRateId }])}>Add line</Button>
      <Row justify="end" style={{ marginTop: 16 }}>
        <Col xs={24} sm={10}>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Subtotal">{amount(subtotal)}</Descriptions.Item>
            <Descriptions.Item label="Sales tax">{amount(taxTotal)}</Descriptions.Item>
            <Descriptions.Item label={<b>Total {currency?.toUpperCase() || settings?.baseCurrency}</b>}><b>{amount(subtotal + taxTotal)}</b></Descriptions.Item>
          </Descriptions>
        </Col>
      </Row>
      <Form form={form} layout="vertical" style={{ marginTop: 16 }}><Form.Item name="notes" label="Notes / payment instructions"><Input.TextArea rows={2} /></Form.Item></Form>
    </Drawer>
  )
}

function DocumentView({ kind, id, onClose, onEdit }: { kind: DocumentKind; id: string; onClose: () => void; onEdit: (d: FinanceDocument) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const { data: settings } = useFinanceSettings()
  const [paying, setPaying] = useState(false)
  const path = kind === 'Invoice' ? 'invoices' : 'bills'
  const { data: d } = useQuery({ queryKey: ['fin-doc', id], queryFn: async () => (await api.get<FinanceDocument>(`/finance/${path}/${id}`)).data })

  const act = async (fn: () => Promise<unknown>, done: string, close = false) => {
    try {
      await fn()
      message.success(done)
      await qc.invalidateQueries({ queryKey: ['fin-docs'] })
      await qc.invalidateQueries({ queryKey: ['fin-doc', id] })
      if (close) onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  if (!d) return <Drawer open onClose={onClose} loading />

  const isInvoice = kind === 'Invoice'
  const taxByRate = Object.values(d.lines.filter(l => l.taxRateId).reduce<Record<string, { name: string; amount: number }>>((acc, l) => {
    acc[l.taxRateId!] = { name: l.taxRateName ?? '', amount: (acc[l.taxRateId!]?.amount ?? 0) + l.taxAmount }
    return acc
  }, {}))

  return (
    <Drawer open onClose={onClose} size={860} title={<Space>{d.number ?? `Draft ${isInvoice ? 'invoice' : 'bill'}`}<Tag color={DOC_COLORS[d.status]}>{d.status}</Tag></Space>}
      extra={
        <Space wrap>
          {d.status === 'Draft' && can(perm(kind, 'edit'), d.entityId) && <Button icon={<EditOutlined />} onClick={() => onEdit(d)}>Edit</Button>}
          {d.status === 'Draft' && can(perm(kind, 'delete'), d.entityId) && (
            <Popconfirm title="Delete this draft?" onConfirm={() => act(() => api.delete(`/finance/${path}/${id}`), 'Draft deleted', true)}><Button danger icon={<DeleteOutlined />} /></Popconfirm>
          )}
          {d.status === 'Draft' && can(perm(kind, 'approve'), d.entityId) && (
            <Popconfirm title={`Approve and post this ${isInvoice ? 'invoice' : 'bill'}?`} description="It gets its number and is posted to the ledger." onConfirm={() => act(() => api.post(`/finance/${path}/${id}/approve`), 'Approved and posted')}>
              <Button type="primary" icon={<CheckOutlined />}>Approve</Button>
            </Popconfirm>
          )}
          {(d.status === 'Open' || d.status === 'PartiallyPaid') && can('finance.payments.create', d.entityId) && (
            <Button type="primary" icon={<DollarOutlined />} onClick={() => setPaying(true)}>{isInvoice ? 'Record receipt' : 'Record payment'}</Button>
          )}
          {d.status === 'Open' && can(perm(kind, 'approve'), d.entityId) && (
            <Popconfirm title="Void this document?" description="Its journal entry is reversed." onConfirm={() => act(() => api.post(`/finance/${path}/${id}/void`, {}), 'Voided')}>
              <Button danger icon={<StopOutlined />}>Void</Button>
            </Popconfirm>
          )}
          {d.status !== 'Draft' && <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print</Button>}
        </Space>
      }>
      <div className="print-area">
        <Row justify="space-between" gutter={16}>
          <Col>
            <Typography.Title level={3} style={{ margin: 0 }}>{isInvoice ? (d.taxTotal > 0 ? 'Sales Tax Invoice' : 'Invoice') : 'Purchase Bill'}</Typography.Title>
            <Typography.Text strong>{d.entityName}</Typography.Text>
            {isInvoice && (settings?.ntn || settings?.strn) && <div><Typography.Text type="secondary">NTN {settings?.ntn ?? '—'} · STRN {settings?.strn ?? '—'}</Typography.Text></div>}
          </Col>
          <Col style={{ textAlign: 'right' }}>
            <div><b>{d.number ?? 'DRAFT'}</b></div>
            <div>Date: {fmtDate(d.date)}</div>
            <div>Due: {fmtDate(d.dueDate)}</div>
            {d.reference && <div>Ref: {d.reference}</div>}
          </Col>
        </Row>
        <Card size="small" style={{ margin: '16px 0' }}>
          <Typography.Text type="secondary">{isInvoice ? 'Bill to' : 'Vendor'}</Typography.Text>
          <div><b>{d.contactName}</b></div>
          {d.contactAddress && <div>{d.contactAddress}</div>}
          {(d.contactNtn || d.contactStrn) && <div>NTN {d.contactNtn ?? '—'} · STRN {d.contactStrn ?? '—'}</div>}
        </Card>
        <Table size="small" pagination={false} rowKey="id" dataSource={d.lines}
          columns={[
            { title: 'Description', dataIndex: 'description' },
            { title: 'Qty', dataIndex: 'quantity', align: 'right' },
            { title: 'Rate', dataIndex: 'unitPrice', align: 'right', render: (v: number) => amount(v) },
            { title: 'Value excl. tax', dataIndex: 'amount', align: 'right', render: (v: number) => amount(v) },
            { title: 'Tax', render: (_, l) => l.taxRateName ? `${+(l.taxRate * 100).toFixed(2)}%` : '—' },
            { title: 'Tax amount', dataIndex: 'taxAmount', align: 'right', render: (v: number) => amount(v) },
          ]} />
        <Row justify="end" style={{ marginTop: 12 }}>
          <Col xs={24} sm={11}>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Value excluding tax">{amount(d.subtotal)}</Descriptions.Item>
              {taxByRate.map(t => <Descriptions.Item key={t.name} label={t.name}>{amount(t.amount)}</Descriptions.Item>)}
              <Descriptions.Item label={<b>Total {d.currency}</b>}><b>{amount(d.total)}</b></Descriptions.Item>
              {d.amountPaid > 0 && <Descriptions.Item label="Paid">{amount(d.amountPaid)}</Descriptions.Item>}
              {d.status !== 'Draft' && d.status !== 'Void' && <Descriptions.Item label={<b>Balance due</b>}><b>{amount(d.balance)}</b></Descriptions.Item>}
              {d.currency !== settings?.baseCurrency && <Descriptions.Item label={`In ${settings?.baseCurrency} @ ${d.exchangeRate}`}>{amount(d.baseTotal || d.total * d.exchangeRate)}</Descriptions.Item>}
            </Descriptions>
          </Col>
        </Row>
        {d.notes && <Typography.Paragraph style={{ marginTop: 12 }}>{d.notes}</Typography.Paragraph>}
      </div>
      {paying && <PaymentModal kind={isInvoice ? 'Receipt' : 'Payment'} contactId={d.contactId} documentId={d.id} onClose={() => setPaying(false)}
        onDone={() => { void qc.invalidateQueries({ queryKey: ['fin-doc', id] }); void qc.invalidateQueries({ queryKey: ['fin-docs'] }) }} />}
    </Drawer>
  )
}

/** Receipt from a customer or payment to a vendor, allocated across their open documents. */
export function PaymentModal({ kind, contactId: initialContact, documentId, onClose, onDone }: {
  kind: PaymentKind; contactId?: string; documentId?: string; onClose: () => void; onDone?: () => void
}) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: settings } = useFinanceSettings()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const contactId = Form.useWatch('contactId', form) as string | undefined ?? initialContact
  const [alloc, setAlloc] = useState<Record<string, number>>({})
  const docKind = kind === 'Receipt' ? 'invoices' : 'bills'

  const { data: open } = useQuery({
    queryKey: ['fin-open-docs', docKind, contactId], enabled: !!contactId,
    queryFn: async () => {
      const [o, p] = await Promise.all(['Open', 'PartiallyPaid'].map(status => api.get<PagedResult<DocumentListItem>>(`/finance/${docKind}`, { params: { contactId, status, pageSize: 200 } })))
      return [...o.data.items, ...p.data.items].sort((a, b) => a.dueDate.localeCompare(b.dueDate))
    },
  })
  // A payment settles documents of its own entity only: default to the document's entity and offer only that entity's documents.
  const entityId = Form.useWatch('entityId', form) as string | undefined
  useEffect(() => {
    const doc = open?.find(d => d.id === documentId)
    if (doc) form.setFieldValue('entityId', doc.entityId)
  }, [open, documentId, form])
  useEffect(() => { setAlloc(a => Object.fromEntries(Object.entries(a).filter(([id]) => open?.find(d => d.id === id)?.entityId === entityId))) }, [entityId, open])
  useEffect(() => {
    if (!open) return
    setAlloc(documentId ? Object.fromEntries(open.filter(d => d.id === documentId).map(d => [d.id, d.balance])) : {})
    const cur = open.find(d => d.id === documentId)?.currency ?? open[0]?.currency
    if (cur) form.setFieldValue('currency', cur)
  }, [open, documentId, form])

  const currency = Form.useWatch('currency', form) as string | undefined
  const total = Object.values(alloc).reduce((s, v) => s + (v || 0), 0)

  // Withholding (payments to suppliers): suggested from the vendor; charged on the part of each bill excluding sales tax,
  // doubled for suppliers not on the Active Taxpayer List. Mirrors the server's calculation for the preview.
  const { data: vendors = [] } = useContacts({ vendors: true })
  const { data: whtRates = [] } = useWhtRates()
  const vendor = kind === 'Payment' ? vendors.find(c => c.id === contactId) : undefined
  const whtRateId = Form.useWatch('withholdingTaxRateId', form) as string | undefined
  useEffect(() => { if (kind === 'Payment') form.setFieldValue('withholdingTaxRateId', vendor?.defaultWhtRateId) }, [kind, vendor?.id, vendor?.defaultWhtRateId, form])
  const whtRate = whtRates.find(r => r.id === whtRateId)
  const appliedRate = whtRate ? whtRate.rate * (vendor?.notOnActiveTaxpayerList ? 2 : 1) : 0
  const whtBase = (open ?? []).reduce((s, d) => s + (alloc[d.id] ? Math.round((d.total ? alloc[d.id] * d.subtotal / d.total : alloc[d.id]) * 100) / 100 : 0), 0)
  const whtTax = Math.round(whtBase * appliedRate * 100) / 100

  const save = async () => {
    const v = await form.validateFields()
    const allocations = Object.entries(alloc).filter(([, a]) => a > 0).map(([documentId, amount]) => ({ documentId, amount }))
    if (!allocations.length) { message.error('Allocate the amount to at least one document.'); return }
    setBusy(true)
    try {
      await api.post('/finance/payments', {
        kind, entityId: v.entityId, contactId, date: (v.date as Dayjs).format('YYYY-MM-DD'), bankAccountId: v.bankAccountId,
        currency: v.currency, exchangeRate: v.exchangeRate || null, amount: Math.round(total * 100) / 100, reference: v.reference, notes: v.notes, allocations,
        withholdingTaxRateId: kind === 'Payment' ? v.withholdingTaxRateId : undefined,
      })
      message.success(kind === 'Receipt' ? 'Receipt recorded' : 'Payment recorded')
      await qc.invalidateQueries({ queryKey: ['fin-payments'] })
      onDone?.()
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Modal open width={760} title={kind === 'Receipt' ? 'Receive payment' : 'Pay vendor'} onCancel={onClose} onOk={save} okText={whtTax ? `Pay ${amount(total - whtTax)} (withhold ${amount(whtTax)})` : `Record ${amount(total)}`} confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false}
        initialValues={{ contactId: initialContact, date: dayjs(), entityId: me?.entities.find(e => e.permissions.includes('finance.payments.create'))?.id }}>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="contactId" label={kind === 'Receipt' ? 'Customer' : 'Vendor'} rules={[{ required: true }]}><ContactSelect customers={kind === 'Receipt' || undefined} vendors={kind === 'Payment' || undefined} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="bankAccountId" label="Bank / cash account" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="currency" label="Currency"><Input disabled /></Form.Item></Col>
          {currency && currency !== settings?.baseCurrency && (
            <Col xs={12} md={6}><Form.Item name="exchangeRate" label="Rate today" extra="Blank = stored rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          )}
          <Col xs={12} md={8}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="finance.payments.create" /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="reference" label="Cheque / transfer reference"><Input /></Form.Item></Col>
          {kind === 'Payment' && (!currency || currency === settings?.baseCurrency) && <>
            <Col xs={24} md={12}><Form.Item name="withholdingTaxRateId" label="Income tax to withhold"
              extra={vendor?.notOnActiveTaxpayerList ? 'Not on the Active Taxpayer List: the rate is doubled.' : undefined}><WhtRateSelect /></Form.Item></Col>
            {whtTax > 0 && <Col span={24}><Alert type="info" showIcon style={{ marginBottom: 12 }}
              title={`Withhold ${amount(whtTax)} (${+(appliedRate * 100).toFixed(2)}% of ${amount(whtBase)} excluding sales tax). The bank pays ${amount(total - whtTax)}; the bills are settled for ${amount(total)}.`} /></Col>}
          </>}
        </Row>
      </Form>
      <Table size="small" pagination={false} rowKey="id" dataSource={(open ?? []).filter(d => !entityId || d.entityId === entityId)}
        locale={{ emptyText: contactId ? (open?.length ? 'No open documents for this entity — change the entity above' : 'No open documents') : 'Choose a contact' }}
        columns={[
          { title: 'Document', dataIndex: 'number' },
          { title: 'Due', dataIndex: 'dueDate', render: fmtDate },
          { title: 'Total', align: 'right', render: (_, d) => <Space>{amount(d.total)}<CurrencyTag currency={d.currency} base={settings?.baseCurrency} /></Space> },
          { title: 'Balance', align: 'right', render: (_, d) => amount(d.balance) },
          {
            title: 'Allocate', render: (_, d) => (
              <Space.Compact>
                <InputNumber min={0} max={d.balance} value={alloc[d.id]} disabled={!!currency && d.currency !== currency}
                  onChange={v => setAlloc(a => ({ ...a, [d.id]: v ?? 0 }))} style={{ width: 130 }} />
                <Button onClick={() => setAlloc(a => ({ ...a, [d.id]: d.balance }))} disabled={!!currency && d.currency !== currency}>Full</Button>
              </Space.Compact>
            ),
          },
        ]} />
    </Modal>
  )
}
