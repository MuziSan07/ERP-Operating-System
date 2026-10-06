import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Popconfirm, Progress, Result, Row,
  Segmented, Select, Space, Statistic, Switch, Table, Tabs, Tag, Timeline, Typography,
} from 'antd'
import { CarOutlined, CheckOutlined, DeleteOutlined, EditOutlined, FlagOutlined, PlusOutlined, PrinterOutlined, SendOutlined, StopOutlined, WarningOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  EXPENSE_TYPES, MODE_COLORS, SHIPMENT_COLORS, SHIPMENT_STATUSES, TRIP_COLORS, VEHICLE_COLORS, VEHICLE_TYPES, kg, words, type Driver, type FreightRoute,
  type LogisticsDashboard, type Maintenance, type Quote, type Shipment, type ShipmentListItem, type ShipmentStatus, type Trip, type TripListItem,
  type TripStatus, type Vehicle,
} from '../../api/logistics'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect, TaxRateSelect } from '../../components/FinancePickers'

const useVehicles = () => useQuery({ queryKey: ['log-vehicles'], queryFn: async () => (await api.get<Vehicle[]>('/logistics/vehicles')).data })
const useDrivers = () => useQuery({ queryKey: ['log-drivers'], queryFn: async () => (await api.get<Driver[]>('/logistics/drivers')).data })
const useRoutes = () => useQuery({ queryKey: ['log-routes'], queryFn: async () => (await api.get<FreightRoute[]>('/logistics/routes')).data })
const d8 = (d?: dayjs.Dayjs | null) => d?.format('YYYY-MM-DD')
const dj = (s?: string) => (s ? dayjs(s) : undefined)
const refreshAll = (qc: ReturnType<typeof useQueryClient>) => Promise.all(['log-shipments', 'log-shipment', 'log-trips', 'log-trip', 'log-dash', 'log-vehicles', 'log-drivers']
  .map(k => qc.invalidateQueries({ queryKey: [k] })))

const StatusTag = ({ s }: { s: ShipmentStatus }) => <Tag color={SHIPMENT_COLORS[s]}>{words(s)}</Tag>
const LoadBar = ({ load, cap }: { load: number; cap: number }) => {
  const pct = cap ? Math.round((load / cap) * 100) : 0
  return <Progress percent={pct} size="small" status={pct > 100 ? 'exception' : 'normal'} format={() => `${Math.round(load).toLocaleString()} / ${cap.toLocaleString()} kg`} />
}

// ======================= Dashboard =======================

export function LogisticsDashboardPage() {
  const [trip, setTrip] = useState<string | null>(null)
  const { data, isLoading, error } = useQuery({ queryKey: ['log-dash'], retry: false, queryFn: async () => (await api.get<LogisticsDashboard>('/logistics/dashboard')).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  const by = data?.shipmentsByStatus ?? {}
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Logistics</Typography.Title></div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Booked today', data?.bookedToday], ['Delivered today', data?.deliveredToday], ['On-time delivery', `${data?.onTimePercent ?? 0}%`],
          ['Freight this month', amount(data?.revenueThisMonth, 0)], ['COD to remit', amount(data?.codPendingRemittance, 0)]].map(([t, v]) => (
          <Col key={t as string} xs={12} md={8} xl={4}><Card loading={isLoading} size="small"><Statistic title={t as string} value={v as string} /></Card></Col>
        ))}
        <Col xs={12} md={8} xl={4}><Card loading={isLoading} size="small"><Statistic title="Awaiting delivery" value={(by.Booked ?? 0) + (by.PickedUp ?? 0) + (by.InTransit ?? 0) + (by.AtHub ?? 0) + (by.OutForDelivery ?? 0)} /></Card></Col>
      </Row>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title="Vehicles on the road & planned trips" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.activeTrips} locale={{ emptyText: 'No active trips' }} scroll={{ x: 600 }}
              onRow={t => ({ onClick: () => setTrip(t.id), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Trip', render: (_, t) => <><b>{t.number}</b><div><Typography.Text type="secondary">{t.origin} → {t.destination}</Typography.Text></div></> },
                { title: 'Vehicle / driver', render: (_, t) => <>{t.vehicleNo}<div><Typography.Text type="secondary">{t.driverName}</Typography.Text></div></> },
                { title: 'Load', width: 190, render: (_, t) => <LoadBar load={t.loadKg} cap={t.capacityKg} /> },
                { title: '', dataIndex: 'status', render: (s: TripStatus) => <Tag color={TRIP_COLORS[s]}>{s}</Tag> }]} />
          </Card>
        </Col>
        <Col xs={24} xl={10}>
          <Card title="Consignments by status" loading={isLoading} style={{ marginBottom: 16 }}>
            <Space wrap>{SHIPMENT_STATUSES.filter(s => by[s]).map(s => <Tag key={s} color={SHIPMENT_COLORS[s]}>{words(s)}: {by[s]}</Tag>)}</Space>
            {!Object.keys(by).length && <Typography.Text type="secondary">No consignments yet</Typography.Text>}
            <Typography.Title level={5} style={{ marginTop: 16 }}>Fleet</Typography.Title>
            <Space wrap>{Object.entries(data?.fleetByStatus ?? {}).map(([s, n]) => <Tag key={s} color={VEHICLE_COLORS[s as keyof typeof VEHICLE_COLORS]}>{words(s)}: {n}</Tag>)}</Space>
          </Card>
          <Card title={<Space><WarningOutlined style={{ color: '#fa8c16' }} />Papers expiring (30 days)</Space>} loading={isLoading}>
            <Table size="small" rowKey={a => `${a.kind}-${a.subject}-${a.document}`} pagination={false} dataSource={data?.alerts} locale={{ emptyText: 'All papers valid' }}
              columns={[{ title: 'Vehicle / driver', render: (_, a) => <>{a.subject} <Typography.Text type="secondary">({a.kind})</Typography.Text></> },
                { title: 'Document', dataIndex: 'document' },
                { title: 'Expiry', render: (_, a) => a.expiry ? <Tag color={(a.daysLeft ?? 0) < 0 ? 'red' : 'orange'}>{fmtDate(a.expiry)}{(a.daysLeft ?? 0) < 0 ? ' — expired' : ` — ${a.daysLeft}d`}</Tag> : 'missing' }]} />
          </Card>
        </Col>
      </Row>
      {trip && <TripDrawer id={trip} onClose={() => setTrip(null)} />}
    </>
  )
}

// ======================= Consignments =======================

