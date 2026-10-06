import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Popconfirm, Row, Segmented, Select, Space, Switch, Table, Tag, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined, RollbackOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { JOURNAL_COLORS, amount, type Contact, type JournalEntry, type JournalSource, type Payment, type PaymentKind } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, WhtRateSelect, ContactSelect, CurrencyTag, useContacts, useFinanceSettings } from '../../components/FinancePickers'
import { PaymentModal } from './DocumentsPage'

// ======================= Payments =======================

export function PaymentsPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: settings } = useFinanceSettings()
  const [kind, setKind] = useState<PaymentKind | 'All'>('All')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState<PaymentKind | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['fin-payments', kind, page],
    queryFn: async () => (await api.get<PagedResult<Payment>>('/finance/payments', { params: { kind: kind === 'All' ? undefined : kind, page, pageSize: 25 } })).data,
  })
  const voidPayment = async (id: string) => {
    try { await api.post(`/finance/payments/${id}/void`, {}); message.success('Payment voided'); await qc.invalidateQueries({ queryKey: ['fin-payments'] }) } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Payments</Typography.Title>
        {can('finance.payments.create') && (
          <Space wrap>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating('Receipt')}>Receive from customer</Button>
            <Button icon={<PlusOutlined />} onClick={() => setCreating('Payment')}>Pay vendor</Button>
          </Space>
        )}
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={kind} onChange={v => { setKind(v as PaymentKind | 'All'); setPage(1) }}
          options={[{ value: 'All', label: 'All' }, { value: 'Receipt', label: 'Receipts' }, { value: 'Payment', label: 'Payments' }]} />
        <Table<Payment> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 900 }}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          expandable={{ expandedRowRender: p => <Space wrap>{p.allocations.map(a => <Tag key={a.documentId}>{a.documentNumber}: {amount(a.amount)}</Tag>)}</Space> }}
          columns={[
            { title: 'Number', dataIndex: 'number', render: (n, p) => <>{n}{p.isVoid && <Tag color="red" style={{ marginLeft: 6 }}>Void</Tag>}</> },
            { title: 'Type', dataIndex: 'kind', render: (k: PaymentKind) => <Tag color={k === 'Receipt' ? 'green' : 'orange'}>{k}</Tag> },
            { title: 'Contact', dataIndex: 'contactName' },
            { title: 'Date', dataIndex: 'date', render: fmtDate },
            { title: 'Account', dataIndex: 'bankAccountName' },
            { title: 'Reference', dataIndex: 'reference' },
            { title: 'Amount', align: 'right', render: (_, p) => <Space>{amount(p.amount)}<CurrencyTag currency={p.currency} base={settings?.baseCurrency} /></Space> },
            { title: 'Tax withheld', align: 'right', render: (_, p) => p.withholdingTax ? <span title={`u/s ${p.withholdingSection}; bank paid ${amount(p.netPaid)}`}>{amount(p.withholdingTax)}</span> : '' },
            {
              key: 'x', align: 'right', render: (_, p) => !p.isVoid && can('finance.payments.approve', p.entityId) && (
                <Popconfirm title="Void this payment?" description="The ledger entry is reversed and the documents reopen." onConfirm={() => voidPayment(p.id)}>
                  <Button size="small" danger icon={<StopOutlined />} />
                </Popconfirm>
              ),
            },
          ]} />
      </Card>
      {creating && <PaymentModal kind={creating} onClose={() => setCreating(null)} />}
    </>
  )
}

// ======================= Journals =======================

const SOURCES: JournalSource[] = ['Manual', 'Invoice', 'Bill', 'Payment', 'Payroll', 'Reversal']

