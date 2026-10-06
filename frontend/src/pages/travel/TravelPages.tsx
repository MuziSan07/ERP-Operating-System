import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Popconfirm, Progress, Row,
  Segmented, Select, Space, Statistic, Table, Tag, Timeline, Typography,
} from 'antd'
import { CheckOutlined, DeleteOutlined, FileDoneOutlined, PlusOutlined, PrinterOutlined, StopOutlined, WarningOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  BOOKING_COLORS, DEP_COLORS, ITEM_TYPES, VISA_COLORS, VISA_STATUSES, words, type Booking, type BookingItemType, type BookingListItem, type Departure,
  type DepartureListItem, type DepartureStatus, type Guide, type ItineraryDay, type ManifestRow, type Passenger, type TourPackage, type TravelBookingStatus,
  type TravelDashboard, type VisaStatus,
} from '../../api/travel'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect, TaxRateSelect } from '../../components/FinancePickers'


const useDepartures = (openOnly = false) => useQuery({ queryKey: ['tour-deps', openOnly], queryFn: async () => (await api.get<DepartureListItem[]>('/tours/departures', { params: { openOnly, from: dayjs().format('YYYY-MM-DD') } })).data })
const usePackages = () => useQuery({ queryKey: ['tour-pkgs'], queryFn: async () => (await api.get<TourPackage[]>('/tours/packages')).data })

const Load = ({ d }: { d: DepartureListItem }) => <Progress percent={d.loadFactor} size="small" format={() => `${d.booked}/${d.capacity}`} status={d.seatsLeft === 0 ? 'success' : 'normal'} />

// ======================= Dashboard =======================