export function ConsignmentsPage() {
  const { can } = useAuth()
  const { message } = App.useApp()
  const [status, setStatus] = useState<string>('Open')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)
  const [editing, setEditing] = useState<Shipment | 'new' | null>(null)
  const { data, isFetching } = useQuery({ queryKey: ['log-shipments', status, search, page],
    queryFn: async () => (await api.get<PagedResult<ShipmentListItem>>('/logistics/shipments', { params: {
      status: ['Open', 'All'].includes(status) ? undefined : status, openOnly: status === 'Open', search, page, pageSize: 25 } })).data })
  const track = async (cn: string) => {
    if (!cn.trim()) return
    try { setOpen((await api.get<Shipment>(`/logistics/track/${encodeURIComponent(cn.trim())}`)).data.id) } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Consignments</Typography.Title>
        <Space>
          <Input.Search placeholder="Track CN number" enterButton="Track" onSearch={track} style={{ width: 260 }} />
          {can('logistics.shipments.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>Book consignment</Button>}
        </Space>
      </div>
      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Segmented value={status} onChange={v => { setStatus(v as string); setPage(1) }} options={['Open', 'Booked', 'InTransit', 'AtHub', 'Delivered', 'All'].map(s => ({ value: s, label: words(s) }))} />
          <Input.Search allowClear placeholder="CN, customer, consignee, city" onSearch={v => { setSearch(v); setPage(1) }} style={{ width: 260 }} />
        </Space>
        <Table<ShipmentListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 1000 }}
          onRow={s => ({ onClick: () => setOpen(s.id), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'CN', dataIndex: 'number', render: (n: string, s) => <><b>{n}</b>{s.late && <Tag color="red" style={{ marginLeft: 6 }}>Late</Tag>}</> },
            { title: 'Booked', dataIndex: 'bookingDate', render: fmtDate },
            { title: 'Customer', dataIndex: 'customerName' },
            { title: 'Lane', render: (_, s) => `${s.originCity} → ${s.destinationCity}` },
            { title: 'Consignee', dataIndex: 'consigneeName' },
            { title: 'Pcs / weight', render: (_, s) => `${s.pieces} · ${kg(s.weightKg)}` },
            { title: 'Pay', dataIndex: 'paymentMode', render: (m: keyof typeof MODE_COLORS) => <Tag color={MODE_COLORS[m]}>{words(m)}</Tag> },
            { title: 'Freight', align: 'right', render: (_, s) => amount(s.total, 0) },
            { title: 'COD', align: 'right', render: (_, s) => s.codAmount ? amount(s.codAmount, 0) : '—' },
            { title: 'Trip', dataIndex: 'tripNumber', render: (t?: string) => t ?? '—' },
            { title: 'Status', dataIndex: 'status', render: (s: ShipmentStatus) => <StatusTag s={s} /> },
          ]} />
      </Card>
      {editing && <ConsignmentEditor shipment={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setOpen} />}
      {open && <ConsignmentDrawer id={open} onClose={() => setOpen(null)} onEdit={s => { setOpen(null); setEditing(s) }} />}
    </>
  )
}

function ConsignmentEditor({ shipment, onClose, onSaved }: { shipment?: Shipment; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: routes = [] } = useRoutes()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [quote, setQuote] = useState<Quote | null>(null)
  const routeId = Form.useWatch('routeId', form)
  const service = Form.useWatch('service', form)
  const weightKg = Form.useWatch('weightKg', form)
  const otherCharges = Form.useWatch('otherCharges', form)
  const freightOverride = Form.useWatch('freightOverride', form)
  const paymentMode = Form.useWatch('paymentMode', form)

  useEffect(() => {
    if (!weightKg) { setQuote(null); return }
    const t = setTimeout(() => api.post<Quote>('/logistics/quote', { routeId, service, weightKg, otherCharges: otherCharges ?? 0 })
      .then(r => setQuote(r.data)).catch(() => setQuote(null)), 300)
    return () => clearTimeout(t)
  }, [routeId, service, weightKg, otherCharges])

  const pickRoute = (id?: string) => {
    const r = routes.find(x => x.id === id)
    if (r) form.setFieldsValue({ originCity: r.origin, destinationCity: r.destination })
  }

  const save = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const body = { ...v, bookingDate: d8(v.bookingDate), promisedDate: d8(v.promisedDate), otherCharges: v.otherCharges ?? 0, codAmount: v.codAmount ?? 0 }
      const res = shipment ? await api.put<Shipment>(`/logistics/shipments/${shipment.id}`, body) : await api.post<Shipment>('/logistics/shipments', body)
      message.success(shipment ? 'Consignment saved' : `CN ${res.data.number} booked${res.data.invoiceNumber ? ` — invoice ${res.data.invoiceNumber}` : ''}`)
      await refreshAll(qc)
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Drawer open onClose={onClose} size={900} title={shipment ? `Edit ${shipment.number}` : 'Book consignment'} extra={<Button type="primary" loading={busy} onClick={save}>Save</Button>}>
      <Form form={form} layout="vertical" initialValues={shipment
        ? { ...shipment, bookingDate: dj(shipment.bookingDate), promisedDate: dj(shipment.promisedDate), freightOverride: undefined }
        : { entityId: me?.entities.find(e => e.permissions.includes('logistics.shipments.create'))?.id, bookingDate: dayjs(), service: 'PartLoad', paymentMode: 'Account', pieces: 1, declaredValue: 0, otherCharges: 0, codAmount: 0 }}>
        <Row gutter={12}>
          <Col xs={24} md={10}><Form.Item name="customerId" label="Bill-to customer" rules={[{ required: true }]}><ContactSelect customers /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="bookingDate" label="Booking date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={9}><Form.Item name="entityId" label="Booking branch" rules={[{ required: true }]}><EntityPicker permission="logistics.shipments.create" /></Form.Item></Col>
        </Row>
        <Row gutter={12}>
          <Col xs={24} md={12}>
            <Card size="small" title="Shipper">
              <Form.Item name="shipperName" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
              <Form.Item name="shipperPhone" label="Phone"><Input /></Form.Item>
              <Form.Item name="shipperAddress" label="Pickup address"><Input.TextArea rows={2} /></Form.Item>
            </Card>
          </Col>
          <Col xs={24} md={12}>
            <Card size="small" title="Consignee">
              <Form.Item name="consigneeName" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
              <Form.Item name="consigneePhone" label="Phone"><Input /></Form.Item>
              <Form.Item name="consigneeAddress" label="Delivery address"><Input.TextArea rows={2} /></Form.Item>
            </Card>
          </Col>
        </Row>
        <Row gutter={12} style={{ marginTop: 12 }}>
          <Col xs={24} md={8}><Form.Item name="routeId" label="Route (rate card)"><Select allowClear showSearch={{ optionFilterProp: "label" }} onChange={pickRoute} placeholder="No rate card — enter freight"
            options={routes.filter(r => r.isActive).map(r => ({ value: r.id, label: `${r.code} · ${r.origin} → ${r.destination}` }))} /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="originCity" label="Origin" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={12} md={8}><Form.Item name="destinationCity" label="Destination" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={8}><Form.Item name="service" label="Service"><Segmented block options={['PartLoad', 'FullTruck', 'Express'].map(s => ({ value: s, label: words(s) }))} /></Form.Item></Col>
          <Col xs={8} md={4}><Form.Item name="pieces" label="Pieces" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={8} md={4}><Form.Item name="weightKg" label="Weight (kg)" rules={[{ required: true }]}><InputNumber min={0.1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={8} md={4}><Form.Item name="volumeCbm" label="Volume (cbm)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={4}><Form.Item name="declaredValue" label="Declared value"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24}><Form.Item name="goodsDescription" label="Goods description"><Input /></Form.Item></Col>
        </Row>
        <Row gutter={12}>
          <Col xs={24} md={8}><Form.Item name="paymentMode" label="Freight payment" extra={paymentMode === 'Account' ? 'Billed monthly' : paymentMode === 'ToPay' ? 'Collected from consignee on delivery' : 'Paid now at the counter'}>
            <Segmented block options={['Prepaid', 'ToPay', 'Account'].map(s => ({ value: s, label: words(s) }))} /></Form.Item></Col>
          {paymentMode === 'Prepaid' && !shipment?.invoiceId && <Col xs={24} md={8}><Form.Item name="paidIntoAccountId" label="Paid into" rules={[{ required: true }]}><AccountSelect subTypes={['Cash', 'Bank']} /></Form.Item></Col>}
          <Col xs={12} md={4}><Form.Item name="codAmount" label="COD to collect" tooltip="Goods value collected from the consignee for the shipper"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="promisedDate" label="Promised by" extra="Blank = route time"><DatePicker style={{ width: '100%' }} format="DD MMM" /></Form.Item></Col>
        </Row>
        <Row gutter={12}>
          <Col xs={12} md={6}><Form.Item name="otherCharges" label="Other charges" tooltip="Loading, packing, insurance"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} md={6}><Form.Item name="freightOverride" label="Freight override" tooltip="Negotiated freight; replaces the rate card"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24} md={12}>
            {freightOverride ? <Alert type="info" showIcon title={`Negotiated freight ${amount(freightOverride, 0)} + fuel surcharge, other charges and tax per the route.`} />
              : quote ? <Descriptions size="small" column={2} bordered items={[
                { label: 'Freight', children: <>{amount(quote.freight, 0)} <Typography.Text type="secondary">({quote.basis})</Typography.Text></> },
                { label: 'Fuel', children: amount(quote.fuelSurcharge, 0) }, { label: 'Tax', children: amount(quote.taxAmount, 0) },
                { label: 'Total', children: <b>{amount(quote.total, 0)}</b> }]} />
              : <Typography.Text type="secondary">{!routeId ? 'Pick a route to price from its rate card, or enter a freight override.' : 'Enter the weight to see the freight quote.'}</Typography.Text>}
          </Col>
        </Row>
      </Form>
    </Drawer>
  )
}

