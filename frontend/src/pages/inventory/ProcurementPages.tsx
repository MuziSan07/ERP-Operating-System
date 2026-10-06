import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Button, Card, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Popconfirm, Progress, Row, Segmented, Select, Space, Table,
  Tabs, Tag, Typography,
} from 'antd'
import { CheckOutlined, CloseOutlined, DeleteOutlined, FileDoneOutlined, InboxOutlined, PlusOutlined, PrinterOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { useNavigate } from 'react-router-dom'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  PO_COLORS, PR_COLORS, qty, spaced, type GoodsReceipt, type GrniRow, type PurchaseOrder, type PurchaseOrderListItem, type PurchaseOrderStatus,
  type PurchaseRequest, type PurchaseRequestStatus,
} from '../../api/inventory'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { ContactSelect, CurrencyTag, TaxRateSelect, useFinanceSettings } from '../../components/FinancePickers'
import { JournalView } from '../finance/LedgerPages'
import { ItemSelect, WarehouseSelect, useItems } from './InventoryPages'

// ======================= Purchase requests =======================

export function PurchaseRequestsPage() {
  const { can, me } = useAuth()
  const [status, setStatus] = useState<PurchaseRequestStatus | 'All'>('All')
  const [mine, setMine] = useState(false)
  const [editing, setEditing] = useState<PurchaseRequest | 'new' | null>(null)
  const [viewing, setViewing] = useState<string | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['proc-prs', status, mine],
    queryFn: async () => (await api.get<PagedResult<PurchaseRequest>>('/procurement/requests', { params: { status: status === 'All' ? undefined : status, mine, pageSize: 100 } })).data,
  })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Purchase requests</Typography.Title>
        {can('procurement.purchase_requests.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New request</Button>}
      </div>
      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Segmented value={status} onChange={v => setStatus(v as typeof status)} options={['All', 'Draft', 'Submitted', 'Approved', 'Ordered', 'Rejected'].map(s => ({ value: s, label: s === 'Submitted' ? 'Awaiting approval' : s }))} />
          <Segmented value={mine ? 'mine' : 'all'} onChange={v => setMine(v === 'mine')} options={[{ value: 'all', label: 'All I can see' }, { value: 'mine', label: 'Mine' }]} />
        </Space>
        <Table<PurchaseRequest> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          onRow={r => ({ onClick: () => setViewing(r.id), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Number', dataIndex: 'number' },
            { title: 'Purpose', dataIndex: 'purpose', ellipsis: true },
            { title: 'Branch', dataIndex: 'entityName' },
            { title: 'Requested by', dataIndex: 'requestedByName' },
            { title: 'Date', dataIndex: 'date', render: fmtDate },
            { title: 'Needed by', dataIndex: 'requiredBy', render: (d?: string) => d ? fmtDate(d) : '—' },
            { title: 'Status', dataIndex: 'status', render: (s: PurchaseRequestStatus) => <Tag color={PR_COLORS[s]}>{s === 'Submitted' ? 'Awaiting approval' : s}</Tag> },
            { title: 'Estimate', align: 'right', render: (_, r) => amount(r.estimatedTotal, 0) },
          ]} />
      </Card>
      {editing && <RequestEditor pr={editing === 'new' ? undefined : editing} defaultEntity={me?.user.primaryEntityId} onClose={() => setEditing(null)} onSaved={setViewing} />}
      {viewing && <RequestView id={viewing} onClose={() => setViewing(null)} onEdit={pr => { setViewing(null); setEditing(pr) }} />}
    </>
  )
}

interface PrLine { itemId?: string; quantity: number; estimatedUnitPrice: number; notes?: string }

function RequestEditor({ pr, defaultEntity, onClose, onSaved }: { pr?: PurchaseRequest; defaultEntity?: string; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const { data: items = [] } = useItems()
  const [lines, setLines] = useState<PrLine[]>(pr?.lines.map(l => ({ itemId: l.itemId, quantity: l.quantity, estimatedUnitPrice: l.estimatedUnitPrice, notes: l.notes })) ?? [{ quantity: 1, estimatedUnitPrice: 0 }])
  const set = (i: number, p: Partial<PrLine>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...p } : l)))
  const save = async () => {
    const v = await form.validateFields()
    if (lines.some(l => !l.itemId)) { message.error('Choose an item on every line.'); return }
    setBusy(true)
    try {
      const body = { entityId: v.entityId, date: (v.date as Dayjs).format('YYYY-MM-DD'), requiredBy: v.requiredBy?.format('YYYY-MM-DD'), purpose: v.purpose, lines }
      const res = pr ? await api.put<PurchaseRequest>(`/procurement/requests/${pr.id}`, body) : await api.post<PurchaseRequest>('/procurement/requests', body)
      message.success('Saved as draft — submit it for approval')
      await qc.invalidateQueries({ queryKey: ['proc-prs'] })
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Drawer open onClose={onClose} size={860} title={pr ? `Edit ${pr.number}` : 'New purchase request'} extra={<Button type="primary" loading={busy} onClick={save}>Save draft</Button>}>
      <Form form={form} layout="vertical" initialValues={pr ? { ...pr, date: dayjs(pr.date), requiredBy: pr.requiredBy ? dayjs(pr.requiredBy) : undefined } : { date: dayjs(), entityId: defaultEntity }}>
        <Row gutter={12}>
          <Col xs={24} md={10}><Form.Item name="entityId" label="For branch / department" rules={[{ required: true }]}><EntityPicker permission="procurement.purchase_requests.create" /></Form.Item></Col>
          <Col xs={12} md={7}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={7}><Form.Item name="requiredBy" label="Needed by"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={24}><Form.Item name="purpose" label="Purpose" rules={[{ required: true }]}><Input placeholder="e.g. Kitchen restock for the conference" /></Form.Item></Col>
        </Row>
      </Form>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines}
        columns={[
          { title: 'Item', width: 280, render: (_, l, i) => <ItemSelect value={l.itemId} onChange={v => set(i, { itemId: v, estimatedUnitPrice: l.estimatedUnitPrice || items.find(x => x.id === v)?.standardCost || 0 })} /> },
          { title: 'Qty', width: 110, render: (_, l, i) => <InputNumber min={0} value={l.quantity} onChange={v => set(i, { quantity: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Est. unit price', width: 140, render: (_, l, i) => <InputNumber min={0} value={l.estimatedUnitPrice} onChange={v => set(i, { estimatedUnitPrice: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Notes', render: (_, l, i) => <Input value={l.notes} onChange={e => set(i, { notes: e.target.value })} /> },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length === 1} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(ls => [...ls, { quantity: 1, estimatedUnitPrice: 0 }])}>Add item</Button>
    </Drawer>
  )
}

function RequestView({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit: (pr: PurchaseRequest) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me, can } = useAuth()
  const navigate = useNavigate()
  const [comment, setComment] = useState('')
  const { data: pr } = useQuery({ queryKey: ['proc-pr', id], queryFn: async () => (await api.get<PurchaseRequest>(`/procurement/requests/${id}`)).data })
  const act = async (path: string, body: unknown, done: string) => {
    try {
      await api.post(`/procurement/requests/${id}/${path}`, body)
      message.success(done)
      await qc.invalidateQueries({ queryKey: ['proc-prs'] })
      await qc.invalidateQueries({ queryKey: ['proc-pr', id] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  if (!pr) return <Drawer open onClose={onClose} loading />
  const isMine = pr.requestedByName === me?.user.fullName
  const canDecide = pr.status === 'Submitted' && !isMine && can('procurement.purchase_requests.approve', pr.entityId)
  return (
    <Drawer open onClose={onClose} size={760} title={<Space>{pr.number}<Tag color={PR_COLORS[pr.status]}>{pr.status}</Tag></Space>}
      extra={
        <Space>
          {(pr.status === 'Draft' || pr.status === 'Rejected') && <Button onClick={() => onEdit(pr)}>Edit</Button>}
          {pr.status === 'Draft' && <Button type="primary" icon={<SendOutlined />} onClick={() => act('submit', {}, 'Submitted for approval')}>Submit</Button>}
          {(pr.status === 'Approved') && can('procurement.purchase_orders.create') && <Button type="primary" onClick={() => navigate(`/procurement/orders?fromRequest=${pr.id}`)}>Create purchase order</Button>}
          {['Draft', 'Submitted', 'Approved', 'Rejected'].includes(pr.status) && (
            <Popconfirm title="Cancel this request?" onConfirm={() => act('cancel', {}, 'Cancelled')}><Button danger icon={<StopOutlined />} /></Popconfirm>
          )}
        </Space>
      }
      footer={canDecide && (
        <Space orientation="vertical" style={{ width: '100%' }}>
          <Input.TextArea rows={2} placeholder="Comment (required to reject)" value={comment} onChange={e => setComment(e.target.value)} />
          <Space>
            <Button type="primary" icon={<CheckOutlined />} onClick={() => act('decision', { approve: true, comment }, 'Approved')}>Approve</Button>
            <Button danger icon={<CloseOutlined />} onClick={() => act('decision', { approve: false, comment }, 'Rejected')}>Reject</Button>
          </Space>
        </Space>
      )}>
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }} style={{ marginBottom: 16 }}>
        <Descriptions.Item label="Purpose" span={2}>{pr.purpose}</Descriptions.Item>
        <Descriptions.Item label="Branch">{pr.entityName}</Descriptions.Item>
        <Descriptions.Item label="Requested by">{pr.requestedByName}</Descriptions.Item>
        <Descriptions.Item label="Date">{fmtDate(pr.date)}</Descriptions.Item>
        <Descriptions.Item label="Needed by">{pr.requiredBy ? fmtDate(pr.requiredBy) : '—'}</Descriptions.Item>
        {pr.approvedByName && <Descriptions.Item label={pr.status === 'Rejected' ? 'Rejected by' : 'Approved by'}>{pr.approvedByName}, {dayjs(pr.approvedAt).format('DD MMM HH:mm')}</Descriptions.Item>}
        {pr.decisionComment && <Descriptions.Item label="Comment">{pr.decisionComment}</Descriptions.Item>}
      </Descriptions>
      <Table size="small" pagination={false} rowKey="id" dataSource={pr.lines}
        columns={[
          { title: 'Item', render: (_, l) => `${l.itemCode} ${l.itemName}` },
          { title: 'Qty', align: 'right', render: (_, l) => `${qty(l.quantity)} ${l.unit}` },
          { title: 'Est. price', align: 'right', render: (_, l) => amount(l.estimatedUnitPrice) },
          { title: 'Ordered', align: 'right', render: (_, l) => qty(l.quantityOrdered) },
          { title: 'Notes', dataIndex: 'notes' },
        ]} />
    </Drawer>
  )
}

// ======================= Purchase orders =======================

export function PurchaseOrdersPage() {
  const { can } = useAuth()
  const fromRequest = new URLSearchParams(location.search).get('fromRequest')
  const [status, setStatus] = useState<PurchaseOrderStatus | 'All'>('All')
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<PurchaseOrder | 'new' | null>(fromRequest ? 'new' : null)
  const [viewing, setViewing] = useState<string | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['proc-pos', status, search],
    queryFn: async () => (await api.get<PagedResult<PurchaseOrderListItem>>('/procurement/orders', { params: { status: status === 'All' ? undefined : status, search, pageSize: 100 } })).data,
  })
  const { data: settings } = useFinanceSettings()
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Purchase orders</Typography.Title>
        {can('procurement.purchase_orders.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New purchase order</Button>}
      </div>
      <Tabs items={[
        {
          key: 'orders', label: 'Orders', children: (
            <Card>
              <Space wrap style={{ marginBottom: 16 }}>
                <Segmented value={status} onChange={v => setStatus(v as typeof status)}
                  options={['All', 'Draft', 'Approved', 'PartiallyReceived', 'Received', 'Closed'].map(s => ({ value: s, label: spaced(s) }))} />
                <Input.Search allowClear placeholder="Number or vendor" onSearch={setSearch} style={{ width: 220 }} />
              </Space>
              <Table<PurchaseOrderListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 950 }}
                onRow={o => ({ onClick: () => setViewing(o.id), style: { cursor: 'pointer' } })}
                columns={[
                  { title: 'Number', dataIndex: 'number' },
                  { title: 'Vendor', dataIndex: 'vendorName' },
                  { title: 'Deliver to', dataIndex: 'warehouseName' },
                  { title: 'Date', dataIndex: 'date', render: fmtDate },
                  { title: 'Status', dataIndex: 'status', render: (s: PurchaseOrderStatus) => <Tag color={PO_COLORS[s]}>{spaced(s)}</Tag> },
                  { title: 'Received', width: 120, render: (_, o) => <Progress percent={o.receivedPercent} size="small" /> },
                  { title: 'Billed', width: 120, render: (_, o) => <Progress percent={o.billedPercent} size="small" strokeColor="#722ed1" /> },
                  { title: 'Total', align: 'right', render: (_, o) => <Space>{amount(o.total)}<CurrencyTag currency={o.currency} base={settings?.baseCurrency} /></Space> },
                ]} />
            </Card>
          ),
        },
        { key: 'grni', label: 'Received, not billed', children: <Grni onOpen={setViewing} /> },
      ]} />
      {editing && <OrderEditor po={editing === 'new' ? undefined : editing} fromRequest={fromRequest ?? undefined} onClose={() => { setEditing(null); if (fromRequest) history.replaceState({}, '', '/procurement/orders') }} onSaved={setViewing} />}
      {viewing && <OrderView id={viewing} onClose={() => setViewing(null)} onEdit={po => { setViewing(null); setEditing(po) }} />}
    </>
  )
}

function Grni({ onOpen }: { onOpen: (id: string) => void }) {
  const { data = [], isLoading } = useQuery({ queryKey: ['proc-grni'], queryFn: async () => (await api.get<GrniRow[]>('/procurement/grni')).data })
  return (
    <Card>
      <Typography.Paragraph type="secondary">Goods received whose vendor bill hasn't been approved yet — the detail behind the "Goods received not invoiced" account.</Typography.Paragraph>
      <Table<GrniRow> rowKey={(_, i) => String(i)} loading={isLoading} dataSource={data} onRow={r => ({ onClick: () => onOpen(r.purchaseOrderId), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'PO', dataIndex: 'purchaseOrderNumber' }, { title: 'Vendor', dataIndex: 'vendorName' },
          { title: 'Item', render: (_, r) => `${r.itemCode} ${r.itemName}` },
          { title: 'Received', dataIndex: 'quantityReceived', align: 'right', render: qty }, { title: 'Billed', dataIndex: 'quantityBilled', align: 'right', render: qty },
          { title: 'Unbilled value', dataIndex: 'unbilledValue', align: 'right', render: (v: number) => <b>{amount(v)}</b> },
        ]}
        summary={rows => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={5}><b>Total</b></Table.Summary.Cell><Table.Summary.Cell index={5} align="right"><b>{amount(rows.reduce((s, r) => s + r.unbilledValue, 0))}</b></Table.Summary.Cell></Table.Summary.Row>} />
    </Card>
  )
}

interface PoLineDraft { itemId?: string; description?: string; quantity: number; unitPrice: number; taxRateId?: string; purchaseRequestLineId?: string }

function OrderEditor({ po, fromRequest, onClose, onSaved }: { po?: PurchaseOrder; fromRequest?: string; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: settings } = useFinanceSettings()
  const { data: items = [] } = useItems()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [lines, setLines] = useState<PoLineDraft[]>(po?.lines.map(l => ({ itemId: l.itemId, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, taxRateId: l.taxRateId, purchaseRequestLineId: l.purchaseRequestLineId })) ?? [{ quantity: 1, unitPrice: 0 }])
  const { data: approvedPrs } = useQuery({ queryKey: ['proc-prs', 'Approved', 'pick'], queryFn: async () => (await api.get<PagedResult<PurchaseRequest>>('/procurement/requests', { params: { status: 'Approved', pageSize: 100 } })).data })
  const [loadedPr, setLoadedPr] = useState(false)
  const set = (i: number, p: Partial<PoLineDraft>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...p } : l)))
  const addFromRequest = (prId: string) => {
    const pr = approvedPrs?.items.find(r => r.id === prId)
    if (!pr) return
    const fresh = pr.lines.filter(l => l.quantity > l.quantityOrdered).map(l => ({ itemId: l.itemId, quantity: l.quantity - l.quantityOrdered, unitPrice: l.estimatedUnitPrice,
      taxRateId: items.find(i => i.id === l.itemId)?.purchaseTaxRateId, purchaseRequestLineId: l.id }))
    setLines(ls => [...ls.filter(l => l.itemId), ...fresh])
    form.setFieldValue('entityId', form.getFieldValue('entityId') ?? pr.entityId)
  }
  if (fromRequest && !loadedPr && approvedPrs) { setLoadedPr(true); addFromRequest(fromRequest) }
  const subtotal = lines.reduce((s, l) => s + l.quantity * l.unitPrice, 0)

  const save = async () => {
    const v = await form.validateFields()
    if (lines.some(l => !l.itemId)) { message.error('Choose an item on every line.'); return }
    setBusy(true)
    try {
      const body = { entityId: v.entityId, vendorId: v.vendorId, warehouseId: v.warehouseId, date: (v.date as Dayjs).format('YYYY-MM-DD'), expectedDate: v.expectedDate?.format('YYYY-MM-DD'),
        currency: v.currency || null, exchangeRate: v.exchangeRate || null, terms: v.terms, notes: v.notes, lines }
      const res = po ? await api.put<PurchaseOrder>(`/procurement/orders/${po.id}`, body) : await api.post<PurchaseOrder>('/procurement/orders', body)
      message.success('Purchase order saved as draft — it needs approval by someone else')
      await qc.invalidateQueries({ queryKey: ['proc-pos'] })
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  const currency = Form.useWatch('currency', form) as string | undefined

  return (
    <Drawer open onClose={onClose} size={1000} title={po ? `Edit ${po.number}` : 'New purchase order'} extra={<Button type="primary" loading={busy} onClick={save}>Save draft</Button>}>
      <Form form={form} layout="vertical" initialValues={po ? { ...po, date: dayjs(po.date), expectedDate: po.expectedDate ? dayjs(po.expectedDate) : undefined, currency: po.currency === settings?.baseCurrency ? undefined : po.currency }
        : { date: dayjs(), entityId: me?.entities.find(e => e.permissions.includes('procurement.purchase_orders.create'))?.id }}>
        <Row gutter={12}>
          <Col xs={24} md={9}><Form.Item name="vendorId" label="Vendor" rules={[{ required: true }]}><ContactSelect vendors /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="warehouseId" label="Deliver to warehouse" rules={[{ required: true }]}><WarehouseSelect permission="procurement.purchase_orders.create" /></Form.Item></Col>
          <Col xs={24} md={7}><Form.Item name="entityId" label="Ordering branch" rules={[{ required: true }]}><EntityPicker permission="procurement.purchase_orders.create" /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="date" label="Order date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="expectedDate" label="Expected delivery"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="currency" label="Currency"><Input maxLength={3} placeholder={settings?.baseCurrency} /></Form.Item></Col>
          {currency && currency.toUpperCase() !== settings?.baseCurrency && <Col xs={12} md={4}><Form.Item name="exchangeRate" label="Rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>}
          <Col xs={24} md={6}>
            <Form.Item label="Add lines from approved request">
              <Select allowClear placeholder="Choose request" onChange={(v?: string) => v && addFromRequest(v)} options={approvedPrs?.items.map(r => ({ value: r.id, label: `${r.number} — ${r.purpose}` }))} />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines} scroll={{ x: 850 }}
        columns={[
          { title: 'Item', width: 260, render: (_, l, i) => <ItemSelect value={l.itemId} onChange={v => set(i, { itemId: v, taxRateId: l.taxRateId ?? items.find(x => x.id === v)?.purchaseTaxRateId, unitPrice: l.unitPrice || items.find(x => x.id === v)?.standardCost || 0 })} /> },
          { title: 'Qty', width: 100, render: (_, l, i) => <InputNumber min={0} value={l.quantity} onChange={v => set(i, { quantity: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Unit price', width: 130, render: (_, l, i) => <InputNumber min={0} value={l.unitPrice} onChange={v => set(i, { unitPrice: v ?? 0 })} style={{ width: '100%' }} /> },
          { title: 'Tax', width: 170, render: (_, l, i) => <TaxRateSelect value={l.taxRateId} onChange={v => set(i, { taxRateId: v })} /> },
          { title: 'Amount', align: 'right', render: (_, l) => amount(l.quantity * l.unitPrice) },
          { title: '', render: (_, l) => l.purchaseRequestLineId && <Tag>From request</Tag> },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length === 1} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))} /> },
        ]}
        summary={() => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={4}><b>Subtotal (excl. tax)</b></Table.Summary.Cell><Table.Summary.Cell index={4} align="right"><b>{amount(subtotal)}</b></Table.Summary.Cell><Table.Summary.Cell index={5} colSpan={2} /></Table.Summary.Row>} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(ls => [...ls, { quantity: 1, unitPrice: 0 }])}>Add line</Button>
      <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="terms" label="Terms (delivery, payment, warranty)"><Input.TextArea rows={2} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="notes" label="Notes"><Input.TextArea rows={2} /></Form.Item></Col>
        </Row>
      </Form>
    </Drawer>
  )
}