export function JournalsPage() {
  const { can } = useAuth()
  const [source, setSource] = useState<JournalSource>()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [viewing, setViewing] = useState<string | null>(null)
  const [editing, setEditing] = useState<JournalEntry | 'new' | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['fin-journals', source, search, page],
    queryFn: async () => (await api.get<PagedResult<JournalEntry>>('/finance/journals', { params: { source, search, page, pageSize: 25 } })).data,
  })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Journal entries</Typography.Title>
        {can('finance.journals.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New journal</Button>}
      </div>
      <Card>
        <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
          <Col xs={24} md={6}><Select allowClear placeholder="Any source" style={{ width: '100%' }} value={source} onChange={v => { setSource(v); setPage(1) }} options={SOURCES.map(s => ({ value: s, label: s }))} /></Col>
          <Col xs={24} md={8}><Input.Search allowClear placeholder="Number, description or reference" onSearch={v => { setSearch(v); setPage(1) }} /></Col>
        </Row>
        <Table<JournalEntry> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          onRow={j => ({ onClick: () => setViewing(j.id), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Number', dataIndex: 'number', render: (n?: string) => n ?? <Typography.Text type="secondary">Draft</Typography.Text> },
            { title: 'Date', dataIndex: 'date', render: fmtDate },
            { title: 'Description', dataIndex: 'description', ellipsis: true },
            { title: 'Source', dataIndex: 'source', render: (s: JournalSource) => <Tag>{s}</Tag> },
            { title: 'Status', dataIndex: 'status', render: (s: JournalEntry['status']) => <Tag color={JOURNAL_COLORS[s]}>{s}</Tag> },
            { title: 'Amount', align: 'right', render: (_, j) => <Space>{amount(j.total)}<Typography.Text type="secondary">{j.currency}</Typography.Text></Space> },
          ]} />
      </Card>
      {viewing && <JournalView id={viewing} onClose={() => setViewing(null)} onEdit={j => { setViewing(null); setEditing(j) }} onOpen={setViewing} />}
      {editing && <JournalEditor entry={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setViewing} />}
    </>
  )
}

export function JournalView({ id, onClose, onEdit, onOpen }: { id: string; onClose: () => void; onEdit?: (j: JournalEntry) => void; onOpen?: (id: string) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const { data: settings } = useFinanceSettings()
  const { data: j } = useQuery({ queryKey: ['fin-journal', id], queryFn: async () => (await api.get<JournalEntry>(`/finance/journals/${id}`)).data })
  const act = async (fn: () => Promise<{ data: JournalEntry } | unknown>, done: string, close = false) => {
    try {
      const res = await fn() as { data?: JournalEntry }
      message.success(done)
      await qc.invalidateQueries({ queryKey: ['fin-journals'] })
      await qc.invalidateQueries({ queryKey: ['fin-journal', id] })
      if (close) onClose()
      else if (res?.data?.id && res.data.id !== id) onOpen?.(res.data.id)
    } catch (e) { message.error(errorMessage(e)) }
  }
  if (!j) return <Drawer open onClose={onClose} loading />
  const foreign = j.currency !== settings?.baseCurrency

  return (
    <Drawer open onClose={onClose} size={900} title={<Space>{j.number ?? 'Draft journal'}<Tag color={JOURNAL_COLORS[j.status]}>{j.status}</Tag><Tag>{j.source}</Tag></Space>}
      extra={
        <Space>
          {j.status === 'Draft' && can('finance.journals.create', j.entityId) && onEdit && <Button onClick={() => onEdit(j)}>Edit</Button>}
          {j.status === 'Draft' && can('finance.journals.create', j.entityId) && (
            <Popconfirm title="Delete draft?" onConfirm={() => act(() => api.delete(`/finance/journals/${id}`), 'Deleted', true)}><Button danger icon={<DeleteOutlined />} /></Popconfirm>
          )}
          {j.status === 'Draft' && can('finance.journals.post', j.entityId) && (
            <Popconfirm title="Post this journal?" description="Posted entries can't be edited, only reversed." onConfirm={() => act(() => api.post(`/finance/journals/${id}/post`), 'Posted')}>
              <Button type="primary" icon={<SendOutlined />}>Post</Button>
            </Popconfirm>
          )}
          {j.status === 'Posted' && j.source === 'Manual' && can('finance.journals.reverse', j.entityId) && (
            <Popconfirm title="Reverse this journal?" description="A mirror entry dated today will be posted." onConfirm={() => act(() => api.post(`/finance/journals/${id}/reverse`, {}), 'Reversed')}>
              <Button danger icon={<RollbackOutlined />}>Reverse</Button>
            </Popconfirm>
          )}
        </Space>
      }>
      <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered style={{ marginBottom: 16 }}>
        <Descriptions.Item label="Date">{fmtDate(j.date)}</Descriptions.Item>
        <Descriptions.Item label="Entity">{j.entityName}</Descriptions.Item>
        <Descriptions.Item label="Description" span={2}>{j.description}</Descriptions.Item>
        {j.reference && <Descriptions.Item label="Reference">{j.reference}</Descriptions.Item>}
        {foreign && <Descriptions.Item label="Currency">{j.currency} @ {j.exchangeRate}</Descriptions.Item>}
        <Descriptions.Item label="Created by">{j.createdByName ?? 'System'}</Descriptions.Item>
        {j.postedByName && <Descriptions.Item label="Posted">{j.postedByName}, {dayjs(j.postedAt).format('DD MMM YYYY HH:mm')}</Descriptions.Item>}
        {j.reversalOfId && <Descriptions.Item label="Reverses"><a onClick={() => onOpen?.(j.reversalOfId!)}>original entry</a></Descriptions.Item>}
        {j.reversedById && <Descriptions.Item label="Reversed by"><a onClick={() => onOpen?.(j.reversedById!)}>reversal entry</a></Descriptions.Item>}
      </Descriptions>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={j.lines} scroll={{ x: 700 }}
        columns={[
          { title: 'Account', render: (_, l) => <>{l.accountCode} {l.accountName}</> },
          { title: 'Entity', dataIndex: 'entityName' },
          { title: 'Details', render: (_, l) => [l.description, l.contactName].filter(Boolean).join(' · ') },
          { title: 'Debit', align: 'right', render: (_, l) => l.debit || l.baseDebit ? amount(foreign ? l.debit : l.baseDebit) : '' },
          { title: 'Credit', align: 'right', render: (_, l) => l.credit || l.baseCredit ? amount(foreign ? l.credit : l.baseCredit) : '' },
          ...(foreign ? [
            { title: `Debit ${settings?.baseCurrency}`, align: 'right' as const, render: (_: unknown, l: JournalEntry['lines'][number]) => l.baseDebit ? amount(l.baseDebit) : '' },
            { title: `Credit ${settings?.baseCurrency}`, align: 'right' as const, render: (_: unknown, l: JournalEntry['lines'][number]) => l.baseCredit ? amount(l.baseCredit) : '' },
          ] : []),
        ]}
        summary={rows => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={3}><b>Total</b></Table.Summary.Cell>
            <Table.Summary.Cell index={3} align="right"><b>{amount(rows.reduce((s, l) => s + (foreign ? l.debit : l.baseDebit), 0))}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={4} align="right"><b>{amount(rows.reduce((s, l) => s + (foreign ? l.credit : l.baseCredit), 0))}</b></Table.Summary.Cell>
            {foreign && <><Table.Summary.Cell index={5} align="right"><b>{amount(rows.reduce((s, l) => s + l.baseDebit, 0))}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={6} align="right"><b>{amount(rows.reduce((s, l) => s + l.baseCredit, 0))}</b></Table.Summary.Cell></>}
          </Table.Summary.Row>
        )} />
    </Drawer>
  )
}

interface JLine { accountId?: string; debit: number; credit: number; description?: string; entityId?: string; contactId?: string }

function JournalEditor({ entry, onClose, onSaved }: { entry?: JournalEntry; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: settings } = useFinanceSettings()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [lines, setLines] = useState<JLine[]>(entry?.lines.map(l => ({ accountId: l.accountId, debit: l.debit, credit: l.credit, description: l.description, entityId: l.entityId === entry.entityId ? undefined : l.entityId, contactId: l.contactId }))
    ?? [{ debit: 0, credit: 0 }, { debit: 0, credit: 0 }])
  const set = (i: number, patch: Partial<JLine>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)))
  const dr = lines.reduce((s, l) => s + (l.debit || 0), 0)
  const cr = lines.reduce((s, l) => s + (l.credit || 0), 0)
  const currency = Form.useWatch('currency', form) as string | undefined

  const save = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const body = { entityId: v.entityId, date: (v.date as Dayjs).format('YYYY-MM-DD'), reference: v.reference, description: v.description,
        currency: v.currency || null, exchangeRate: v.exchangeRate || null, lines: lines.filter(l => l.accountId) }
      const res = entry ? await api.put<JournalEntry>(`/finance/journals/${entry.id}`, body) : await api.post<JournalEntry>('/finance/journals', body)
      message.success('Draft saved')
      await qc.invalidateQueries({ queryKey: ['fin-journals'] })
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Drawer open onClose={onClose} size={980} title={entry ? 'Edit draft journal' : 'New journal entry'}
      extra={<Button type="primary" loading={busy} onClick={save} disabled={Math.round(dr * 100) !== Math.round(cr * 100) || dr === 0}>Save draft</Button>}>
      <Form form={form} layout="vertical" initialValues={entry ? { ...entry, date: dayjs(entry.date), currency: entry.currency === settings?.baseCurrency ? undefined : entry.currency }
        : { date: dayjs(), entityId: me?.entities.find(e => e.permissions.includes('finance.journals.create'))?.id }}>
        <Row gutter={12}>
          <Col xs={24} md={6}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="finance.journals.create" /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="currency" label="Currency"><Input maxLength={3} placeholder={settings?.baseCurrency} /></Form.Item></Col>
          {currency && currency.toUpperCase() !== settings?.baseCurrency && <Col xs={12} md={6}><Form.Item name="exchangeRate" label="Rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          <Col xs={24} md={16}><Form.Item name="description" label="Description" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="reference" label="Reference"><Input /></Form.Item></Col>
        </Row>
      </Form>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines} scroll={{ x: 900 }}
        columns={[
          { title: 'Account', width: 240, render: (_, l, i) => <AccountSelect value={l.accountId} onChange={v => set(i, { accountId: v })} /> },
          { title: 'Debit', width: 130, render: (_, l, i) => <InputNumber min={0} value={l.debit || undefined} onChange={v => set(i, { debit: v ?? 0, credit: v ? 0 : l.credit })} style={{ width: '100%' }} /> },
          { title: 'Credit', width: 130, render: (_, l, i) => <InputNumber min={0} value={l.credit || undefined} onChange={v => set(i, { credit: v ?? 0, debit: v ? 0 : l.debit })} style={{ width: '100%' }} /> },
          { title: 'Line text', render: (_, l, i) => <Input value={l.description} onChange={e => set(i, { description: e.target.value })} /> },
          { title: 'Contact', width: 170, render: (_, l, i) => <ContactSelect value={l.contactId} onChange={v => set(i, { contactId: v })} /> },
          { title: 'Entity', width: 170, render: (_, l, i) => <EntityPicker value={l.entityId} onChange={v => set(i, { entityId: v })} placeholder="Same as entry" permission="finance.journals.create" /> },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length <= 2} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))} /> },
        ]}
        summary={() => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0}><b>Total</b>{Math.round(dr * 100) !== Math.round(cr * 100) && <Tag color="red" style={{ marginLeft: 8 }}>Out of balance by {amount(Math.abs(dr - cr))}</Tag>}</Table.Summary.Cell>
            <Table.Summary.Cell index={1}><b>{amount(dr)}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={2}><b>{amount(cr)}</b></Table.Summary.Cell>
            <Table.Summary.Cell index={3} colSpan={4} />
          </Table.Summary.Row>
        )} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(ls => [...ls, { debit: 0, credit: 0 }])}>Add line</Button>
    </Drawer>
  )
}