function ConsignmentDrawer({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit?: (s: Shipment) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [deliver, setDeliver] = useState(false)
  const [statusOpen, setStatusOpen] = useState(false)
  const { data: s, isLoading } = useQuery({ queryKey: ['log-shipment', id], queryFn: async () => (await api.get<Shipment>(`/logistics/shipments/${id}`)).data })
  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); message.success(ok); await refreshAll(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const closed = s && ['Delivered', 'Returned', 'Cancelled'].includes(s.status)
  const edit = can('logistics.shipments.edit')
  return (
    <Drawer open onClose={onClose} size={760} loading={isLoading} title={s && <Space>{s.number}<StatusTag s={s.status} /></Space>}
      extra={s && <Space wrap>
        <Button icon={<PrinterOutlined />} onClick={() => printCn(s)}>Print CN</Button>
        {edit && s.status === 'Booked' && !s.tripNumber && onEdit && <Button icon={<EditOutlined />} onClick={() => onEdit(s)}>Edit</Button>}
        {edit && !closed && s.status !== 'Booked' && <Button onClick={() => setStatusOpen(true)}>Update status</Button>}
        {edit && !closed && s.status !== 'Booked' && <Button type="primary" icon={<CheckOutlined />} onClick={() => setDeliver(true)}>Deliver</Button>}
        {edit && s.status === 'Booked' && !s.tripNumber && !s.invoiceId &&
          <Popconfirm title="Cancel this consignment?" onConfirm={() => act(() => api.post(`/logistics/shipments/${s.id}/cancel`), 'Cancelled')}><Button danger icon={<StopOutlined />}>Cancel</Button></Popconfirm>}
      </Space>}>
      {s && <>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
          { label: 'Customer', children: s.customerName }, { label: 'Booked', children: `${fmtDate(s.bookingDate)} · ${s.entityName}` },
          { label: 'Shipper', children: <>{s.shipperName}<div><Typography.Text type="secondary">{[s.shipperPhone, s.shipperAddress].filter(Boolean).join(' · ')}</Typography.Text></div></> },
          { label: 'Consignee', children: <>{s.consigneeName}<div><Typography.Text type="secondary">{[s.consigneePhone, s.consigneeAddress].filter(Boolean).join(' · ')}</Typography.Text></div></> },
          { label: 'Lane', children: `${s.originCity} → ${s.destinationCity}${s.routeCode ? ` (${s.routeCode})` : ''}` }, { label: 'Service', children: words(s.service) },
          { label: 'Goods', children: `${s.pieces} pcs · ${kg(s.weightKg)}${s.goodsDescription ? ` · ${s.goodsDescription}` : ''}` }, { label: 'Declared value', children: amount(s.declaredValue, 0) },
          { label: 'Freight', children: <>{amount(s.total, 0)} <Tag color={MODE_COLORS[s.paymentMode]}>{words(s.paymentMode)}</Tag>
            <div><Typography.Text type="secondary">Freight {amount(s.freight, 0)} + fuel {amount(s.fuelSurcharge, 0)} + other {amount(s.otherCharges, 0)} + tax {amount(s.taxAmount, 0)}</Typography.Text></div></> },
          { label: 'Invoice', children: s.invoiceNumber ?? (s.paymentMode === 'Account' ? 'On next monthly bill' : '—') },
          { label: 'COD', children: s.codAmount ? <>{amount(s.codAmount, 0)} {s.codRemitted ? <Tag color="green">Remitted</Tag> : s.codCollected ? <Tag color="orange">Collected — to remit</Tag> : <Tag>To collect</Tag>}</> : '—' },
          { label: 'Promised', children: s.promisedDate ? fmtDate(s.promisedDate) : '—' },
          { label: 'Trip', children: s.tripNumber ?? 'Not loaded', span: s.deliveredAt ? 1 : 'filled' as const },
          ...(s.deliveredAt ? [{ label: 'Delivered', span: 'filled' as const, children: `${dayjs(s.deliveredAt).format('DD MMM YYYY HH:mm')} — ${s.receivedBy}${s.deliveryRemarks ? ` (${s.deliveryRemarks})` : ''}` }] : []),
        ]} />
        <Typography.Title level={5} style={{ marginTop: 24 }}>Tracking</Typography.Title>
        <Timeline items={s.events.map(e => ({ color: SHIPMENT_COLORS[e.status], content: <>
          <b>{words(e.status)}</b>{e.location && ` — ${e.location}`}
          <div><Typography.Text type="secondary">{dayjs(e.at).format('DD MMM YYYY HH:mm')}{e.byName && ` · ${e.byName}`}</Typography.Text></div>
          {e.remarks && <div>{e.remarks}</div>}</> }))} />
      </>}
      {deliver && s && <DeliverModal s={s} onClose={() => setDeliver(false)} />}
      {statusOpen && s && <StatusModal s={s} onClose={() => setStatusOpen(false)} />}
    </Drawer>
  )
}