function OrderView({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit: (po: PurchaseOrder) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [receiving, setReceiving] = useState(false)
  const [grn, setGrn] = useState<string | null>(null)
  const { data: po } = useQuery({ queryKey: ['proc-po', id], queryFn: async () => (await api.get<PurchaseOrder>(`/procurement/orders/${id}`)).data })
  const refresh = async () => { await qc.invalidateQueries({ queryKey: ['proc-pos'] }); await qc.invalidateQueries({ queryKey: ['proc-po', id] }); await qc.invalidateQueries({ queryKey: ['proc-grni'] }) }
  const act = async (fn: () => Promise<unknown>, done: string) => { try { await fn(); message.success(done); await refresh() } catch (e) { message.error(errorMessage(e)) } }
  if (!po) return <Drawer open onClose={onClose} loading />
  const open = po.status === 'Approved' || po.status === 'PartiallyReceived'
  const unbilled = po.lines.some(l => l.quantityReceived > l.quantityBilled)

  return (
    <Drawer open onClose={onClose} size={980} title={<Space>{po.number}<Tag color={PO_COLORS[po.status]}>{spaced(po.status)}</Tag></Space>}
      extra={
        <Space wrap>
          {po.status === 'Draft' && can('procurement.purchase_orders.edit', po.entityId) && <Button onClick={() => onEdit(po)}>Edit</Button>}
          {po.status === 'Draft' && can('procurement.purchase_orders.approve', po.entityId) && (
            <Popconfirm title="Approve this purchase order?" onConfirm={() => act(() => api.post(`/procurement/orders/${id}/approve`), 'Approved')}><Button type="primary" icon={<CheckOutlined />}>Approve</Button></Popconfirm>
          )}
          {open && can('procurement.goods_receipts.create') && <Button type="primary" icon={<InboxOutlined />} onClick={() => setReceiving(true)}>Receive goods</Button>}
          {unbilled && can('finance.bills.create', po.entityId) && (
            <Button icon={<FileDoneOutlined />} onClick={() => act(async () => { await api.post(`/procurement/orders/${id}/bill`); navigate('/finance/bills') }, 'Draft bill created — review and approve it in Purchase bills')}>Create bill</Button>
          )}
          {po.status !== 'Closed' && po.status !== 'Cancelled' && po.status !== 'Draft' && can('procurement.purchase_orders.approve', po.entityId) && (
            <Popconfirm title={po.lines.some(l => l.quantityReceived > 0) ? 'Close the order? Undelivered quantities are dropped.' : 'Cancel this order?'} onConfirm={() => act(() => api.post(`/procurement/orders/${id}/close`), 'Order closed')}>
              <Button danger icon={<StopOutlined />}>{po.lines.some(l => l.quantityReceived > 0) ? 'Close' : 'Cancel'}</Button>
            </Popconfirm>
          )}
          <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print</Button>
        </Space>
      }>
      <div className="print-area">
        <Row justify="space-between">
          <Col><Typography.Title level={3} style={{ margin: 0 }}>Purchase Order</Typography.Title><Typography.Text strong>{po.entityName}</Typography.Text></Col>
          <Col style={{ textAlign: 'right' }}><b>{po.number}</b><div>Date: {fmtDate(po.date)}</div>{po.expectedDate && <div>Delivery by: {fmtDate(po.expectedDate)}</div>}</Col>
        </Row>
        <Row gutter={16} style={{ margin: '16px 0' }}>
          <Col xs={24} md={12}><Card size="small"><Typography.Text type="secondary">Vendor</Typography.Text><div><b>{po.vendorName}</b></div>{po.vendorAddress && <div>{po.vendorAddress}</div>}{(po.vendorNtn || po.vendorStrn) && <div>NTN {po.vendorNtn ?? '—'} · STRN {po.vendorStrn ?? '—'}</div>}</Card></Col>
          <Col xs={24} md={12}><Card size="small"><Typography.Text type="secondary">Deliver to</Typography.Text><div><b>{po.warehouseName}</b></div>{po.approvedByName && <div>Approved by {po.approvedByName}</div>}</Card></Col>
        </Row>
        <Table size="small" pagination={false} rowKey="id" dataSource={po.lines}
          columns={[
            { title: 'Item', render: (_, l) => <>{l.itemCode} {l.description}{l.itemType === 'Service' && <Tag style={{ marginLeft: 6 }}>Service</Tag>}</> },
            { title: 'Qty', align: 'right', render: (_, l) => `${qty(l.quantity)} ${l.unit}` },
            { title: 'Rate', align: 'right', render: (_, l) => amount(l.unitPrice) },
            { title: 'Tax', render: (_, l) => l.taxRateName ?? '—' },
            { title: 'Amount', align: 'right', render: (_, l) => amount(l.amount) },
            { title: 'Received', align: 'right', render: (_, l) => <span style={{ color: l.quantityReceived >= l.quantity ? '#3f8600' : undefined }}>{qty(l.quantityReceived)}</span> },
            { title: 'Billed', align: 'right', render: (_, l) => qty(l.quantityBilled) },
          ]} />
        <Row justify="end" style={{ marginTop: 12 }}>
          <Col xs={24} sm={10}>
            <Descriptions size="small" bordered column={1}>
              <Descriptions.Item label="Subtotal">{amount(po.subtotal)}</Descriptions.Item>
              <Descriptions.Item label="Sales tax">{amount(po.taxTotal)}</Descriptions.Item>
              <Descriptions.Item label={<b>Total {po.currency}</b>}><b>{amount(po.total)}</b></Descriptions.Item>
            </Descriptions>
          </Col>
        </Row>
        {po.terms && <Typography.Paragraph style={{ marginTop: 12 }}><b>Terms:</b> {po.terms}</Typography.Paragraph>}
      </div>
      <Row gutter={16} style={{ marginTop: 16 }}>
        <Col xs={24} md={12}>
          <Card size="small" title="Goods receipts">
            {po.receipts.length === 0 ? <Typography.Text type="secondary">Nothing received yet</Typography.Text> : po.receipts.map(r => (
              <div key={r.id}><a onClick={() => setGrn(r.id)}>{r.number}</a> · {fmtDate(r.date)} · {amount(r.value)}</div>
            ))}
          </Card>
        </Col>
        <Col xs={24} md={12}>
          <Card size="small" title="Bills">
            {po.bills.length === 0 ? <Typography.Text type="secondary">No bills yet</Typography.Text> : po.bills.map(b => (
              <div key={b.id}>{b.number ?? 'Draft'} · {fmtDate(b.date)} · {amount(b.value)} <Tag>{b.status}</Tag></div>
            ))}
          </Card>
        </Col>
      </Row>
      {receiving && <ReceiveModal po={po} onClose={() => setReceiving(false)} onDone={refresh} />}
      {grn && <ReceiptView id={grn} onClose={() => setGrn(null)} />}
    </Drawer>
  )
}

interface GrnDraft { quantity: number; batchNo?: string; expiryDate?: Dayjs | null }

function ReceiveModal({ po, onClose, onDone }: { po: PurchaseOrder; onClose: () => void; onDone: () => Promise<void> }) {
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)
  const [date, setDate] = useState(dayjs())
  const [deliveryNote, setDeliveryNote] = useState('')
  const openLines = po.lines.filter(l => l.quantityReceived < l.quantity)
  const [draft, setDraft] = useState<Record<string, GrnDraft>>(Object.fromEntries(openLines.map(l => [l.id, { quantity: l.quantity - l.quantityReceived }])))
  const set = (id: string, p: Partial<GrnDraft>) => setDraft(d => ({ ...d, [id]: { ...d[id], ...p } }))
  const save = async () => {
    setBusy(true)
    try {
      await api.post('/procurement/receipts', {
        purchaseOrderId: po.id, date: date.format('YYYY-MM-DD'), deliveryNote,
        lines: openLines.filter(l => draft[l.id]?.quantity > 0).map(l => ({ purchaseOrderLineId: l.id, quantity: draft[l.id].quantity, batchNo: draft[l.id].batchNo, expiryDate: draft[l.id].expiryDate?.format('YYYY-MM-DD') })),
      })
      message.success('Goods received into stock')
      await onDone()
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Modal open width={900} title={`Receive goods — ${po.number}`} onCancel={onClose} onOk={save} okText="Post goods receipt" confirmLoading={busy}>
      <Space wrap style={{ marginBottom: 12 }}>
        <DatePicker value={date} onChange={d => d && setDate(d)} format="DD MMM YYYY" allowClear={false} />
        <Input placeholder="Vendor delivery note no." value={deliveryNote} onChange={e => setDeliveryNote(e.target.value)} style={{ width: 220 }} />
        <Typography.Text type="secondary">Into {po.warehouseName}</Typography.Text>
      </Space>
      <Table size="small" pagination={false} rowKey="id" dataSource={openLines} scroll={{ x: 750 }}
        columns={[
          { title: 'Item', render: (_, l) => `${l.itemCode} ${l.description}` },
          { title: 'Outstanding', align: 'right', render: (_, l) => `${qty(l.quantity - l.quantityReceived)} ${l.unit}` },
          { title: 'Receiving', width: 120, render: (_, l) => <InputNumber min={0} max={l.quantity - l.quantityReceived} value={draft[l.id]?.quantity} onChange={v => set(l.id, { quantity: v ?? 0 })} style={{ width: '100%' }} /> },
          {
            title: 'Batch / expiry', render: (_, l) => l.trackBatches ? (
              <Space.Compact>
                <Input placeholder="Batch no." value={draft[l.id]?.batchNo} onChange={e => set(l.id, { batchNo: e.target.value })} style={{ width: 120 }} status={draft[l.id]?.quantity > 0 && !draft[l.id]?.batchNo ? 'error' : undefined} />
                {l.trackExpiry && <DatePicker placeholder="Expiry" value={draft[l.id]?.expiryDate} onChange={d => set(l.id, { expiryDate: d })} status={draft[l.id]?.quantity > 0 && !draft[l.id]?.expiryDate ? 'error' : undefined} />}
              </Space.Compact>
            ) : null,
          },
        ]} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, fontSize: 12 }}>
        Stock comes in at the PO price and is accrued to "Goods received not invoiced" until the vendor's bill is approved.
        Received more than one batch? Post one receipt per batch.
      </Typography.Paragraph>
    </Modal>
  )
}