export function TravelDashboardPage() {
  const [open, setOpen] = useState<string | null>(null)
  const { data, isLoading, error } = useQuery({ queryKey: ['trv-dash'], retry: false, queryFn: async () => (await api.get<TravelDashboard>('/travel/dashboard')).data })
  if (error) return <Alert type="info" showIcon title={errorMessage(error)} />
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Travel & Tours</Typography.Title></div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Open files', data?.openBookings], ['Confirmed this month', data?.confirmedThisMonth], ['Sales this month', amount(data?.salesThisMonth, 0)], ['Margin this month', amount(data?.marginThisMonth, 0)]].map(([t, v]) => (
          <Col key={t as string} xs={12} md={6}><Card loading={isLoading} size="small"><Statistic title={t as string} value={v as string} /></Card></Col>
        ))}
      </Row>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Card title="Departures — next 60 days" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.upcomingDepartures} locale={{ emptyText: 'No departures scheduled' }}
              columns={[{ title: 'Departure', render: (_, d) => <><b>{d.packageName}</b><div><Typography.Text type="secondary">{fmtDate(d.startDate)}</Typography.Text></div></> },
                { title: 'Seats', width: 160, render: (_, d) => <Load d={d} /> }, { title: '', render: (_, d) => <Tag color={DEP_COLORS[d.status]}>{d.status}</Tag> }]} />
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title="Travelling in the next 30 days" loading={isLoading}>
            <Table size="small" rowKey="id" pagination={false} dataSource={data?.upcomingTravel} onRow={b => ({ onClick: () => setOpen(b.id), style: { cursor: 'pointer' } })}
              columns={[{ title: 'File', dataIndex: 'number' }, { title: 'Customer', dataIndex: 'customerName' }, { title: 'Travel', dataIndex: 'travelDate', render: (d?: string) => d ? fmtDate(d) : '—' },
                { title: '', render: (_, b) => b.warnings > 0 && <Tag color="orange" icon={<WarningOutlined />}>{b.warnings}</Tag> }]} />
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title="Visas in progress" loading={isLoading}>
            <Table size="small" rowKey={(_, i) => String(i)} pagination={false} dataSource={data?.pendingVisas} onRow={v => ({ onClick: () => setOpen(v.bookingId), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Passenger', render: (_, v) => v.passengerName ?? v.customerName }, { title: 'Country', dataIndex: 'country' },
                { title: 'Status', dataIndex: 'status', render: (s: VisaStatus) => <Tag color={VISA_COLORS[s]}>{words(s)}</Tag> },
                { title: 'Travel', dataIndex: 'travelDate', render: (d?: string) => d ? fmtDate(d) : '—' }]} />
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title={<Space><WarningOutlined style={{ color: '#fa8c16' }} />Passports expiring within 6 months of travel</Space>} loading={isLoading}>
            <Table size="small" rowKey={(_, i) => String(i)} pagination={false} dataSource={data?.passportWarnings} locale={{ emptyText: 'All good' }}
              columns={[{ title: 'Passenger', dataIndex: 'passengerName' }, { title: 'File', dataIndex: 'bookingNumber' }, { title: 'Passport', dataIndex: 'passportNo' },
                { title: 'Expires', dataIndex: 'passportExpiry', render: (d?: string) => d ? fmtDate(d) : 'unknown' }]} />
          </Card>
        </Col>
      </Row>
      {open && <BookingDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

// ======================= Bookings =======================

export function BookingsPage() {
  const { can } = useAuth()
  const [status, setStatus] = useState<TravelBookingStatus | 'All'>('All')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)
  const [editing, setEditing] = useState<Booking | 'new' | null>(null)
  const { data, isFetching } = useQuery({ queryKey: ['trv-bookings', status, search, page],
    queryFn: async () => (await api.get<PagedResult<BookingListItem>>('/travel/bookings', { params: { status: status === 'All' ? undefined : status, search, page, pageSize: 25 } })).data })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Travel bookings</Typography.Title>
        {can('travel.bookings.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New booking</Button>}
      </div>
      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Segmented value={status} onChange={v => { setStatus(v as typeof status); setPage(1) }} options={['All', 'Quotation', 'Confirmed', 'Invoiced', 'Cancelled']} />
          <Input.Search allowClear placeholder="File no., customer, passenger, passport" onSearch={v => { setSearch(v); setPage(1) }} style={{ width: 280 }} />
        </Space>
        <Table<BookingListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          onRow={b => ({ onClick: () => setOpen(b.id), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'File', dataIndex: 'number' },
            { title: 'Customer', dataIndex: 'customerName' },
            { title: 'Travel', dataIndex: 'travelDate', render: (d?: string) => d ? fmtDate(d) : '—' },
            { title: 'Pax', dataIndex: 'passengers' },
            { title: 'Services', dataIndex: 'services', render: (s: string) => s.split(', ').map(x => <Tag key={x}>{words(x)}</Tag>) },
            { title: 'Status', dataIndex: 'status', render: (s: TravelBookingStatus) => <Tag color={BOOKING_COLORS[s]}>{s}</Tag> },
            { title: 'Sell', align: 'right', render: (_, b) => amount(b.sellTotal, 0) },
            { title: 'Margin', align: 'right', render: (_, b) => amount(b.margin, 0) },
            { title: '', render: (_, b) => b.warnings > 0 && <Tag color="orange" icon={<WarningOutlined />}>{b.warnings}</Tag> },
          ]} />
      </Card>
      {editing && <BookingEditor booking={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} onSaved={setOpen} />}
      {open && <BookingDrawer id={open} onClose={() => setOpen(null)} onEdit={b => { setOpen(null); setEditing(b) }} />}
    </>
  )
}

interface ItemDraft {
  type: BookingItemType; description?: string; serviceDate?: Dayjs | null; supplierId?: string; quantity: number; unitCost: number; unitPrice: number
  taxRateId?: string; incomeAccountId?: string; tourDepartureId?: string; adults: number; children: number; airline?: string; pnr?: string
  ticketNumber?: string; route?: string; country?: string; visaStatus?: VisaStatus; passengerIndex?: number
}
interface PaxDraft extends Omit<Passenger, 'passportExpiry' | 'dateOfBirth'> { passportExpiry?: Dayjs | null; dateOfBirth?: Dayjs | null }

function BookingEditor({ booking, onClose, onSaved }: { booking?: Booking; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: departures = [] } = useDepartures(true)
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [pax, setPax] = useState<PaxDraft[]>(booking?.passengers.map(p => ({ ...p, passportExpiry: p.passportExpiry ? dayjs(p.passportExpiry) : null, dateOfBirth: p.dateOfBirth ? dayjs(p.dateOfBirth) : null }))
    ?? [{ fullName: '', type: 'Adult' }])
  const [items, setItems] = useState<ItemDraft[]>(booking?.items.map(i => ({ ...i, serviceDate: i.serviceDate ? dayjs(i.serviceDate) : null,
    passengerIndex: i.passengerId ? booking.passengers.findIndex(p => p.id === i.passengerId) : undefined })) ?? [])
  const setP = (i: number, p: Partial<PaxDraft>) => setPax(ps => ps.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const setI = (i: number, p: Partial<ItemDraft>) => setItems(xs => xs.map((x, j) => (j === i ? { ...x, ...p } : x)))
  const addItem = (type: BookingItemType) => setItems(xs => [...xs, { type, quantity: type === 'TourSeats' ? 1 : pax.length || 1, unitCost: 0, unitPrice: 0,
    adults: pax.filter(p => p.type === 'Adult').length || 1, children: pax.filter(p => p.type === 'Child').length, visaStatus: type === 'Visa' ? 'NotStarted' : undefined }])
  // Estimate only (adult price); the server prices children from the departure.
  const seatsPrice = (it: ItemDraft) => (departures.find(x => x.id === it.tourDepartureId)?.adultPrice ?? 0) * it.adults
  const sell = items.reduce((s, it) => s + (it.type === 'TourSeats' ? (it.unitPrice || seatsPrice(it)) : it.quantity * it.unitPrice), 0)
  const cost = items.reduce((s, it) => s + it.quantity * it.unitCost, 0)

  const save = async () => {
    const v = await form.validateFields()
    if (pax.some(p => !p.fullName)) { message.error('Every passenger needs a name.'); return }
    if (!items.length) { message.error('Add at least one service.'); return }
    setBusy(true)
    try {
      const body = {
        entityId: v.entityId, customerId: v.customerId, contactPhone: v.contactPhone, travelDate: v.travelDate?.format('YYYY-MM-DD'), notes: v.notes,
        passengers: pax.map(p => ({ ...p, passportExpiry: p.passportExpiry?.format('YYYY-MM-DD'), dateOfBirth: p.dateOfBirth?.format('YYYY-MM-DD') })),
        items: items.map(it => ({ ...it, serviceDate: it.serviceDate?.format('YYYY-MM-DD') })),
      }
      const res = booking ? await api.put<Booking>(`/travel/bookings/${booking.id}`, body) : await api.post<Booking>('/travel/bookings', body)
      message.success(booking ? 'Booking saved' : `Quotation ${res.data.number} created`)
      await qc.invalidateQueries({ queryKey: ['trv-bookings'] })
      await qc.invalidateQueries({ queryKey: ['tour-deps'] })
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Drawer open onClose={onClose} size={1100} title={booking ? `Edit ${booking.number}` : 'New travel booking'} extra={<Button type="primary" loading={busy} onClick={save}>Save</Button>}>
      <Form form={form} layout="vertical" initialValues={booking ? { ...booking, travelDate: booking.travelDate ? dayjs(booking.travelDate) : undefined }
        : { entityId: me?.entities.find(e => e.permissions.includes('travel.bookings.create'))?.id }}>
        <Row gutter={12}>
          <Col xs={24} md={9}><Form.Item name="customerId" label="Customer (billed)" rules={[{ required: true }]}><ContactSelect customers /></Form.Item></Col>
          <Col xs={12} md={5}><Form.Item name="contactPhone" label="Contact phone"><Input /></Form.Item></Col>
          <Col xs={12} md={4}><Form.Item name="travelDate" label="Travel date" extra="Blank = first service"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={24} md={6}><Form.Item name="entityId" label="Branch" rules={[{ required: true }]}><EntityPicker permission="travel.bookings.create" /></Form.Item></Col>
        </Row>
      </Form>
      <Typography.Title level={5}>Passengers</Typography.Title>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={pax} scroll={{ x: 950 }}
        columns={[
          { title: 'Full name (as on passport)', width: 220, render: (_, p, i) => <Input value={p.fullName} onChange={e => setP(i, { fullName: e.target.value })} /> },
          { title: 'Type', width: 100, render: (_, p, i) => <Select value={p.type} onChange={v => setP(i, { type: v })} options={['Adult', 'Child', 'Infant'].map(t => ({ value: t, label: t }))} style={{ width: '100%' }} /> },
          { title: 'CNIC', width: 150, render: (_, p, i) => <Input value={p.cnic} onChange={e => setP(i, { cnic: e.target.value })} /> },
          { title: 'Passport', width: 120, render: (_, p, i) => <Input value={p.passportNo} onChange={e => setP(i, { passportNo: e.target.value })} /> },
          { title: 'Expiry', width: 140, render: (_, p, i) => <DatePicker value={p.passportExpiry} onChange={d => setP(i, { passportExpiry: d })} format="DD MMM YY" /> },
          { title: 'Nationality', width: 120, render: (_, p, i) => <Input value={p.nationality} onChange={e => setP(i, { nationality: e.target.value })} /> },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={pax.length === 1} onClick={() => setPax(ps => ps.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setPax(ps => [...ps, { fullName: '', type: 'Adult' }])}>Add passenger</Button>

      <Typography.Title level={5} style={{ marginTop: 24 }}>Services</Typography.Title>
      <Space orientation="vertical" style={{ width: '100%' }}>
        {items.map((it, i) => (
          <Card key={i} size="small" title={<Space><Tag color="blue">{words(it.type)}</Tag></Space>} extra={<Button size="small" danger icon={<DeleteOutlined />} onClick={() => setItems(xs => xs.filter((_, j) => j !== i))} />}>
            <Row gutter={8}>
              {it.type === 'TourSeats' ? <>
                <Col xs={24} md={10}><Typography.Text type="secondary">Departure</Typography.Text>
                  <Select value={it.tourDepartureId} onChange={v => setI(i, { tourDepartureId: v })} style={{ width: '100%' }} placeholder="Choose departure"
                    options={departures.map(d => ({ value: d.id, label: `${d.packageName} — ${dayjs(d.startDate).format('DD MMM')} (${d.seatsLeft} seats left)`, disabled: d.seatsLeft <= 0 && d.id !== it.tourDepartureId }))} /></Col>
                <Col xs={8} md={3}><Typography.Text type="secondary">Adults</Typography.Text><InputNumber min={0} value={it.adults} onChange={v => setI(i, { adults: v ?? 0 })} style={{ width: '100%' }} /></Col>
                <Col xs={8} md={3}><Typography.Text type="secondary">Children</Typography.Text><InputNumber min={0} value={it.children} onChange={v => setI(i, { children: v ?? 0 })} style={{ width: '100%' }} /></Col>
                <Col xs={8} md={4}><Typography.Text type="secondary">Total price</Typography.Text><InputNumber min={0} value={it.unitPrice || undefined} placeholder="Auto" onChange={v => setI(i, { unitPrice: v ?? 0 })} style={{ width: '100%' }} /></Col>
              </> : <>
                <Col xs={24} md={8}><Typography.Text type="secondary">Description</Typography.Text><Input value={it.description} onChange={e => setI(i, { description: e.target.value })} placeholder={it.type === 'Hotel' ? 'Serena Islamabad, 2 nights, 1 double' : ''} /></Col>
                <Col xs={12} md={4}><Typography.Text type="secondary">Date</Typography.Text><DatePicker value={it.serviceDate} onChange={d => setI(i, { serviceDate: d })} format="DD MMM YY" style={{ width: '100%' }} /></Col>
                <Col xs={12} md={6}><Typography.Text type="secondary">Supplier</Typography.Text><ContactSelect value={it.supplierId} onChange={v => setI(i, { supplierId: v })} vendors /></Col>
                <Col xs={8} md={2}><Typography.Text type="secondary">Qty</Typography.Text><InputNumber min={0} value={it.quantity} onChange={v => setI(i, { quantity: v ?? 0 })} style={{ width: '100%' }} /></Col>
                <Col xs={8} md={2}><Typography.Text type="secondary">Cost</Typography.Text><InputNumber min={0} value={it.unitCost} onChange={v => setI(i, { unitCost: v ?? 0 })} style={{ width: '100%' }} /></Col>
                <Col xs={8} md={2}><Typography.Text type="secondary">Sell</Typography.Text><InputNumber min={0} value={it.unitPrice} onChange={v => setI(i, { unitPrice: v ?? 0 })} style={{ width: '100%' }} /></Col>
              </>}
              {it.type === 'Flight' && <>
                <Col xs={12} md={4} style={{ marginTop: 8 }}><Input addonBefore="Airline" value={it.airline} onChange={e => setI(i, { airline: e.target.value })} /></Col>
                <Col xs={12} md={4} style={{ marginTop: 8 }}><Input addonBefore="Route" placeholder="ISB-GIL" value={it.route} onChange={e => setI(i, { route: e.target.value })} /></Col>
                <Col xs={12} md={4} style={{ marginTop: 8 }}><Input addonBefore="PNR" value={it.pnr} onChange={e => setI(i, { pnr: e.target.value })} /></Col>
                <Col xs={12} md={6} style={{ marginTop: 8 }}><Input addonBefore="Ticket no." value={it.ticketNumber} onChange={e => setI(i, { ticketNumber: e.target.value })} /></Col>
              </>}
              {it.type === 'Visa' && <>
                <Col xs={12} md={6} style={{ marginTop: 8 }}><Input addonBefore="Country" value={it.country} onChange={e => setI(i, { country: e.target.value })} /></Col>
                <Col xs={12} md={6} style={{ marginTop: 8 }}><Select value={it.passengerIndex} onChange={v => setI(i, { passengerIndex: v })} placeholder="For passenger" style={{ width: '100%' }} options={pax.map((p, j) => ({ value: j, label: p.fullName || `Passenger ${j + 1}` }))} /></Col>
              </>}
              <Col xs={12} md={6} style={{ marginTop: 8 }}><TaxRateSelect value={it.taxRateId} onChange={v => setI(i, { taxRateId: v })} /></Col>
            </Row>
          </Card>
        ))}
      </Space>
      <Space wrap style={{ marginTop: 8 }}>{ITEM_TYPES.map(t => <Button key={t} size="small" icon={<PlusOutlined />} onClick={() => addItem(t)}>{words(t)}</Button>)}</Space>
      <Row justify="end" style={{ marginTop: 16 }}><Col xs={24} sm={9}><Descriptions size="small" bordered column={1}>
        <Descriptions.Item label="Selling price (excl. tax)">{amount(sell, 0)}</Descriptions.Item>
        <Descriptions.Item label="Supplier cost">{amount(cost, 0)}</Descriptions.Item>
        <Descriptions.Item label={<b>Margin</b>}><b>{amount(sell - cost, 0)}</b></Descriptions.Item>
      </Descriptions></Col></Row>
      <Form form={form} layout="vertical" style={{ marginTop: 12 }}><Form.Item name="notes" label="Notes (dietary, special requests)"><Input.TextArea rows={2} /></Form.Item></Form>
    </Drawer>
  )
}

function BookingDrawer({ id, onClose, onEdit }: { id: string; onClose: () => void; onEdit?: (b: Booking) => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [depositing, setDepositing] = useState(false)
  const key = ['trv-booking', id]
  const { data: b } = useQuery({ queryKey: key, queryFn: async () => (await api.get<Booking>(`/travel/bookings/${id}`)).data })
  const refresh = async () => { await Promise.all([key, ['trv-bookings'], ['trv-dash'], ['tour-deps']].map(k => qc.invalidateQueries({ queryKey: k }))) }
  const act = async (fn: () => Promise<unknown>, done: string) => { try { await fn(); message.success(done); await refresh() } catch (e) { message.error(errorMessage(e)) } }
  if (!b) return <Drawer open onClose={onClose} loading />
  const E = b.entityId
  const editable = b.status === 'Quotation' || b.status === 'Confirmed'
  return (
    <Drawer open onClose={onClose} size={980} title={<Space wrap>{b.number}<Tag color={BOOKING_COLORS[b.status]}>{b.status}</Tag>{b.customerName}</Space>}
      extra={
        <Space wrap>
          {editable && onEdit && can('travel.bookings.edit', E) && <Button onClick={() => onEdit(b)}>Edit</Button>}
          {b.status === 'Quotation' && can('travel.bookings.edit', E) && <Button type="primary" icon={<CheckOutlined />} onClick={() => act(() => api.post(`/travel/bookings/${id}/confirm`), 'Booking confirmed — seats held')}>Confirm</Button>}
          {editable && can('travel.bookings.edit', E) && <Button onClick={() => setDepositing(true)}>Take advance</Button>}
          {b.status === 'Confirmed' && can('travel.bookings.edit', E) && (
            <Popconfirm title="Issue the customer invoice?" description="Advances are applied automatically." onConfirm={() => act(() => api.post(`/travel/bookings/${id}/invoice`), 'Invoice issued')}>
              <Button type="primary" icon={<FileDoneOutlined />}>Invoice customer</Button>
            </Popconfirm>
          )}
          {(b.status === 'Confirmed' || b.status === 'Invoiced') && b.items.some(i => !i.billId && i.supplierId && i.unitCost > 0) && can('travel.bookings.edit', E) && (
            <Button onClick={() => act(() => api.post(`/travel/bookings/${id}/supplier-bills`), 'Draft supplier bills created for Accounts to approve')}>Create supplier bills</Button>
          )}
          {editable && can('travel.bookings.cancel', E) && <Popconfirm title="Cancel this booking?" description="Any advance stays on the customer's account until refunded." onConfirm={() => act(() => api.post(`/travel/bookings/${id}/cancel`), 'Cancelled')}><Button danger icon={<StopOutlined />}>Cancel</Button></Popconfirm>}
          <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print voucher</Button>
        </Space>
      }>
      {b.warnings.length > 0 && <Alert type="warning" showIcon style={{ marginBottom: 16 }} title="Check before travel" description={<ul style={{ margin: 0, paddingLeft: 18 }}>{b.warnings.map(w => <li key={w}>{w}</li>)}</ul>} />}
      <div className="print-area">
        <Descriptions size="small" bordered column={{ xs: 1, md: 3 }} style={{ marginBottom: 16 }}>
          <Descriptions.Item label="Customer">{b.customerName}</Descriptions.Item>
          <Descriptions.Item label="Phone">{b.contactPhone ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Travel date">{b.travelDate ? fmtDate(b.travelDate) : '—'}</Descriptions.Item>
          {b.invoiceNumber && <Descriptions.Item label="Invoice">{b.invoiceNumber}</Descriptions.Item>}
          <Descriptions.Item label="Consultant">{b.createdByName}</Descriptions.Item>
          {b.notes && <Descriptions.Item label="Notes" span={3}>{b.notes}</Descriptions.Item>}
        </Descriptions>
        <Typography.Title level={5}>Passengers</Typography.Title>
        <Table size="small" pagination={false} rowKey="id" dataSource={b.passengers} style={{ marginBottom: 16 }}
          columns={[{ title: 'Name', dataIndex: 'fullName' }, { title: 'Type', dataIndex: 'type' }, { title: 'CNIC', dataIndex: 'cnic' }, { title: 'Passport', dataIndex: 'passportNo' },
            { title: 'Expiry', dataIndex: 'passportExpiry', render: (d?: string) => d ? fmtDate(d) : '' }, { title: 'Nationality', dataIndex: 'nationality' }]} />
        <Typography.Title level={5}>Services</Typography.Title>
        <Table size="small" pagination={false} rowKey="id" dataSource={b.items} scroll={{ x: 800 }}
          columns={[
            { title: 'Service', render: (_, i) => <><Tag>{words(i.type)}</Tag>{i.description}
              {(i.pnr || i.ticketNumber) && <div><Typography.Text type="secondary">{i.pnr && `PNR ${i.pnr}`} {i.ticketNumber && `· Ticket ${i.ticketNumber}`}</Typography.Text></div>}
              {i.passengerName && <div><Typography.Text type="secondary">For {i.passengerName}</Typography.Text></div>}</> },
            { title: 'Date', dataIndex: 'serviceDate', render: (d?: string) => d ? fmtDate(d) : '' },
            { title: 'Supplier', dataIndex: 'supplierName' },
            { title: 'Visa', render: (_, i) => i.type === 'Visa' && (can('travel.visas.edit', E) && b.status !== 'Cancelled'
              ? <Select size="small" value={i.visaStatus} style={{ width: 170 }} onChange={v => act(() => api.put(`/travel/bookings/${id}/visas/${i.id}`, { status: v }), 'Visa status updated')}
                  options={VISA_STATUSES.map(s => ({ value: s, label: <Tag color={VISA_COLORS[s]}>{words(s)}</Tag> }))} />
              : <Tag color={VISA_COLORS[i.visaStatus ?? 'NotStarted']}>{words(i.visaStatus ?? 'NotStarted')}</Tag>) },
            { title: 'Sell', align: 'right', render: (_, i) => amount(i.sellTotal, 0) },
            { title: 'Cost', align: 'right', render: (_, i) => <>{amount(i.costTotal, 0)}{i.billId && <Tag color="purple" style={{ marginLeft: 4 }}>Billed</Tag>}</> },
          ]} />
        <Row gutter={16} style={{ marginTop: 16 }}>
          <Col xs={24} md={12}>
            {b.deposits.length > 0 && <Card size="small" title="Advances">
              <Timeline items={b.deposits.map(d => ({ children: <>{fmtDate(d.date)} · {amount(d.amount, 0)} · {d.bankAccountName}{d.reference && ` · ${d.reference}`} {d.applied && <Tag color="green">Applied</Tag>}</> }))} />
            </Card>}
          </Col>
          <Col xs={24} md={12}>
            <Descriptions size="small" bordered column={1}>
              <Descriptions.Item label="Selling price (excl. tax)">{amount(b.sellTotal, 0)}</Descriptions.Item>
              <Descriptions.Item label="Supplier cost">{amount(b.costTotal, 0)}</Descriptions.Item>
              <Descriptions.Item label={<b>Margin</b>}><b>{amount(b.margin, 0)}</b> {b.sellTotal > 0 && `(${Math.round(b.margin / b.sellTotal * 100)}%)`}</Descriptions.Item>
              <Descriptions.Item label="Advances received">{amount(b.depositsTotal, 0)}</Descriptions.Item>
            </Descriptions>
          </Col>
        </Row>
      </div>
      {depositing && <AdvanceModal id={id} onClose={() => setDepositing(false)} onDone={refresh} />}
    </Drawer>
  )
}

function AdvanceModal({ id, onClose, onDone }: { id: string; onClose: () => void; onDone: () => Promise<void> }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const save = async () => {
    const v = await form.validateFields()
    try { await api.post(`/travel/bookings/${id}/deposits`, { ...v, date: v.date.format('YYYY-MM-DD') }); message.success('Advance recorded'); await onDone(); onClose() } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title="Take advance" onCancel={onClose} onOk={save} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ date: dayjs() }}>
        <Form.Item name="amount" label="Amount" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="bankAccountId" label="Received into" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item>
        <Form.Item name="date" label="Date"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
        <Form.Item name="reference" label="Reference"><Input /></Form.Item>
      </Form>
    </Modal>
  )
}

// ======================= Departures =======================

export function DeparturesPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: packages = [] } = usePackages()
  const [open, setOpen] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['tour-deps', 'all'], queryFn: async () => (await api.get<DepartureListItem[]>('/tours/departures', { params: { from: dayjs().subtract(30, 'day').format('YYYY-MM-DD') } })).data })
  const create = async () => {
    const v = await form.validateFields()
    try {
      const res = await api.post<Departure>('/tours/departures', { ...v, startDate: v.startDate.format('YYYY-MM-DD') })
      message.success(`Departure ${res.data.code} scheduled`)
      setCreating(false)
      await qc.invalidateQueries({ queryKey: ['tour-deps'] })
      setOpen(res.data.id)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Tour departures</Typography.Title>
        {can('tourism.departures.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Schedule departure</Button>}
      </div>
      <Card>
        <Table<DepartureListItem> rowKey="id" loading={isLoading} dataSource={data} scroll={{ x: 900 }} onRow={d => ({ onClick: () => setOpen(d.id), style: { cursor: 'pointer' } })}
          columns={[
            { title: 'Departure', render: (_, d) => <><b>{d.packageName}</b> <Typography.Text type="secondary">{d.code}</Typography.Text><div><Typography.Text type="secondary">{d.destination}</Typography.Text></div></> },
            { title: 'Dates', render: (_, d) => `${dayjs(d.startDate).format('DD MMM')} – ${dayjs(d.endDate).format('DD MMM YYYY')}` },
            { title: 'Seats', width: 170, render: (_, d) => <Load d={d} /> },
            { title: 'From', align: 'right', render: (_, d) => amount(d.adultPrice, 0) },
            { title: 'Guides', dataIndex: 'guides' },
            { title: 'Status', dataIndex: 'status', render: (s: DepartureStatus) => <Tag color={DEP_COLORS[s]}>{s}</Tag> },
          ]} />
      </Card>
      <Modal open={creating} title="Schedule departure" onCancel={() => setCreating(false)} onOk={create} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={{ capacity: 15 }}>
          <Form.Item name="tourPackageId" label="Package" rules={[{ required: true }]}><Select options={packages.filter(p => p.isActive).map(p => ({ value: p.id, label: `${p.name} (${p.durationDays} days)` }))} /></Form.Item>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="startDate" label="Start date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
            <Col span={12}><Form.Item name="capacity" label="Seats" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="adultPrice" label="Adult price" extra="Blank = package price"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="childPrice" label="Child price"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
          <Form.Item name="notes" label="Notes"><Input /></Form.Item>
        </Form>
      </Modal>
      {open && <DepartureDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function DepartureDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const key = ['tour-dep', id]
  const { data: d } = useQuery({ queryKey: key, queryFn: async () => (await api.get<Departure>(`/tours/departures/${id}`)).data })
  const { data: manifest = [] } = useQuery({ queryKey: ['tour-manifest', id], queryFn: async () => (await api.get<ManifestRow[]>(`/tours/departures/${id}/manifest`)).data })
  const { data: guides = [] } = useQuery({ queryKey: ['tour-guides'], queryFn: async () => (await api.get<Guide[]>('/tours/guides')).data })
  const [guideId, setGuideId] = useState<string>()
  const [costForm] = Form.useForm()
  const refresh = async () => { await Promise.all([key, ['tour-deps']].map(k => qc.invalidateQueries({ queryKey: k }))) }
  const act = async (fn: () => Promise<unknown>, done: string) => { try { await fn(); message.success(done); await refresh() } catch (e) { message.error(errorMessage(e)) } }
  if (!d) return <Drawer open onClose={onClose} loading />
  const edit = can('tourism.departures.edit', d.entityId)
  return (
    <Drawer open onClose={onClose} size={960} title={<Space wrap>{d.packageName}<Tag>{d.code}</Tag><Tag color={DEP_COLORS[d.status]}>{d.status}</Tag></Space>}
      extra={<Space wrap>
        {edit && d.status === 'Open' && <Button onClick={() => act(() => api.post(`/tours/departures/${id}/status`, { status: 'Closed' }), 'Sales closed')}>Close sales</Button>}
        {edit && d.status === 'Closed' && <Button onClick={() => act(() => api.post(`/tours/departures/${id}/status`, { status: 'Open' }), 'Reopened')}>Reopen</Button>}
        {edit && ['Open', 'Closed'].includes(d.status) && <Button onClick={() => act(() => api.post(`/tours/departures/${id}/status`, { status: 'Departed' }), 'Marked departed')}>Departed</Button>}
        {edit && d.status === 'Departed' && <Button onClick={() => act(() => api.post(`/tours/departures/${id}/status`, { status: 'Completed' }), 'Completed')}>Completed</Button>}
        {edit && d.booked === 0 && d.status !== 'Cancelled' && <Popconfirm title="Cancel departure?" onConfirm={() => act(() => api.post(`/tours/departures/${id}/status`, { status: 'Cancelled' }), 'Cancelled')}><Button danger>Cancel</Button></Popconfirm>}
        <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print manifest</Button>
      </Space>}>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}><Card size="small"><Statistic title="Seats sold" value={`${d.booked} / ${d.capacity}`} /></Card></Col>
        <Col xs={12} md={6}><Card size="small"><Statistic title="Revenue" value={amount(d.revenue, 0)} /></Card></Col>
        <Col xs={12} md={6}><Card size="small"><Statistic title="Costs (incl. guides)" value={amount(d.totalCost, 0)} /></Card></Col>
        <Col xs={12} md={6}><Card size="small"><Statistic title="Margin" value={amount(d.margin, 0)} styles={{ content: { color: d.margin >= 0 ? '#3f8600' : '#cf1322' } }} /></Card></Col>
      </Row>
      <Row gutter={16}>
        <Col xs={24} md={10}>
          <Card size="small" title="Guides" style={{ marginBottom: 16 }}>
            {d.guides.map(g => <div key={g.id} style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}><span><b>{g.guideName}</b> {g.role && <Tag>{g.role}</Tag>}<div><Typography.Text type="secondary">{g.phone} · {amount(g.dailyRate, 0)}/day</Typography.Text></div></span>
              {edit && <Button size="small" danger icon={<DeleteOutlined />} onClick={() => act(() => api.delete(`/tours/departures/${id}/guides/${g.id}`), 'Guide removed')} />}</div>)}
            {d.guides.length === 0 && <Typography.Text type="secondary">No guide assigned</Typography.Text>}
            {edit && <Space.Compact style={{ width: '100%', marginTop: 8 }}>
              <Select value={guideId} onChange={setGuideId} placeholder="Assign guide" style={{ width: '100%' }} options={guides.filter(g => g.isActive && !d.guides.some(x => x.guideId === g.id)).map(g => ({ value: g.id, label: `${g.fullName}${g.languages ? ` (${g.languages})` : ''}` }))} />
              <Button disabled={!guideId} onClick={() => act(() => api.post(`/tours/departures/${id}/guides`, { guideId, role: d.guides.length === 0 ? 'Lead guide' : 'Assistant' }).then(() => setGuideId(undefined)), 'Guide assigned')}>Assign</Button>
            </Space.Compact>}
          </Card>
        </Col>
        <Col xs={24} md={14}>
          <Card size="small" title="Operating costs" style={{ marginBottom: 16 }}>
            <Table size="small" pagination={false} rowKey="id" dataSource={d.costs} locale={{ emptyText: 'No costs yet' }}
              columns={[{ title: 'Cost', dataIndex: 'description' }, { title: 'Vendor', dataIndex: 'vendorName' }, { title: 'Amount', align: 'right', render: (_, c) => amount(c.amount, 0) },
                { key: 'b', render: (_, c) => c.billId ? <Tag color="purple">Bill {c.billNumber}</Tag> : edit && c.vendorId && <Button size="small" onClick={() => act(() => api.post(`/tours/departures/${id}/costs/${c.id}/bill`), 'Draft bill created')}>Create bill</Button> }]} />
            {edit && <Form form={costForm} layout="inline" style={{ marginTop: 8, rowGap: 8 }} onFinish={v => act(() => api.post(`/tours/departures/${id}/costs`, v).then(() => costForm.resetFields()), 'Cost added')}>
              <Form.Item name="description" rules={[{ required: true }]}><Input placeholder="Coaster, hotels, permits…" /></Form.Item>
              <Form.Item name="vendorId" style={{ minWidth: 180 }}><ContactSelect vendors /></Form.Item>
              <Form.Item name="amount" rules={[{ required: true }]}><InputNumber min={0} placeholder="Amount" /></Form.Item>
              <Button htmlType="submit" icon={<PlusOutlined />} />
            </Form>}
          </Card>
        </Col>
      </Row>
      <div className="print-area">
        <Typography.Title level={5}>Passenger manifest — {d.packageName}, {fmtDate(d.startDate)} to {fmtDate(d.endDate)}</Typography.Title>
        <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={manifest} locale={{ emptyText: 'No confirmed passengers yet' }} scroll={{ x: 700 }}
          columns={[{ title: '#', render: (_, __, i) => i + 1, width: 40 }, { title: 'Passenger', render: (_, m) => <>{m.passengerName} {m.type !== 'Adult' && <Tag>{m.type}</Tag>}</> },
            { title: 'File', render: (_, m) => `${m.bookingNumber} · ${m.customerName}` }, { title: 'CNIC', dataIndex: 'cnic' }, { title: 'Passport', render: (_, m) => <>{m.passportNo}{m.passportWarning && <Tag color="orange" style={{ marginLeft: 4 }}>Expiry</Tag>}</> },
            { title: 'Phone', dataIndex: 'phone' }]} />
      </div>
    </Drawer>
  )
}

// ======================= Packages & guides =======================

export function PackagesPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isLoading } = usePackages()
  const { data: guides = [] } = useQuery({ queryKey: ['tour-guides'], queryFn: async () => (await api.get<Guide[]>('/tours/guides')).data })
  const [editing, setEditing] = useState<TourPackage | 'new' | null>(null)
  const [editingGuide, setEditingGuide] = useState<Guide | 'new' | null>(null)
  const [days, setDays] = useState<ItineraryDay[]>([])
  const [form] = Form.useForm()
  const [gForm] = Form.useForm()
  const duration = Form.useWatch('durationDays', form) as number | undefined
  const openEditor = (p: TourPackage | 'new') => { setEditing(p); setDays(p === 'new' ? [] : p.itinerary) }
  const save = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, itinerary: days.filter(x => x.title) }
      if (editing === 'new') await api.post('/tours/packages', body)
      else if (editing) await api.put(`/tours/packages/${editing.id}`, body)
      message.success('Package saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['tour-pkgs'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const saveGuide = async () => {
    const v = await gForm.validateFields()
    try {
      if (editingGuide === 'new') await api.post('/tours/guides', v)
      else if (editingGuide) await api.put(`/tours/guides/${editingGuide.id}`, v)
      setEditingGuide(null)
      await qc.invalidateQueries({ queryKey: ['tour-guides'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const ensureDays = (n: number) => setDays(ds => Array.from({ length: n }, (_, i) => ds.find(d => d.dayNumber === i + 1) ?? { dayNumber: i + 1, title: '' }))
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Packages & guides</Typography.Title>
        {can('tourism.packages.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => openEditor('new')}>New package</Button>}
      </div>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={15}>
          <Card title="Tour packages">
            {isLoading ? null : data.length === 0 ? <Empty /> : (
              <Row gutter={[12, 12]}>{data.map(p => (
                <Col key={p.id} xs={24} md={12}>
                  <Card size="small" hoverable onClick={() => can('tourism.packages.edit', p.entityId) && openEditor(p)}
                    title={<Space>{p.name}{!p.isActive && <Tag>Inactive</Tag>}</Space>} extra={<Tag color="blue">{p.durationDays}D</Tag>}>
                    <div>{p.destination}</div>
                    <div><b>{amount(p.adultPrice, 0)}</b> <Typography.Text type="secondary">per adult · child {amount(p.childPrice, 0)}</Typography.Text></div>
                    <Typography.Text type="secondary">{p.upcomingDepartures} upcoming departure(s) · {p.itinerary.length} itinerary days</Typography.Text>
                  </Card>
                </Col>))}
              </Row>)}
          </Card>
        </Col>
        <Col xs={24} xl={9}>
          <Card title="Guides" extra={can('tourism.guides.create') && <Button size="small" icon={<PlusOutlined />} onClick={() => setEditingGuide('new')}>Add</Button>}>
            <Table size="small" rowKey="id" pagination={false} dataSource={guides} onRow={g => ({ onClick: () => can('tourism.guides.edit') && setEditingGuide(g), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Guide', render: (_, g) => <><b>{g.fullName}</b>{!g.isActive && <Tag style={{ marginLeft: 4 }}>Inactive</Tag>}<div><Typography.Text type="secondary">{g.languages}</Typography.Text></div></> },
                { title: 'Rate', align: 'right', render: (_, g) => amount(g.dailyRate, 0) }, { title: 'Next', render: (_, g) => g.upcomingDepartures[0] ?? '—' }]} />
          </Card>
        </Col>
      </Row>
      <Drawer open={!!editing} onClose={() => setEditing(null)} size={900} title={editing === 'new' ? 'New tour package' : 'Edit package'} extra={<Button type="primary" onClick={save}>Save</Button>} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { durationDays: 3, isActive: true, adultPrice: 0, childPrice: 0, singleSupplement: 0 } : editing ?? {}}
          onValuesChange={c => { if (c.durationDays) ensureDays(c.durationDays) }}>
          <Row gutter={12}>
            <Col xs={8} md={4}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col xs={16} md={10}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input placeholder="Hunza Cherry Blossom" /></Form.Item></Col>
            <Col xs={16} md={6}><Form.Item name="destination" label="Destination" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col xs={8} md={4}><Form.Item name="durationDays" label="Days" rules={[{ required: true }]}><InputNumber min={1} max={60} style={{ width: '100%' }} /></Form.Item></Col>
            <Col xs={8} md={5}><Form.Item name="adultPrice" label="Adult price"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col xs={8} md={5}><Form.Item name="childPrice" label="Child price"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col xs={8} md={5}><Form.Item name="singleSupplement" label="Single supplement"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col xs={12} md={5}><Form.Item name="taxRateId" label="Sales tax"><TaxRateSelect /></Form.Item></Col>
            <Col xs={12} md={4}><Form.Item name="isActive" valuePropName="checked" label=" "><Checkbox>Active</Checkbox></Form.Item></Col>
            <Col span={24}><Form.Item name="summary" label="Summary"><Input.TextArea rows={2} /></Form.Item></Col>
            <Col xs={24} md={12}><Form.Item name="inclusions" label="Includes"><Input.TextArea rows={3} /></Form.Item></Col>
            <Col xs={24} md={12}><Form.Item name="exclusions" label="Excludes"><Input.TextArea rows={3} /></Form.Item></Col>
          </Row>
        </Form>
        <Typography.Title level={5}>Itinerary</Typography.Title>
        {days.length === 0 && <Button onClick={() => ensureDays(duration ?? 1)}>Add itinerary days</Button>}
        {days.map((d, i) => (
          <Row key={d.dayNumber} gutter={8} style={{ marginBottom: 8 }}>
            <Col flex="60px"><Tag color="blue">Day {d.dayNumber}</Tag></Col>
            <Col xs={24} md={7}><Input placeholder="Title, e.g. Islamabad → Chilas" value={d.title} onChange={e => setDays(ds => ds.map((x, j) => (j === i ? { ...x, title: e.target.value } : x)))} /></Col>
            <Col xs={24} md={9}><Input placeholder="Description" value={d.description} onChange={e => setDays(ds => ds.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)))} /></Col>
            <Col xs={24} md={5}><Input placeholder="Overnight at" value={d.overnight} onChange={e => setDays(ds => ds.map((x, j) => (j === i ? { ...x, overnight: e.target.value } : x)))} /></Col>
          </Row>
        ))}
      </Drawer>
      <Modal open={!!editingGuide} title={editingGuide === 'new' ? 'New guide' : 'Edit guide'} onCancel={() => setEditingGuide(null)} onOk={saveGuide} destroyOnHidden>
        <Form form={gForm} layout="vertical" preserve={false} initialValues={editingGuide === 'new' ? { isActive: true, dailyRate: 0 } : editingGuide ?? {}}>
          <Form.Item name="fullName" label="Name" rules={[{ required: true }]}><Input /></Form.Item>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="licenseNo" label="Licence no."><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="languages" label="Languages"><Input placeholder="Urdu, English, Chinese" /></Form.Item></Col>
            <Col span={8}><Form.Item name="dailyRate" label="Daily rate"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
          <Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item>
        </Form>
      </Modal>
    </>
  )
}