function DeliverModal({ s, onClose }: { s: Shipment; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const toPay = s.paymentMode === 'ToPay' && !s.invoiceId
  const collect = (toPay ? s.total : 0) + s.codAmount
  const ok = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      await api.post(`/logistics/shipments/${s.id}/deliver`, { ...v, codCollected: s.codAmount || undefined })
      message.success(`${s.number} delivered`)
      await refreshAll(qc)
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Modal open title={`Proof of delivery — ${s.number}`} onCancel={onClose} onOk={ok} okText="Mark delivered" confirmLoading={busy}>
      {collect > 0 && <Alert style={{ marginBottom: 16 }} type="warning" showIcon title={`Collect ${amount(collect, 0)} from the consignee`}
        description={[toPay && `To-pay freight ${amount(s.total, 0)}`, s.codAmount && `COD ${amount(s.codAmount, 0)} (held for ${s.customerName})`].filter(Boolean).join(' · ')} />}
      <Form form={form} layout="vertical">
        <Form.Item name="receivedBy" label="Received by" rules={[{ required: true }]}><Input placeholder="Name of the person who signed" /></Form.Item>
        {collect > 0 && <Form.Item name="collectedIntoAccountId" label="Cash collected into" rules={[{ required: true }]}><AccountSelect subTypes={['Cash', 'Bank']} /></Form.Item>}
        <Form.Item name="remarks" label="Remarks"><Input placeholder="e.g. 1 carton damaged" /></Form.Item>
      </Form>
    </Modal>
  )
}

function StatusModal({ s, onClose }: { s: Shipment; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try { await api.post(`/logistics/shipments/${s.id}/status`, v); message.success('Tracking updated'); await refreshAll(qc); onClose() } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={`Update tracking — ${s.number}`} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={{ status: 'OutForDelivery', location: s.destinationCity }}>
        <Form.Item name="status" label="Status"><Select options={(['PickedUp', 'AtHub', 'OutForDelivery', 'Returned'] as ShipmentStatus[]).map(x => ({ value: x, label: words(x) }))} /></Form.Item>
        <Form.Item name="location" label="Location"><Input /></Form.Item>
        <Form.Item name="remarks" label="Remarks"><Input /></Form.Item>
      </Form>
    </Modal>
  )
}

function printCn(s: Shipment) {
  const w = window.open('', '_blank', 'width=800,height=900')
  if (!w) return
  const esc = (x?: string | number) => String(x ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)
  const row = (a: string, b?: string | number) => `<tr><th>${esc(a)}</th><td>${esc(b)}</td></tr>`
  w.document.write(`<html><head><title>${esc(s.number)}</title><style>body{font-family:sans-serif;padding:24px}h1{margin:0}table{border-collapse:collapse;width:100%;margin-top:16px}
    th,td{border:1px solid #999;padding:6px 8px;text-align:left;vertical-align:top}th{width:30%;background:#f5f5f5}.sig{margin-top:48px;display:flex;justify-content:space-between}</style></head><body>
    <h1>Consignment note ${esc(s.number)}</h1><div>${esc(s.entityName)} · Booked ${esc(fmtDate(s.bookingDate))}</div><table>
    ${row('From', `${s.shipperName}, ${s.shipperPhone ?? ''} ${s.shipperAddress ?? ''} — ${s.originCity}`)}
    ${row('To', `${s.consigneeName}, ${s.consigneePhone ?? ''} ${s.consigneeAddress ?? ''} — ${s.destinationCity}`)}
    ${row('Goods', `${s.pieces} pcs, ${s.weightKg} kg ${s.goodsDescription ?? ''}`)}${row('Declared value', amount(s.declaredValue, 0))}
    ${row('Service', words(s.service))}${row('Freight', `${amount(s.total, 0)} (${words(s.paymentMode)})`)}
    ${s.codAmount ? row('COD — collect from consignee', amount(s.codAmount, 0)) : ''}${row('Promised by', s.promisedDate ? fmtDate(s.promisedDate) : '')}
    </table><div class="sig"><span>Shipper signature ____________</span><span>Received in good condition ____________</span></div>
    <script>window.print()</script></body></html>`)
  w.document.close()
}

// ======================= Trips =======================