function ReceiptView({ id, onClose }: { id: string; onClose: () => void }) {
  const { can } = useAuth()
  const [journal, setJournal] = useState(false)
  const { data: g } = useQuery({ queryKey: ['proc-grn', id], queryFn: async () => (await api.get<GoodsReceipt>(`/procurement/receipts/${id}`)).data })
  if (!g) return <Drawer open onClose={onClose} loading />
  return (
    <Drawer open onClose={onClose} size={760} title={`${g.number} — ${g.purchaseOrderNumber}`} extra={g.journalEntryId && can('finance.journals.view') && <Button onClick={() => setJournal(true)}>Ledger entry</Button>}>
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }} style={{ marginBottom: 16 }}>
        <Descriptions.Item label="Vendor">{g.vendorName}</Descriptions.Item>
        <Descriptions.Item label="Warehouse">{g.warehouseName}</Descriptions.Item>
        <Descriptions.Item label="Date">{fmtDate(g.date)}</Descriptions.Item>
        <Descriptions.Item label="Delivery note">{g.deliveryNote ?? '—'}</Descriptions.Item>
        <Descriptions.Item label="Received by">{g.createdByName}</Descriptions.Item>
        <Descriptions.Item label="Value">{amount(g.totalValue)}</Descriptions.Item>
      </Descriptions>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={g.lines}
        columns={[
          { title: 'Item', render: (_, l) => `${l.itemCode} ${l.itemName}` },
          { title: 'Batch', render: (_, l) => l.batchNo ? `${l.batchNo}${l.expiryDate ? ` (exp ${fmtDate(l.expiryDate)})` : ''}` : '' },
          { title: 'Qty', align: 'right', render: (_, l) => `${qty(l.quantity)} ${l.unit}` },
          { title: 'Unit cost', align: 'right', render: (_, l) => amount(l.unitCost) },
          { title: 'Value', align: 'right', render: (_, l) => amount(l.value) },
        ]} />
      {journal && g.journalEntryId && <JournalView id={g.journalEntryId} onClose={() => setJournal(false)} />}
    </Drawer>
  )
}