// ======================= Contacts =======================

export function ContactsPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [filter, setFilter] = useState<'all' | 'customers' | 'vendors'>('all')
  const [editing, setEditing] = useState<Contact | 'new' | null>(null)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useContacts(filter === 'all' ? undefined : filter === 'customers' ? { customers: true } : { vendors: true })

  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/finance/contacts', v)
      else if (editing) await api.put(`/finance/contacts/${editing.id}`, v)
      message.success('Saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['fin-contacts'] })
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Customers & vendors</Typography.Title>
        {can('finance.contacts.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New contact</Button>}
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={filter} onChange={v => setFilter(v as typeof filter)}
          options={[{ value: 'all', label: 'All' }, { value: 'customers', label: 'Customers' }, { value: 'vendors', label: 'Vendors' }]} />
        <Table<Contact> rowKey="id" loading={isLoading} dataSource={data} scroll={{ x: 800 }}
          onRow={c => ({ onClick: () => can('finance.contacts.edit') && setEditing(c), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Name', render: (_, c) => <><b>{c.name}</b> <Typography.Text type="secondary">{c.code}</Typography.Text></> },
            { title: 'Type', render: (_, c) => <Space>{c.isCustomer && <Tag color="green">Customer</Tag>}{c.isVendor && <Tag color="orange">Vendor</Tag>}</Space> },
            { title: 'NTN / STRN', render: (_, c) => [c.ntn, c.strn].filter(Boolean).join(' / ') || '—' },
            { title: 'Phone', dataIndex: 'phone' },
            { title: 'Currency', dataIndex: 'currency' },
            { title: 'Receivable', align: 'right', render: (_, c) => c.receivable ? amount(c.receivable) : '' },
            { title: 'Payable', align: 'right', render: (_, c) => c.payable ? amount(c.payable) : '' },
          ]} />
      </Card>
      <Modal open={!!editing} width={640} title={editing === 'new' ? 'New contact' : 'Edit contact'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { isCustomer: true, paymentTermsDays: 30, isActive: true } : editing ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={24}>
              <Form.Item name="isCustomer" valuePropName="checked" noStyle><Checkbox>Customer</Checkbox></Form.Item>
              <Form.Item name="isVendor" valuePropName="checked" noStyle><Checkbox>Vendor</Checkbox></Form.Item>
              <Form.Item name="isActive" valuePropName="checked" noStyle><Checkbox>Active</Checkbox></Form.Item>
            </Col>
            <Col xs={12} style={{ marginTop: 12 }}><Form.Item name="ntn" label="NTN"><Input /></Form.Item></Col>
            <Col xs={12} style={{ marginTop: 12 }}><Form.Item name="strn" label="STRN (sales tax reg.)"><Input /></Form.Item></Col>
            <Col xs={12}><Form.Item name="email" label="Email" rules={[{ type: 'email' }]}><Input /></Form.Item></Col>
            <Col xs={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="address" label="Address"><Input /></Form.Item></Col>
            <Col xs={8}><Form.Item name="city" label="City"><Input /></Form.Item></Col>
            <Col xs={8}><Form.Item name="currency" label="Currency" extra="Blank = base"><Input maxLength={3} /></Form.Item></Col>
            <Col xs={8}><Form.Item name="paymentTermsDays" label="Payment terms (days)"><InputNumber min={0} max={365} style={{ width: '100%' }} /></Form.Item></Col>
            <Form.Item noStyle shouldUpdate={(a, b) => a.isVendor !== b.isVendor}>
              {({ getFieldValue }) => getFieldValue('isVendor') && <>
                <Col xs={16}><Form.Item name="defaultWhtRateId" label="Income tax withholding when paying" extra="Suggested on every payment to this vendor"><WhtRateSelect /></Form.Item></Col>
                <Col xs={8}><Form.Item name="notOnActiveTaxpayerList" label="Not on FBR's ATL" valuePropName="checked" tooltip="Suppliers missing from the Active Taxpayer List suffer double withholding"><Switch /></Form.Item></Col>
              </>}
            </Form.Item>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

/** Pay a posted payroll run's net salaries from a bank account (used on the payroll run page). */
export function PaySalariesModal({ runId, onClose, onDone }: { runId: string; onClose: () => void; onDone: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const save = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/finance/payments/payroll/${runId}`, { bankAccountId: v.bankAccountId, date: (v.date as Dayjs).format('YYYY-MM-DD') })
      message.success('Salary payment recorded in the ledger')
      onDone()
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title="Record salary transfer" onCancel={onClose} onOk={save} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ date: dayjs() }}>
        <Form.Item name="bankAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item>
        <Form.Item name="date" label="Transfer date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
      </Form>
      <Typography.Paragraph type="secondary">Posts Dr Salaries payable / Cr Bank for the run's total net pay.</Typography.Paragraph>
    </Modal>
  )
}