export function TripsPage() {
  const { can } = useAuth()
  const [status, setStatus] = useState<TripStatus | 'All'>('All')
  const [open, setOpen] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['log-trips', status],
    queryFn: async () => (await api.get<TripListItem[]>('/logistics/trips', { params: { status: status === 'All' ? undefined : status } })).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Trips & load sheets</Typography.Title>
        {can('logistics.shipments.dispatch') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Plan trip</Button>}
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={status} onChange={v => setStatus(v as typeof status)} options={['All', 'Planned', 'Dispatched', 'Completed', 'Cancelled']} />
        <Table<TripListItem> rowKey="id" loading={isFetching} dataSource={data} scroll={{ x: 950 }} onRow={t => ({ onClick: () => setOpen(t.id), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Trip', dataIndex: 'number', render: (n: string) => <b>{n}</b> },
            { title: 'Date', dataIndex: 'plannedDate', render: fmtDate },
            { title: 'Lane', render: (_, t) => `${t.origin} → ${t.destination}` },
            { title: 'Vehicle', dataIndex: 'vehicleNo' },
            { title: 'Driver', dataIndex: 'driverName' },
            { title: 'CNs', dataIndex: 'shipments' },
            { title: 'Load', width: 190, render: (_, t) => <LoadBar load={t.loadKg} cap={t.capacityKg} /> },
            { title: 'Freight', align: 'right', render: (_, t) => amount(t.freight, 0) },
            { title: 'Margin', align: 'right', render: (_, t) => <Typography.Text type={t.freight - t.expenses < 0 ? 'danger' : undefined}>{amount(t.freight - t.expenses, 0)}</Typography.Text> },
            { title: 'Status', dataIndex: 'status', render: (s: TripStatus) => <Tag color={TRIP_COLORS[s]}>{s}</Tag> },
          ]} />
      </Card>
      {creating && <TripEditor onClose={() => setCreating(false)} onSaved={setOpen} />}
      {open && <TripDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function TripEditor({ trip, onClose, onSaved }: { trip?: Trip; onClose: () => void; onSaved?: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: vehicles = [] } = useVehicles()
  const { data: drivers = [] } = useDrivers()
  const { data: routes = [] } = useRoutes()
  const [form] = Form.useForm()
  const routeId = Form.useWatch('routeId', form)
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, plannedDate: d8(v.plannedDate) }
      const res = trip ? await api.put<Trip>(`/logistics/trips/${trip.id}`, body) : await api.post<Trip>('/logistics/trips', body)
      message.success(trip ? 'Trip saved' : `Trip ${res.data.number} planned — now load consignments`)
      await refreshAll(qc)
      onClose()
      onSaved?.(res.data.id)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={trip ? `Edit ${trip.number}` : 'Plan trip'} onCancel={onClose} onOk={ok} width={640}>
      <Form form={form} layout="vertical" initialValues={trip ? { ...trip, plannedDate: dj(trip.plannedDate) }
        : { entityId: me?.entities.find(e => e.permissions.includes('logistics.shipments.dispatch'))?.id, plannedDate: dayjs() }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="vehicleId" label="Vehicle" rules={[{ required: true }]}><Select showSearch={{ optionFilterProp: 'label' }}
            options={vehicles.filter(v => v.status !== 'Inactive').map(v => ({ value: v.id, label: `${v.registrationNo} · ${words(v.type)} · ${v.capacityKg.toLocaleString()} kg${v.status !== 'Available' ? ` (${words(v.status)})` : ''}${v.documentAlerts.length ? ' ⚠' : ''}`, disabled: v.status === 'Maintenance' }))} /></Form.Item></Col>
          <Col span={12}><Form.Item name="driverId" label="Driver" rules={[{ required: true }]}><Select showSearch={{ optionFilterProp: 'label' }}
            options={drivers.filter(d => d.isActive).map(d => ({ value: d.id, label: `${d.fullName}${d.onTrip ? ' (on trip)' : ''}${d.licenseAlert ? ' ⚠' : ''}` }))} /></Form.Item></Col>
          <Col span={12}><Form.Item name="routeId" label="Route"><Select allowClear options={routes.map(r => ({ value: r.id, label: `${r.code} · ${r.origin} → ${r.destination}` }))} /></Form.Item></Col>
          <Col span={12}><Form.Item name="plannedDate" label="Planned date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          {!routeId && <>
            <Col span={12}><Form.Item name="origin" label="From" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="destination" label="To" rules={[{ required: true }]}><Input /></Form.Item></Col>
          </>}
          <Col span={24}><Form.Item name="entityId" label="Branch" rules={[{ required: true }]}><EntityPicker permission="logistics.shipments.dispatch" /></Form.Item></Col>
          <Col span={24}><Form.Item name="notes" label="Notes"><Input /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

function TripDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message, modal } = App.useApp()
  const [loading, setLoading] = useState(false)
  const [expense, setExpense] = useState(false)
  const [editing, setEditing] = useState(false)
  const [cn, setCn] = useState<string | null>(null)
  const { data: t, isLoading } = useQuery({ queryKey: ['log-trip', id], queryFn: async () => (await api.get<Trip>(`/logistics/trips/${id}`)).data })
  const act = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); message.success(ok); await refreshAll(qc) } catch (e) { message.error(errorMessage(e)) }
  }
  const odometer = (title: string, path: string, field: string, ok: string) => {
    let value: number | null = null
    modal.confirm({ title, icon: null, content: <InputNumber style={{ width: '100%', marginTop: 8 }} placeholder="Odometer reading (optional)" min={0} onChange={v => { value = v as number | null }} />,
      onOk: () => act(() => api.post(`/logistics/trips/${id}/${path}`, { [field]: value ?? undefined }), ok) })
  }
  const dispatch = can('logistics.shipments.dispatch')
  return (
    <Drawer open onClose={onClose} size={900} loading={isLoading} title={t && <Space>{t.number}<Tag color={TRIP_COLORS[t.status]}>{t.status}</Tag></Space>}
      extra={t && dispatch && <Space wrap>
        <Button icon={<PrinterOutlined />} onClick={() => printManifest(t)}>Load sheet</Button>
        {t.status === 'Planned' && <>
          <Button icon={<EditOutlined />} onClick={() => setEditing(true)}>Edit</Button>
          <Button icon={<PlusOutlined />} onClick={() => setLoading(true)}>Load consignments</Button>
          <Button type="primary" icon={<SendOutlined />} disabled={!t.shipments.length} onClick={() => odometer(`Dispatch ${t.vehicleNo}?`, 'dispatch', 'odometerStart', 'Dispatched')}>Dispatch</Button>
          <Popconfirm title="Cancel this trip? Consignments return to the booking pool." onConfirm={() => act(() => api.post(`/logistics/trips/${id}/cancel`), 'Trip cancelled')}>
            <Button danger icon={<StopOutlined />}>Cancel</Button></Popconfirm>
        </>}
        {t.status === 'Dispatched' && <Button type="primary" icon={<FlagOutlined />} onClick={() => odometer(`${t.vehicleNo} arrived at ${t.destination}?`, 'arrive', 'odometerEnd', 'Arrived — consignments at hub')}>Mark arrived</Button>}
        {t.status !== 'Cancelled' && <Button onClick={() => setExpense(true)}>Add expense</Button>}
      </Space>}>
      {t && <>
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          <Col xs={12} md={6}><Statistic title="Freight" value={amount(t.freight, 0)} /></Col>
          <Col xs={12} md={6}><Statistic title="Trip expenses" value={amount(t.expensesTotal, 0)} /></Col>
          <Col xs={12} md={6}><Statistic title="Margin" value={amount(t.margin, 0)} styles={{ content: { color: t.margin < 0 ? '#cf1322' : '#389e0d' } }} /></Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Load</Typography.Text><LoadBar load={t.loadKg} cap={t.capacityKg} /></Col>
        </Row>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
          { label: 'Lane', children: `${t.origin} → ${t.destination}${t.routeCode ? ` (${t.routeCode})` : ''}` }, { label: 'Planned', children: fmtDate(t.plannedDate) },
          { label: 'Vehicle', children: `${t.vehicleNo} · ${words(t.vehicleType)}` }, { label: 'Driver', children: `${t.driverName}${t.driverPhone ? ` · ${t.driverPhone}` : ''}` },
          { label: 'Dispatched', children: t.dispatchedAt ? `${dayjs(t.dispatchedAt).format('DD MMM HH:mm')}${t.odometerStart ? ` · ${t.odometerStart.toLocaleString()} km` : ''}` : '—' },
          { label: 'Arrived', children: t.arrivedAt ? `${dayjs(t.arrivedAt).format('DD MMM HH:mm')}${t.odometerEnd ? ` · ${t.odometerEnd.toLocaleString()} km` : ''}` : '—' },
          ...(t.odometerStart && t.odometerEnd ? [{ label: 'Distance', span: 'filled' as const, children: `${(t.odometerEnd - t.odometerStart).toLocaleString()} km · cost/km ${amount(t.expensesTotal / Math.max(1, t.odometerEnd - t.odometerStart), 1)}` }] : []),
        ]} />
        <Typography.Title level={5} style={{ marginTop: 24 }}>Manifest ({t.shipments.length})</Typography.Title>
        <Table size="small" rowKey="id" pagination={false} dataSource={t.shipments} locale={{ emptyText: 'Nothing loaded yet' }} scroll={{ x: 700 }}
          onRow={s => ({ onClick: () => setCn(s.id), style: { cursor: 'pointer' } })}
          columns={[{ title: 'CN', dataIndex: 'number' }, { title: 'Customer', dataIndex: 'customerName' }, { title: 'To', render: (_, s) => `${s.consigneeName}, ${s.destinationCity}` },
            { title: 'Weight', render: (_, s) => kg(s.weightKg) }, { title: 'COD', align: 'right', render: (_, s) => s.codAmount ? amount(s.codAmount, 0) : '' },
            { title: 'Status', dataIndex: 'status', render: (s: ShipmentStatus) => <StatusTag s={s} /> },
            ...(t.status === 'Planned' && dispatch ? [{ key: 'x', render: (_: unknown, s: ShipmentListItem) => <Button size="small" danger icon={<DeleteOutlined />}
              onClick={e => { e.stopPropagation(); act(() => api.delete(`/logistics/trips/${id}/shipments/${s.id}`), `${s.number} unloaded`) }} /> }] : [])]} />
        <Typography.Title level={5} style={{ marginTop: 24 }}>Expenses</Typography.Title>
        <Table size="small" rowKey="id" pagination={false} dataSource={t.expenses} locale={{ emptyText: 'No expenses recorded' }}
          columns={[{ title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'Type', dataIndex: 'type', render: words }, { title: 'Description', dataIndex: 'description' },
            { title: 'Paid', render: (_, x) => x.paidFrom ?? (x.vendorName ? <>{x.vendorName} <Tag>Bill drafted</Tag></> : '—') },
            { title: 'Amount', align: 'right', render: (_, x) => amount(x.amount, 0) }]} />
      </>}
      {loading && t && <LoadModal trip={t} onClose={() => setLoading(false)} />}
      {expense && <ExpenseModal tripId={id} onClose={() => setExpense(false)} />}
      {editing && t && <TripEditor trip={t} onClose={() => setEditing(false)} />}
      {cn && <ConsignmentDrawer id={cn} onClose={() => setCn(null)} />}
    </Drawer>
  )
}

function LoadModal({ trip, onClose }: { trip: Trip; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [selected, setSelected] = useState<string[]>([])
  const [sameLane, setSameLane] = useState(true)
  const { data, isFetching } = useQuery({ queryKey: ['log-shipments', 'loadable'],
    queryFn: async () => (await api.get<PagedResult<ShipmentListItem>>('/logistics/shipments', { params: { openOnly: true, pageSize: 200 } })).data })
  const pool = (data?.items ?? []).filter(s => ['Booked', 'PickedUp', 'AtHub'].includes(s.status) && !s.tripNumber
    && (!sameLane || s.destinationCity.toLowerCase() === trip.destination.toLowerCase()))
  const adding = pool.filter(s => selected.includes(s.id)).reduce((a, s) => a + s.weightKg, 0)
  const ok = async () => {
    try { await api.post(`/logistics/trips/${trip.id}/load`, { shipmentIds: selected }); message.success(`${selected.length} consignment(s) loaded`); await refreshAll(qc); onClose() }
    catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open width={860} title={`Load ${trip.vehicleNo} — ${trip.origin} → ${trip.destination}`} onCancel={onClose} onOk={ok} okButtonProps={{ disabled: !selected.length }} okText="Load">
      <Space style={{ marginBottom: 12 }}><Switch checked={sameLane} onChange={setSameLane} /> Only consignments for {trip.destination}</Space>
      <LoadBar load={trip.loadKg + adding} cap={trip.capacityKg} />
      <Table size="small" rowKey="id" loading={isFetching} dataSource={pool} pagination={false} scroll={{ y: 360 }} locale={{ emptyText: <Empty description="No consignments waiting" /> }}
        rowSelection={{ selectedRowKeys: selected, onChange: k => setSelected(k as string[]) }}
        columns={[{ title: 'CN', dataIndex: 'number' }, { title: 'Customer', dataIndex: 'customerName' }, { title: 'To', render: (_, s) => `${s.consigneeName}, ${s.destinationCity}` },
          { title: 'Weight', render: (_, s) => kg(s.weightKg) }, { title: 'Booked', dataIndex: 'bookingDate', render: fmtDate }]} />
    </Modal>
  )
}

function ExpenseModal({ tripId, onClose }: { tripId: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [onCredit, setOnCredit] = useState(false)
  const ok = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/logistics/trips/${tripId}/expenses`, { ...v, date: d8(v.date), paidFromAccountId: onCredit ? undefined : v.paidFromAccountId, vendorId: onCredit ? v.vendorId : undefined })
      message.success(onCredit ? 'Expense recorded — draft bill created' : 'Expense posted')
      await refreshAll(qc)
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title="Trip expense" onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={{ type: 'Fuel', date: dayjs() }}>
        <Row gutter={12}>
          <Col span={12}><Form.Item name="type" label="Type"><Select options={EXPENSE_TYPES.map(t => ({ value: t, label: words(t) }))} /></Form.Item></Col>
          <Col span={12}><Form.Item name="amount" label="Amount" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={12}><Form.Item name="date" label="Date"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={12}><Form.Item name="description" label="Description"><Input /></Form.Item></Col>
        </Row>
        <Checkbox checked={onCredit} onChange={e => setOnCredit(e.target.checked)} style={{ marginBottom: 12 }}>On credit (fuel station account, vehicle owner)</Checkbox>
        {onCredit ? <Form.Item name="vendorId" label="Vendor" rules={[{ required: true }]}><ContactSelect vendors /></Form.Item>
          : <Form.Item name="paidFromAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Cash', 'Bank']} /></Form.Item>}
      </Form>
    </Modal>
  )
}

function printManifest(t: Trip) {
  const w = window.open('', '_blank', 'width=900,height=900')
  if (!w) return
  const esc = (x?: string | number) => String(x ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!)
  w.document.write(`<html><head><title>${esc(t.number)}</title><style>body{font-family:sans-serif;padding:24px}table{border-collapse:collapse;width:100%;margin-top:16px}
    th,td{border:1px solid #999;padding:5px 8px;text-align:left}th{background:#f5f5f5}</style></head><body>
    <h2>Load sheet ${esc(t.number)}</h2><div>${esc(t.origin)} → ${esc(t.destination)} · ${esc(fmtDate(t.plannedDate))}</div>
    <div>Vehicle ${esc(t.vehicleNo)} · Driver ${esc(t.driverName)} ${esc(t.driverPhone)} · Load ${esc(kg(t.loadKg))} of ${esc(kg(t.capacityKg))}</div>
    <table><tr><th>#</th><th>CN</th><th>Consignee</th><th>City</th><th>Weight</th><th>COD</th><th>Signature</th></tr>
    ${t.shipments.map((s, i) => `<tr><td>${i + 1}</td><td>${esc(s.number)}</td><td>${esc(s.consigneeName)}</td><td>${esc(s.destinationCity)}</td><td>${esc(kg(s.weightKg))}</td><td>${s.codAmount ? esc(amount(s.codAmount, 0)) : ''}</td><td></td></tr>`).join('')}
    </table><script>window.print()</script></body></html>`)
  w.document.close()
}

// ======================= Fleet, drivers, routes =======================

export function FleetPage() {
  const { can } = useAuth()
  const tabs = [
    can('logistics.fleet.view') && { key: 'vehicles', label: 'Vehicles', children: <VehiclesTab /> },
    can('logistics.drivers.view') && { key: 'drivers', label: 'Drivers', children: <DriversTab /> },
    can('logistics.routes.view') && { key: 'routes', label: 'Routes & rates', children: <RoutesTab /> },
    can('logistics.fleet.view') && { key: 'maintenance', label: 'Maintenance', children: <MaintenanceTab /> },
  ].filter(Boolean) as { key: string; label: string; children: React.ReactNode }[]
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Fleet</Typography.Title></div>
      <Card>{tabs.length ? <Tabs items={tabs} /> : <Result status="403" title="No access" />}</Card>
    </>
  )
}

const ExpiryCell = ({ d }: { d?: string }) => {
  if (!d) return <Typography.Text type="secondary">—</Typography.Text>
  const days = dayjs(d).diff(dayjs().startOf('day'), 'day')
  return <Typography.Text type={days < 0 ? 'danger' : days <= 30 ? 'warning' : undefined}>{fmtDate(d)}</Typography.Text>
}

function VehiclesTab() {
  const { can } = useAuth()
  const { data, isFetching } = useVehicles()
  const [editing, setEditing] = useState<Vehicle | 'new' | null>(null)
  return (
    <>
      {can('logistics.fleet.create') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => setEditing('new')}>Add vehicle</Button>}
      <Table<Vehicle> rowKey="id" loading={isFetching} dataSource={data} scroll={{ x: 1100 }} pagination={false}
        onRow={v => ({ onClick: () => can('logistics.fleet.edit') && setEditing(v), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Registration', render: (_, v) => <><b>{v.registrationNo}</b>{v.isHired && <Tag style={{ marginLeft: 6 }}>Hired</Tag>}<div><Typography.Text type="secondary">{v.makeModel}</Typography.Text></div></> },
          { title: 'Type', dataIndex: 'type', render: words },
          { title: 'Capacity', render: (_, v) => kg(v.capacityKg) },
          { title: 'Fitness', render: (_, v) => <ExpiryCell d={v.fitnessExpiry} /> },
          { title: 'Insurance', render: (_, v) => <ExpiryCell d={v.insuranceExpiry} /> },
          { title: 'Route permit', render: (_, v) => <ExpiryCell d={v.routePermitExpiry} /> },
          { title: 'Token tax', render: (_, v) => <ExpiryCell d={v.tokenTaxExpiry} /> },
          { title: 'Odometer', render: (_, v) => `${v.odometer.toLocaleString()} km` },
          { title: 'Status', render: (_, v) => <><Tag color={VEHICLE_COLORS[v.status]}>{words(v.status)}</Tag>{v.currentTrip && <Typography.Text type="secondary">{v.currentTrip}</Typography.Text>}</> },
          { title: '', render: (_, v) => v.documentAlerts.length > 0 && <Tag color="orange" icon={<WarningOutlined />} title={v.documentAlerts.join('\n')}>{v.documentAlerts.length}</Tag> },
        ]} />
      {editing && <VehicleModal v={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function VehicleModal({ v, onClose }: { v?: Vehicle; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const [form] = Form.useForm()
  const hired = Form.useWatch('isHired', form)
  const dates = ['fitnessExpiry', 'insuranceExpiry', 'routePermitExpiry', 'tokenTaxExpiry'] as const
  const ok = async () => {
    const x = await form.validateFields()
    try {
      const body = { ...x, ...Object.fromEntries(dates.map(k => [k, d8(x[k])])) }
      if (v) await api.put(`/logistics/vehicles/${v.id}`, body); else await api.post('/logistics/vehicles', body)
      message.success('Vehicle saved'); await qc.invalidateQueries({ queryKey: ['log-vehicles'] }); onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open width={720} title={v ? v.registrationNo : 'Add vehicle'} onCancel={onClose} onOk={ok}>
      <Form form={form} layout="vertical" initialValues={v ? { ...v, ...Object.fromEntries(dates.map(k => [k, dj(v[k])])) }
        : { entityId: me?.entities.find(e => e.permissions.includes('logistics.fleet.create'))?.id, type: 'Truck', status: 'Available', odometer: 0, isHired: false }}>
        <Row gutter={12}>
          <Col span={8}><Form.Item name="registrationNo" label="Registration no." rules={[{ required: true }]}><Input placeholder="LES-1234" /></Form.Item></Col>
          <Col span={8}><Form.Item name="type" label="Type"><Select options={VEHICLE_TYPES.map(t => ({ value: t, label: words(t) }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="makeModel" label="Make / model"><Input placeholder="Hino 500" /></Form.Item></Col>
          <Col span={8}><Form.Item name="capacityKg" label="Capacity (kg)" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="capacityCbm" label="Capacity (cbm)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="odometer" label="Odometer (km)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          {dates.map(k => <Col span={6} key={k}><Form.Item name={k} label={{ fitnessExpiry: 'Fitness', insuranceExpiry: 'Insurance', routePermitExpiry: 'Route permit', tokenTaxExpiry: 'Token tax' }[k] + ' expiry'}>
            <DatePicker style={{ width: '100%' }} format="DD MMM YY" /></Form.Item></Col>)}
          <Col span={8}><Form.Item name="isHired" label="Hired vehicle" valuePropName="checked"><Switch /></Form.Item></Col>
          {hired && <Col span={16}><Form.Item name="ownerVendorId" label="Owner / broker" rules={[{ required: true }]}><ContactSelect vendors /></Form.Item></Col>}
          <Col span={12}><Form.Item name="status" label="Status"><Select options={(['Available', 'Maintenance', 'Inactive'] as const).map(s => ({ value: s, label: s }))} disabled={v?.status === 'OnTrip'} /></Form.Item></Col>
          <Col span={12}><Form.Item name="entityId" label="Branch" rules={[{ required: true }]}><EntityPicker permission="logistics.fleet.create" /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

function DriversTab() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data, isFetching } = useDrivers()
  const [editing, setEditing] = useState<Driver | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (d: Driver | 'new') => { setEditing(d); form.resetFields(); form.setFieldsValue(d === 'new' ? { entityId: me?.entities.find(e => e.permissions.includes('logistics.drivers.create'))?.id, isActive: true, dailyAllowance: 0 } : { ...d, licenseExpiry: dj(d.licenseExpiry) }) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, licenseExpiry: d8(v.licenseExpiry) }
      if (editing !== 'new' && editing) await api.put(`/logistics/drivers/${editing.id}`, body); else await api.post('/logistics/drivers', body)
      message.success('Driver saved'); await qc.invalidateQueries({ queryKey: ['log-drivers'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      {can('logistics.drivers.create') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add driver</Button>}
      <Table<Driver> rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 800 }}
        onRow={d => ({ onClick: () => can('logistics.drivers.edit') && open(d), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Name', render: (_, d) => <><b>{d.fullName}</b>{!d.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}<div><Typography.Text type="secondary">{d.phone}</Typography.Text></div></> },
          { title: 'CNIC', dataIndex: 'cnic' },
          { title: 'Licence', render: (_, d) => `${d.licenseNo}${d.licenseCategory ? ` (${d.licenseCategory})` : ''}` },
          { title: 'Expiry', render: (_, d) => <ExpiryCell d={d.licenseExpiry} /> },
          { title: 'Daily allowance', align: 'right', render: (_, d) => amount(d.dailyAllowance, 0) },
          { title: '', render: (_, d) => <>{d.onTrip && <Tag color="blue" icon={<CarOutlined />}>On trip</Tag>}{d.licenseAlert && <Tag color="orange">{d.licenseAlert}</Tag>}</> },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add driver' : 'Edit driver'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={12}><Form.Item name="fullName" label="Full name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="cnic" label="CNIC"><Input placeholder="35201-1234567-1" /></Form.Item></Col>
            <Col span={12}><Form.Item name="licenseNo" label="Licence no." rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="licenseCategory" label="Category"><Select allowClear options={['LTV', 'HTV', 'PSV', 'Motorcycle'].map(x => ({ value: x, label: x }))} /></Form.Item></Col>
            <Col span={8}><Form.Item name="licenseExpiry" label="Licence expiry"><DatePicker style={{ width: '100%' }} format="DD MMM YY" /></Form.Item></Col>
            <Col span={8}><Form.Item name="dailyAllowance" label="Daily allowance"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={16}><Form.Item name="entityId" label="Branch" rules={[{ required: true }]}><EntityPicker permission="logistics.drivers.create" /></Form.Item></Col>
            <Col span={8}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

function RoutesTab() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data, isFetching } = useRoutes()
  const [editing, setEditing] = useState<FreightRoute | 'new' | null>(null)
  const [form] = Form.useForm()
  const open = (r: FreightRoute | 'new') => { setEditing(r); form.resetFields(); form.setFieldsValue(r === 'new' ? { isActive: true, fuelSurchargePercent: 0, minimumCharge: 0, fullTruckRate: 0 } : r) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      if (editing !== 'new' && editing) await api.put(`/logistics/routes/${editing.id}`, v); else await api.post('/logistics/routes', v)
      message.success('Route saved'); await qc.invalidateQueries({ queryKey: ['log-routes'] }); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      {can('logistics.routes.create') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add route</Button>}
      <Table<FreightRoute> rowKey="id" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 800 }}
        onRow={r => ({ onClick: () => can('logistics.routes.edit') && open(r), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Route', render: (_, r) => <><b>{r.code}</b>{!r.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}<div>{r.origin} → {r.destination}</div></> },
          { title: 'Distance', render: (_, r) => `${r.distanceKm.toLocaleString()} km · ${r.standardHours} h` },
          { title: 'Per kg', align: 'right', render: (_, r) => amount(r.ratePerKg, 2) },
          { title: 'Minimum', align: 'right', render: (_, r) => amount(r.minimumCharge, 0) },
          { title: 'Full truck', align: 'right', render: (_, r) => r.fullTruckRate ? amount(r.fullTruckRate, 0) : '—' },
          { title: 'Fuel surcharge', align: 'right', render: (_, r) => `${r.fuelSurchargePercent}%` },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add route' : 'Edit route'} onCancel={() => setEditing(null)} onOk={ok} forceRender width={640}>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input placeholder="LHE-KHI" /></Form.Item></Col>
            <Col span={8}><Form.Item name="origin" label="Origin" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="destination" label="Destination" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="distanceKm" label="Distance (km)" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="standardHours" label="Transit hours" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="ratePerKg" label="Rate per kg" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="minimumCharge" label="Minimum charge"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="fullTruckRate" label="Full-truck rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="fuelSurchargePercent" label="Fuel surcharge %"><InputNumber min={0} max={100} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={16}><Form.Item name="taxRateId" label="Sales tax on services"><TaxRateSelect /></Form.Item></Col>
            <Col span={8}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

function MaintenanceTab() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: vehicles = [] } = useVehicles()
  const { data, isFetching } = useQuery({ queryKey: ['log-maint'], queryFn: async () => (await api.get<Maintenance[]>('/logistics/maintenance')).data })
  const [open, setOpen] = useState(false)
  const [form] = Form.useForm()
  const ok = async () => {
    const v = await form.validateFields()
    try {
      await api.post('/logistics/maintenance', { ...v, date: d8(v.date), nextServiceDate: d8(v.nextServiceDate) })
      message.success(v.vendorId ? 'Logged — draft bill created in Finance' : 'Maintenance logged')
      await qc.invalidateQueries({ queryKey: ['log-maint'] }); setOpen(false)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      {can('logistics.fleet.edit') && <Button type="primary" icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setOpen(true) }}>Log maintenance</Button>}
      <Table<Maintenance> rowKey="id" loading={isFetching} dataSource={data} pagination={{ pageSize: 25 }} scroll={{ x: 800 }}
        columns={[
          { title: 'Date', dataIndex: 'date', render: fmtDate }, { title: 'Vehicle', dataIndex: 'registrationNo' }, { title: 'Work done', dataIndex: 'description' },
          { title: 'Odometer', render: (_, m) => m.odometer ? `${m.odometer.toLocaleString()} km` : '—' }, { title: 'Workshop', render: (_, m) => m.vendorName ?? '—' },
          { title: 'Cost', align: 'right', render: (_, m) => amount(m.cost, 0) }, { title: 'Next service', render: (_, m) => <ExpiryCell d={m.nextServiceDate} /> },
        ]} />
      <Modal open={open} title="Log maintenance" onCancel={() => setOpen(false)} onOk={ok} forceRender>
        <Form form={form} layout="vertical" initialValues={{ date: dayjs() }}>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="vehicleId" label="Vehicle" rules={[{ required: true }]}><Select options={vehicles.map(v => ({ value: v.id, label: v.registrationNo }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
            <Col span={24}><Form.Item name="description" label="Work done" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="odometer" label="Odometer"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="cost" label="Cost" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="nextServiceDate" label="Next service"><DatePicker style={{ width: '100%' }} format="DD MMM YY" /></Form.Item></Col>
            <Col span={24}><Form.Item name="vendorId" label="Workshop (creates a draft bill)"><ContactSelect vendors /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

// ======================= COD & billing =======================

export function CodBillingPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [codForm] = Form.useForm()
  const [billForm] = Form.useForm()
  const [last, setLast] = useState<string | null>(null)
  const remit = async () => {
    const v = await codForm.validateFields()
    try {
      const r = (await api.post<{ shipments: number; amount: number }>('/logistics/cod/remit', { ...v, date: d8(v.date) })).data
      setLast(`Remitted ${amount(r.amount, 0)} COD for ${r.shipments} consignment(s).`); codForm.resetFields(); await refreshAll(qc)
    } catch (e) { message.error(errorMessage(e)) }
  }
  const bill = async () => {
    const v = await billForm.validateFields()
    try {
      const r = (await api.post<{ invoiceNumber?: string; shipments: number; total: number }>('/logistics/billing', { ...v, upTo: d8(v.upTo) })).data
      setLast(`Invoice ${r.invoiceNumber} raised for ${r.shipments} consignment(s), total ${amount(r.total, 0)}. Find it under Finance → Invoices.`); billForm.resetFields(); await refreshAll(qc)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>COD & freight billing</Typography.Title></div>
      {last && <Alert type="success" showIcon closable title={last} style={{ marginBottom: 16 }} onClose={() => setLast(null)} />}
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title="Monthly freight invoice" extra={<Typography.Text type="secondary">Account customers</Typography.Text>}>
            <Typography.Paragraph type="secondary">Raises one invoice for every un-billed consignment booked on account for the customer, up to the date chosen.</Typography.Paragraph>
            <Form form={billForm} layout="vertical" disabled={!can('logistics.shipments.edit')}>
              <Form.Item name="customerId" label="Customer" rules={[{ required: true }]}><ContactSelect customers /></Form.Item>
              <Form.Item name="upTo" label="Booked up to" extra="Blank = today"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
              <Button type="primary" onClick={bill}>Raise invoice</Button>
            </Form>
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card title="Remit COD to shipper">
            <Typography.Paragraph type="secondary">Pays the shipper all cash-on-delivery collected on their delivered consignments that hasn't been remitted yet.</Typography.Paragraph>
            {can('logistics.cod.remit') ? <Form form={codForm} layout="vertical" initialValues={{ date: dayjs() }}>
              <Form.Item name="customerId" label="Shipper" rules={[{ required: true }]}><ContactSelect customers /></Form.Item>
              <Row gutter={12}>
                <Col span={12}><Form.Item name="bankAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item></Col>
                <Col span={12}><Form.Item name="date" label="Date"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
              </Row>
              <Form.Item name="reference" label="Reference"><Input placeholder="IBFT / cheque no." /></Form.Item>
              <Button type="primary" onClick={remit}>Remit COD</Button>
            </Form> : <Alert type="info" showIcon title="COD remittance needs the 'logistics.cod.remit' permission." />}
          </Card>
        </Col>
      </Row>
    </>
  )
}
