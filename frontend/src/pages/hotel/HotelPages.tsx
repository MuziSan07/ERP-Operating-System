import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, AutoComplete, Badge, Button, Card, Checkbox, Col, DatePicker, Descriptions, Divider, Drawer, Empty, Form, Input, InputNumber, Modal,
  Popconfirm, Row, Segmented, Select, Space, Statistic, Table, Tabs, Tag, Tooltip, Typography,
} from 'antd'
import { CrownFilled, LoginOutlined, LogoutOutlined, MoonOutlined, PlusOutlined, PrinterOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  HK_COLORS, RES_COLORS, SOURCES, label, pkToday, type Availability, type FolioChargeType, type FrontDesk, type Guest, type HotelReport,
  type HousekeepingStatus, type Reservation, type ReservationListItem, type ReservationStatus, type Room, type RoomType, type TapeChart,
} from '../../api/hotel'
import { useAuth } from '../../auth/AuthContext'
import { AccountSelect, ContactSelect, TaxRateSelect } from '../../components/FinancePickers'
import { ItemSelect, WarehouseSelect } from '../inventory/InventoryPages'

/* eslint-disable react-refresh/only-export-components */

/** The property (hotel entity) being worked on, remembered per browser. */
function useProperty(permission = 'hotel.reservations.view') {
  const { me } = useAuth()
  const options = (me?.entities ?? []).filter(e => e.permissions.includes(permission))
  const [id, setId] = useState<string | undefined>(() => {
    try { return localStorage.getItem('erpos.hotel') ?? undefined } catch { return undefined }
  })
  // Default to a property that actually has rooms (not the division above it or a department below it).
  const { data: types = [] } = useQuery({ queryKey: ['htl-types', 'all'], queryFn: async () => (await api.get<RoomType[]>('/hotel/room-types')).data, staleTime: 60_000 })
  const withRooms = new Set(types.map(t => t.entityId))
  const current = options.find(o => o.id === id) ?? options.find(o => withRooms.has(o.id))
    ?? options.find(o => o.modules.includes('hotel') && o.industry === 'Hotel') ?? options[0]
  const select = (v: string) => { setId(v); try { localStorage.setItem('erpos.hotel', v) } catch { /* ignore */ } }
  const picker = options.length > 1 ? <Select value={current?.id} onChange={select} style={{ minWidth: 220 }} options={options.map(o => ({ value: o.id, label: o.name }))} /> : null
  return { propertyId: current?.id, propertyName: current?.name, picker }
}

const ResTag = ({ s }: { s: ReservationStatus }) => <Tag color={RES_COLORS[s]}>{label(s)}</Tag>
const resColumns = (onOpen: (id: string) => void) => [
  { title: 'Guest', render: (_: unknown, r: ReservationListItem) => <a onClick={() => onOpen(r.id)}>{r.isVip && <CrownFilled style={{ color: '#d4b106', marginRight: 4 }} />}{r.guestName}</a> },
  { title: 'Res.', dataIndex: 'number' },
  { title: 'Rooms', dataIndex: 'rooms' },
  { title: 'Stay', render: (_: unknown, r: ReservationListItem) => `${dayjs(r.arrivalDate).format('DD MMM')} → ${dayjs(r.departureDate).format('DD MMM')} (${r.nights}n)` },
  { title: 'Status', render: (_: unknown, r: ReservationListItem) => <ResTag s={r.status} /> },
  { title: 'Balance', align: 'right' as const, render: (_: unknown, r: ReservationListItem) => amount(r.balance, 0) },
]

// ======================= Front desk =======================

export function FrontDeskPage() {
  const { propertyId, propertyName, picker } = useProperty()
  const { can } = useAuth()
  const { message, modal } = App.useApp()
  const qc = useQueryClient()
  const [open, setOpen] = useState<string | null>(null)
  const [booking, setBooking] = useState(false)
  const [from, setFrom] = useState(dayjs(pkToday()))
  const { data: fd, isLoading } = useQuery({ queryKey: ['htl-fd', propertyId], enabled: !!propertyId, refetchInterval: 60_000,
    queryFn: async () => (await api.get<FrontDesk>('/hotel/front-desk', { params: { entityId: propertyId } })).data })
  const { data: tape } = useQuery({ queryKey: ['htl-tape', propertyId, from.format('YYYY-MM-DD')], enabled: !!propertyId,
    queryFn: async () => (await api.get<TapeChart>('/hotel/tape-chart', { params: { entityId: propertyId, from: from.format('YYYY-MM-DD'), days: 14 } })).data })

  if (!propertyId) return <Alert type="info" showIcon title="No hotel property available. Enable the Hotel module on an entity and add room types." />

  const nightAudit = () => modal.confirm({
    title: `Run night audit for ${fmtDate(pkToday())}?`, content: 'Posts tonight\'s room charge to every in-house guest. Safe to run more than once.',
    onOk: async () => {
      try {
        const { data } = await api.post<{ reservationsCharged: number; roomRevenue: number }>('/hotel/night-audit', { entityId: propertyId, date: pkToday() })
        message.success(`Charged ${data.reservationsCharged} stay(s), room revenue ${amount(data.roomRevenue, 0)}`)
        await qc.invalidateQueries({ queryKey: ['htl-res'] })
      } catch (e) { message.error(errorMessage(e)) }
    },
  })

  return (
    <>
      <div className="page-header">
        <Space wrap><Typography.Title level={2}>Front desk</Typography.Title><Typography.Text type="secondary">{propertyName}</Typography.Text>{picker}</Space>
        <Space wrap>
          {can('hotel.reservations.checkout', propertyId) && <Button icon={<MoonOutlined />} onClick={nightAudit}>Night audit</Button>}
          {can('hotel.reservations.create', propertyId) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setBooking(true)}>New reservation</Button>}
        </Space>
      </div>
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {[['Occupancy', `${fd?.occupancyPercent ?? 0}%`], ['Occupied', `${fd?.occupied ?? 0} / ${fd?.totalRooms ?? 0}`], ['Vacant clean', fd?.vacantClean],
          ['Vacant dirty', fd?.vacantDirty], ['Out of order', fd?.outOfOrder], ['Arrivals', fd?.arrivals.length], ['Departures', fd?.departures.length]].map(([t, v]) => (
          <Col key={t as string} xs={12} sm={8} lg={24 / 7 > 3 ? 3 : 3}><Card loading={isLoading} size="small"><Statistic title={t as string} value={v as string | number} /></Card></Col>
        ))}
      </Row>
      <Card style={{ marginBottom: 16 }}>
        <Tabs items={[
          { key: 'arr', label: <Badge count={fd?.arrivals.length} size="small" offset={[10, 0]}>Arrivals</Badge>, children: <Table rowKey="id" size="small" pagination={false} dataSource={fd?.arrivals} columns={resColumns(setOpen)} scroll={{ x: 700 }} locale={{ emptyText: 'No arrivals pending' }} /> },
          { key: 'dep', label: <Badge count={fd?.departures.length} size="small" offset={[10, 0]}>Departures</Badge>, children: <Table rowKey="id" size="small" pagination={false} dataSource={fd?.departures} columns={resColumns(setOpen)} scroll={{ x: 700 }} locale={{ emptyText: 'No departures due' }} /> },
          { key: 'in', label: `In house (${fd?.inHouse.length ?? 0})`, children: <Table rowKey="id" size="small" pagination={false} dataSource={fd?.inHouse} columns={resColumns(setOpen)} scroll={{ x: 700 }} /> },
        ]} />
      </Card>
      <Card title="Room calendar" extra={<Space><Button onClick={() => setFrom(f => f.subtract(7, 'day'))}>‹</Button><DatePicker value={from} onChange={d => d && setFrom(d)} allowClear={false} format="DD MMM" /><Button onClick={() => setFrom(f => f.add(7, 'day'))}>›</Button></Space>}>
        {tape ? <Tape tape={tape} onOpen={setOpen} /> : <Empty />}
      </Card>
      {booking && <ReservationEditor propertyId={propertyId} onClose={() => setBooking(false)} onSaved={setOpen} />}
      {open && <ReservationDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function Tape({ tape, onOpen }: { tape: TapeChart; onOpen: (id: string) => void }) {
  const days = Array.from({ length: tape.days }, (_, i) => dayjs(tape.from).add(i, 'day'))
  const cellW = 64
  const blockStyle = (b: { from: string; to: string; status: ReservationStatus }) => {
    const start = Math.max(0, dayjs(b.from).diff(dayjs(tape.from), 'day'))
    const end = Math.min(tape.days, dayjs(b.to).diff(dayjs(tape.from), 'day'))
    return { left: start * cellW + 2, width: Math.max(1, end - start) * cellW - 4 }
  }
  const color: Record<ReservationStatus, string> = { Tentative: '#faad14', Confirmed: '#2f54eb', CheckedIn: '#389e0d', CheckedOut: '#8c8c8c', Cancelled: '#cf1322', NoShow: '#d4380d' }
  return (
    <div style={{ overflowX: 'auto' }}>
      <div style={{ minWidth: 140 + cellW * tape.days }}>
        <div style={{ display: 'flex', position: 'sticky', top: 0 }}>
          <div style={{ width: 140, flexShrink: 0 }} />
          {days.map(d => <div key={d.toString()} style={{ width: cellW, textAlign: 'center', fontSize: 11, opacity: 0.75, fontWeight: d.isSame(dayjs(pkToday()), 'day') ? 700 : 400 }}>{d.format('ddd')}<br />{d.format('DD MMM')}</div>)}
        </div>
        {tape.rooms.map(r => (
          <div key={r.roomId} style={{ display: 'flex', borderTop: '1px solid rgba(128,128,128,.2)', height: 34, alignItems: 'center' }}>
            <div style={{ width: 140, flexShrink: 0, fontSize: 12 }}><b>{r.number}</b> {r.roomTypeName} <Tag color={HK_COLORS[r.housekeeping]} style={{ fontSize: 10 }}>{r.housekeeping === 'OutOfOrder' ? 'OOO' : r.housekeeping[0]}</Tag></div>
            <div style={{ position: 'relative', height: 28, width: cellW * tape.days }}>
              {r.blocks.map(b => (
                <Tooltip key={b.reservationId} title={`${b.guestName} · ${b.number} · ${fmtDate(b.from)} → ${fmtDate(b.to)}`}>
                  <div onClick={() => onOpen(b.reservationId)} style={{ position: 'absolute', top: 2, height: 24, borderRadius: 6, background: color[b.status], color: '#fff', fontSize: 11, padding: '4px 6px', overflow: 'hidden', whiteSpace: 'nowrap', cursor: 'pointer', ...blockStyle(b) }}>
                    {b.isVip && '★ '}{b.guestName}
                  </div>
                </Tooltip>
              ))}
            </div>
          </div>
        ))}
        {tape.unassigned.length > 0 && <>
          <Divider titlePlacement="start" plain style={{ margin: '12px 0 4px' }}>Not yet assigned to a room</Divider>
          <Space wrap>{tape.unassigned.map(b => <Tag key={b.reservationId} color={RES_COLORS[b.status]} style={{ cursor: 'pointer' }} onClick={() => onOpen(b.reservationId)}>{b.guestName} · {dayjs(b.from).format('DD MMM')}–{dayjs(b.to).format('DD MMM')}</Tag>)}</Space>
        </>}
      </div>
    </div>
  )
}

// ======================= Reservations =======================

export function ReservationsPage() {
  const { propertyId, picker } = useProperty()
  const { can } = useAuth()
  const [status, setStatus] = useState<ReservationStatus | 'All'>('All')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)
  const [booking, setBooking] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['htl-res', propertyId, status, search, page], enabled: !!propertyId,
    queryFn: async () => (await api.get<PagedResult<ReservationListItem>>('/hotel/reservations', { params: { entityId: propertyId, status: status === 'All' ? undefined : status, search, page, pageSize: 25 } })).data })
  return (
    <>
      <div className="page-header">
        <Space wrap><Typography.Title level={2}>Reservations</Typography.Title>{picker}</Space>
        {propertyId && can('hotel.reservations.create', propertyId) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setBooking(true)}>New reservation</Button>}
      </div>
      <Card>
        <Space wrap style={{ marginBottom: 16 }}>
          <Segmented value={status} onChange={v => { setStatus(v as typeof status); setPage(1) }} options={['All', 'Confirmed', 'Tentative', 'CheckedIn', 'CheckedOut', 'Cancelled'].map(s => ({ value: s, label: label(s) }))} />
          <Input.Search allowClear placeholder="Guest, phone or number" onSearch={v => { setSearch(v); setPage(1) }} style={{ width: 240 }} />
        </Space>
        <Table<ReservationListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[...resColumns(setOpen), { title: 'Source', dataIndex: 'source', render: (s: string) => label(s) }]} />
      </Card>
      {booking && propertyId && <ReservationEditor propertyId={propertyId} onClose={() => setBooking(false)} onSaved={setOpen} />}
      {open && <ReservationDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function GuestPicker({ value, onChange }: { value?: string; onChange?: (id?: string, g?: Guest) => void }) {
  const [search, setSearch] = useState('')
  const { data = [] } = useQuery({ queryKey: ['htl-guests', search], enabled: search.length >= 2,
    queryFn: async () => (await api.get<Guest[]>('/hotel/guests', { params: { search } })).data })
  const chosen = data.find(g => g.id === value)
  return (
    <AutoComplete value={chosen ? chosen.fullName : search} onSearch={setSearch} placeholder="Search returning guest by name, phone, CNIC"
      onSelect={(id: string) => onChange?.(id, data.find(g => g.id === id))} onClear={() => onChange?.(undefined)} allowClear style={{ width: '100%' }}
      options={data.map(g => ({ value: g.id, label: <span>{g.isVip && '★ '}{g.fullName} <Typography.Text type="secondary">{g.phone} · {g.stays} stay(s)</Typography.Text></span> }))} />
  )
}

interface RoomDraft { roomTypeId: string; rate?: number }

function ReservationEditor({ propertyId, reservation, onClose, onSaved }: { propertyId: string; reservation?: Reservation; onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [guestId, setGuestId] = useState<string | undefined>(reservation?.guestId)
  const [range, setRange] = useState<[Dayjs, Dayjs]>(reservation ? [dayjs(reservation.arrivalDate), dayjs(reservation.departureDate)] : [dayjs(pkToday()), dayjs(pkToday()).add(1, 'day')])
  const [rooms, setRooms] = useState<RoomDraft[]>(reservation?.rooms.map(r => ({ roomTypeId: r.roomTypeId, rate: r.rate })) ?? [])
  const { data: avail } = useQuery({ queryKey: ['htl-avail', propertyId, range[0].format('YYYY-MM-DD'), range[1].format('YYYY-MM-DD')],
    queryFn: async () => (await api.get<Availability>('/hotel/availability', { params: { entityId: propertyId, from: range[0].format('YYYY-MM-DD'), to: range[1].format('YYYY-MM-DD') } })).data })
  const nights = range[1].diff(range[0], 'day')
  const total = rooms.reduce((s, r) => s + (r.rate ?? avail?.roomTypes.find(t => t.roomTypeId === r.roomTypeId)?.baseRate ?? 0), 0) * nights

  const save = async () => {
    const v = await form.validateFields()
    if (!rooms.length) { message.error('Add at least one room.'); return }
    setBusy(true)
    try {
      const body = {
        entityId: propertyId, guestId, newGuest: guestId ? null : { fullName: v.fullName, phone: v.phone, email: v.email, cnic: v.cnic, passportNo: v.passportNo, nationality: v.nationality, city: v.city, isVip: !!v.isVip },
        billToContactId: v.billToContactId, source: v.source, arrivalDate: range[0].format('YYYY-MM-DD'), departureDate: range[1].format('YYYY-MM-DD'),
        adults: v.adults, children: v.children ?? 0, tentative: !!v.tentative, externalReference: v.externalReference, notes: v.notes, rooms,
      }
      const res = reservation ? await api.put<Reservation>(`/hotel/reservations/${reservation.id}`, body) : await api.post<Reservation>('/hotel/reservations', body)
      message.success(reservation ? 'Reservation updated' : `Reservation ${res.data.number} confirmed`)
      await Promise.all(['htl-res', 'htl-fd', 'htl-tape'].map(k => qc.invalidateQueries({ queryKey: [k] })))
      onClose()
      onSaved(res.data.id)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Drawer open onClose={onClose} size={900} title={reservation ? `Edit ${reservation.number}` : 'New reservation'} extra={<Button type="primary" loading={busy} onClick={save}>{reservation ? 'Save' : 'Book'}</Button>}>
      <Form form={form} layout="vertical" initialValues={reservation ? { ...reservation } : { source: 'WalkIn', adults: 1, children: 0 }}>
        <Typography.Title level={5}>Guest</Typography.Title>
        <Row gutter={12}>
          <Col span={24}><Form.Item label="Returning guest"><GuestPicker value={guestId} onChange={id => setGuestId(id)} /></Form.Item></Col>
          {!guestId && <>
            <Col xs={24} md={10}><Form.Item name="fullName" label="New guest name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col xs={12} md={7}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col xs={12} md={7}><Form.Item name="cnic" label="CNIC"><Input placeholder="12345-1234567-1" /></Form.Item></Col>
            <Col xs={12} md={7}><Form.Item name="passportNo" label="Passport (foreigners)"><Input /></Form.Item></Col>
            <Col xs={12} md={6}><Form.Item name="nationality" label="Nationality"><Input placeholder="Pakistani" /></Form.Item></Col>
            <Col xs={12} md={6}><Form.Item name="city" label="City"><Input /></Form.Item></Col>
            <Col xs={12} md={5}><Form.Item name="isVip" valuePropName="checked" label=" "><Checkbox>VIP</Checkbox></Form.Item></Col>
          </>}
        </Row>
        <Typography.Title level={5}>Stay</Typography.Title>
        <Row gutter={12}>
          <Col xs={24} md={9}><Form.Item label={`Dates (${nights} night${nights === 1 ? '' : 's'})`} required><DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} format="DD MMM YYYY" style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={8} md={3}><Form.Item name="adults" label="Adults"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={8} md={3}><Form.Item name="children" label="Children"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={8} md={4}><Form.Item name="source" label="Source"><Select options={SOURCES.map(s => ({ value: s, label: label(s) }))} /></Form.Item></Col>
          <Col xs={24} md={5}><Form.Item name="externalReference" label="OTA / agent ref."><Input /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="billToContactId" label="Bill to company / agent" extra="Leave empty when the guest pays at checkout"><ContactSelect customers /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="tentative" valuePropName="checked" label=" "><Checkbox>Tentative (hold, not yet confirmed)</Checkbox></Form.Item></Col>
        </Row>
      </Form>
      <Typography.Title level={5}>Rooms</Typography.Title>
      <Table size="small" pagination={false} rowKey="roomTypeId" dataSource={avail?.roomTypes ?? []}
        columns={[
          { title: 'Room type', dataIndex: 'roomTypeName' },
          { title: 'Rack rate', align: 'right', render: (_, t) => amount(t.baseRate, 0) },
          { title: 'Free every night', align: 'right', render: (_, t) => {
            const taken = rooms.filter(r => r.roomTypeId === t.roomTypeId).length
            const reservedHere = reservation?.rooms.filter(r => r.roomTypeId === t.roomTypeId).length ?? 0
            const free = t.minAvailable + reservedHere - taken
            return <Tag color={free > 0 ? 'green' : 'red'}>{free} of {t.totalRooms}</Tag>
          } },
          { title: 'Book', render: (_, t) => (
            <Space>
              <Button size="small" onClick={() => setRooms(rs => [...rs, { roomTypeId: t.roomTypeId }])}>+ Add</Button>
              {rooms.some(r => r.roomTypeId === t.roomTypeId) && <Button size="small" onClick={() => setRooms(rs => { const i = rs.map(r => r.roomTypeId).lastIndexOf(t.roomTypeId); return rs.filter((_, j) => j !== i) })}>−</Button>}
            </Space>
          ) },
        ]} />
      {rooms.length > 0 && (
        <Table size="small" pagination={false} style={{ marginTop: 12 }} rowKey={(_, i) => String(i)} dataSource={rooms}
          columns={[
            { title: 'Booked room', render: (_, r) => avail?.roomTypes.find(t => t.roomTypeId === r.roomTypeId)?.roomTypeName },
            { title: 'Nightly rate (excl. tax)', render: (_, r, i) => <InputNumber min={0} value={r.rate ?? avail?.roomTypes.find(t => t.roomTypeId === r.roomTypeId)?.baseRate} onChange={v => setRooms(rs => rs.map((x, j) => (j === i ? { ...x, rate: v ?? undefined } : x)))} style={{ width: 160 }} /> },
          ]}
          summary={() => <Table.Summary.Row><Table.Summary.Cell index={0}><b>Room charges for {nights} night(s), before tax</b></Table.Summary.Cell><Table.Summary.Cell index={1}><b>{amount(total, 0)}</b></Table.Summary.Cell></Table.Summary.Row>} />
      )}
      <Form form={form} layout="vertical" style={{ marginTop: 16 }}><Form.Item name="notes" label="Notes / special requests"><Input.TextArea rows={2} /></Form.Item></Form>
    </Drawer>
  )
}

function ReservationDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [charging, setCharging] = useState(false)
  const [depositing, setDepositing] = useState(false)
  const [checkingOut, setCheckingOut] = useState(false)
  const [editing, setEditing] = useState(false)
  const key = ['htl-reservation', id]
  const { data: r } = useQuery({ queryKey: key, queryFn: async () => (await api.get<Reservation>(`/hotel/reservations/${id}`)).data })
  const { data: rooms = [] } = useQuery({ queryKey: ['htl-rooms', r?.entityId], enabled: !!r, queryFn: async () => (await api.get<Room[]>('/hotel/rooms', { params: { entityId: r!.entityId } })).data })
  const refresh = async () => { await Promise.all([key, ['htl-res'], ['htl-fd'], ['htl-tape'], ['htl-rooms']].map(k => qc.invalidateQueries({ queryKey: k }))) }
  const act = async (fn: () => Promise<unknown>, done: string) => { try { await fn(); message.success(done); await refresh() } catch (e) { message.error(errorMessage(e)) } }
  if (!r) return <Drawer open onClose={onClose} loading />
  const E = r.entityId
  const active = r.status === 'Tentative' || r.status === 'Confirmed'

  return (
    <Drawer open onClose={onClose} size={920}
      title={<Space wrap>{r.guest.isVip && <CrownFilled style={{ color: '#d4b106' }} />}{r.guest.fullName}<Tag>{r.number}</Tag><ResTag s={r.status} /></Space>}
      extra={
        <Space wrap>
          {active && can('hotel.reservations.edit', E) && <Button onClick={() => setEditing(true)}>Edit</Button>}
          {active && can('hotel.reservations.checkin', E) && <Button type="primary" icon={<LoginOutlined />} onClick={() => act(() => api.post(`/hotel/reservations/${id}/check-in`), 'Checked in')}>Check in</Button>}
          {(active || r.status === 'CheckedIn') && can('hotel.reservations.edit', E) && <Button onClick={() => setDepositing(true)}>Take deposit</Button>}
          {r.status === 'CheckedIn' && can('hotel.reservations.edit', E) && <Button onClick={() => setCharging(true)}>Post charge</Button>}
          {r.status === 'CheckedIn' && can('hotel.reservations.checkout', E) && <Button type="primary" icon={<LogoutOutlined />} onClick={() => setCheckingOut(true)}>Check out</Button>}
          {active && can('hotel.reservations.edit', E) && (
            <Popconfirm title="Cancel this reservation?" description="Refund any deposit separately from Finance." onConfirm={() => act(() => api.post(`/hotel/reservations/${id}/cancel`), 'Cancelled')}><Button danger>Cancel</Button></Popconfirm>
          )}
          {active && dayjs(r.arrivalDate).isBefore(dayjs(pkToday())) && can('hotel.reservations.edit', E) && <Button danger onClick={() => act(() => api.post(`/hotel/reservations/${id}/no-show`), 'Marked no-show')}>No-show</Button>}
          <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print folio</Button>
        </Space>
      }>
      <div className="print-area">
        <Descriptions size="small" bordered column={{ xs: 1, md: 3 }} style={{ marginBottom: 16 }}>
          <Descriptions.Item label="Stay">{fmtDate(r.arrivalDate)} → {fmtDate(r.departureDate)} ({r.nights} nights)</Descriptions.Item>
          <Descriptions.Item label="Guests">{r.adults} adult(s){r.children ? `, ${r.children} child(ren)` : ''}</Descriptions.Item>
          <Descriptions.Item label="Source">{label(r.source)}{r.externalReference && ` · ${r.externalReference}`}</Descriptions.Item>
          <Descriptions.Item label="Phone">{r.guest.phone ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="CNIC / passport">{r.guest.cnic ?? r.guest.passportNo ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Bill to">{r.billToName ?? 'Guest pays'}</Descriptions.Item>
          {r.invoiceNumber && <Descriptions.Item label="Invoice">{r.invoiceNumber}</Descriptions.Item>}
          {r.notes && <Descriptions.Item label="Notes" span={3}>{r.notes}</Descriptions.Item>}
        </Descriptions>
        <Typography.Title level={5}>Rooms</Typography.Title>
        <Table size="small" pagination={false} rowKey="id" dataSource={r.rooms} style={{ marginBottom: 16 }}
          columns={[
            { title: 'Type', dataIndex: 'roomTypeName' },
            { title: 'Rate / night', align: 'right', render: (_, x) => amount(x.rate, 0) },
            { title: 'Room', render: (_, x) => (r.status === 'CheckedOut' || r.status === 'Cancelled' || !can('hotel.reservations.edit', E)) ? (x.roomNumber ?? '—') : (
              <Select value={x.roomId} placeholder="Assign room" style={{ width: 200 }} onChange={v => act(() => api.post(`/hotel/reservations/${id}/assign-room`, { reservationRoomId: x.id, roomId: v }), 'Room assigned')}
                options={rooms.filter(rm => rm.roomTypeId === x.roomTypeId && rm.isActive && (!rm.occupied || rm.reservationId === r.id)).map(rm => ({
                  value: rm.id, label: <span>{rm.number} <Tag color={HK_COLORS[rm.housekeeping]} style={{ fontSize: 10 }}>{label(rm.housekeeping)}</Tag></span> }))} />
            ) },
          ]} />
        <Typography.Title level={5}>Folio</Typography.Title>
        <Table size="small" pagination={false} rowKey="id" dataSource={r.charges} locale={{ emptyText: 'No charges yet — room nights post at night audit' }}
          rowClassName={c => (c.isVoid ? 'ant-table-row-disabled' : '')}
          columns={[
            { title: 'Date', dataIndex: 'date', render: (d: string) => dayjs(d).format('DD MMM') },
            { title: 'Type', dataIndex: 'type', render: (t: FolioChargeType, c) => <Space><Tag>{t}</Tag>{c.isVoid && <Tag color="red">Void</Tag>}</Space> },
            { title: 'Description', render: (_, c) => <span style={{ textDecoration: c.isVoid ? 'line-through' : undefined }}>{c.description}{c.quantity !== 1 && ` × ${c.quantity}`}</span> },
            { title: 'Amount', align: 'right', render: (_, c) => amount(c.amount) },
            { title: 'Tax', align: 'right', render: (_, c) => c.taxAmount ? amount(c.taxAmount) : '' },
            { key: 'x', render: (_, c) => r.status === 'CheckedIn' && !c.isVoid && !c.fromStock && c.type !== 'Room' && can('hotel.reservations.checkout', E) &&
              <Button size="small" type="link" danger onClick={() => act(() => api.post(`/hotel/reservations/${id}/charges/${c.id}/void`), 'Charge voided')}>Void</Button> },
          ]} />
        <Row justify="end" style={{ marginTop: 12 }}>
          <Col xs={24} sm={10}>
            <Descriptions size="small" bordered column={1}>
              <Descriptions.Item label="Charges">{amount(r.chargesTotal)}</Descriptions.Item>
              <Descriptions.Item label="Sales tax">{amount(r.taxTotal)}</Descriptions.Item>
              <Descriptions.Item label="Deposits">− {amount(r.depositsTotal)}</Descriptions.Item>
              <Descriptions.Item label={<b>Balance</b>}><b>{amount(r.balance)}</b></Descriptions.Item>
              {r.status !== 'CheckedOut' && <Descriptions.Item label="Estimated room charges">{amount(r.estimatedStayTotal)} + tax</Descriptions.Item>}
            </Descriptions>
          </Col>
        </Row>
      </div>
      {charging && <ChargeModal reservation={r} onClose={() => setCharging(false)} onDone={refresh} />}
      {depositing && <DepositModal id={id} onClose={() => setDepositing(false)} onDone={refresh} />}
      {checkingOut && <CheckOutModal reservation={r} onClose={() => setCheckingOut(false)} onDone={refresh} />}
      {editing && <ReservationEditor propertyId={r.entityId} reservation={r} onClose={() => setEditing(false)} onSaved={() => void refresh()} />}
    </Drawer>
  )
}

function ChargeModal({ reservation, onClose, onDone }: { reservation: Reservation; onClose: () => void; onDone: () => Promise<void> }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const fromStock = Form.useWatch('fromStock', form)
  const save = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/hotel/reservations/${reservation.id}/charges`, { ...v, itemId: v.fromStock ? v.itemId : null, warehouseId: v.fromStock ? v.warehouseId : null })
      message.success('Charge posted to folio')
      await onDone()
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={`Post charge — ${reservation.guest.fullName}`} onCancel={onClose} onOk={save} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ type: 'Restaurant', quantity: 1 }}>
        <Row gutter={12}>
          <Col span={10}><Form.Item name="type" label="Outlet"><Select options={['Restaurant', 'Minibar', 'Laundry', 'Transport', 'Other'].map(t => ({ value: t, label: t }))} /></Form.Item></Col>
          <Col span={14}><Form.Item name="description" label="Description" rules={[{ required: true }]}><Input placeholder="Dinner — 2 covers" /></Form.Item></Col>
          <Col span={8}><Form.Item name="quantity" label="Qty" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="unitPrice" label="Price" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="taxRateId" label="Tax"><TaxRateSelect /></Form.Item></Col>
          <Col span={24}><Form.Item name="incomeAccountId" label="Income account" extra="Blank = revenue from services"><AccountSelect allowClear types={['Income']} /></Form.Item></Col>
          <Col span={24}><Form.Item name="fromStock" valuePropName="checked"><Checkbox>Item comes from stock (issued at average cost)</Checkbox></Form.Item></Col>
          {fromStock && <>
            <Col span={12}><Form.Item name="itemId" label="Item" rules={[{ required: true }]}><ItemSelect stockOnly /></Form.Item></Col>
            <Col span={12}><Form.Item name="warehouseId" label="From outlet store" rules={[{ required: true }]}><WarehouseSelect /></Form.Item></Col>
          </>}
        </Row>
      </Form>
    </Modal>
  )
}

function DepositModal({ id, onClose, onDone }: { id: string; onClose: () => void; onDone: () => Promise<void> }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const save = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/hotel/reservations/${id}/deposits`, { ...v, date: v.date.format('YYYY-MM-DD') })
      message.success('Deposit recorded')
      await onDone()
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title="Take deposit" onCancel={onClose} onOk={save} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ date: dayjs(pkToday()) }}>
        <Form.Item name="amount" label="Amount" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="bankAccountId" label="Received into" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item>
        <Form.Item name="date" label="Date"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
        <Form.Item name="reference" label="Card / transfer reference"><Input /></Form.Item>
      </Form>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>Held as a customer advance and applied to the invoice at checkout.</Typography.Paragraph>
    </Modal>
  )
}

function CheckOutModal({ reservation: r, onClose, onDone }: { reservation: Reservation; onClose: () => void; onDone: () => Promise<void> }) {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const date = Form.useWatch('date', form) as Dayjs | undefined
  // Nights still to be charged (night audit may not have run for every night).
  const charged = r.charges.filter(c => c.type === 'Room' && !c.isVoid).length
  const end = date ?? dayjs(pkToday())
  const stayNights = Math.max(1, end.diff(dayjs(r.arrivalDate), 'day'))
  const missingRoom = r.rooms.reduce((s, x) => s + x.rate, 0) * stayNights - r.charges.filter(c => c.type === 'Room' && !c.isVoid).reduce((s, c) => s + c.amount, 0)
  const estimate = r.balance + Math.max(0, missingRoom) * 1.16
  useEffect(() => { form.setFieldsValue({ amountPaid: Math.max(0, Math.round(estimate)) }) }, [estimate, form])
  const save = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      await api.post(`/hotel/reservations/${r.id}/check-out`, { date: v.date.format('YYYY-MM-DD'), bankAccountId: v.amountPaid > 0 ? v.bankAccountId : null, amountPaid: v.amountPaid || null, paymentReference: v.paymentReference })
      message.success('Checked out — invoice issued')
      await onDone()
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return (
    <Modal open title={`Check out ${r.guest.fullName}`} onCancel={onClose} onOk={save} okText="Check out & invoice" confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ date: dayjs(pkToday()) }}>
        <Form.Item name="date" label="Departure date" extra={`${stayNights} night(s) will be billed; ${charged} room-night charge(s) already on the folio.`}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
        <Alert type="info" showIcon style={{ marginBottom: 12 }} title={`Estimated balance due after deposits: ${amount(estimate, 0)}`}
          description={r.billToName ? `Billed to ${r.billToName} — any unpaid balance stays on their account.` : 'The exact total is computed on the invoice (tax by rate).'} />
        <Row gutter={12}>
          <Col span={12}><Form.Item name="amountPaid" label="Collected now"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={12}><Form.Item name="bankAccountId" label="Into" rules={[{ required: !r.billToName, message: 'Choose cash or bank' }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item></Col>
          <Col span={24}><Form.Item name="paymentReference" label="Card / cash receipt reference"><Input /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  )
}

// ======================= Housekeeping =======================

export function HousekeepingPage() {
  const { propertyId, propertyName, picker } = useProperty('hotel.housekeeping.view')
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [filter, setFilter] = useState<HousekeepingStatus | 'All'>('All')
  const { data = [], isLoading } = useQuery({ queryKey: ['htl-rooms', propertyId], enabled: !!propertyId, refetchInterval: 60_000,
    queryFn: async () => (await api.get<Room[]>('/hotel/rooms', { params: { entityId: propertyId } })).data })
  const setStatus = async (room: Room, status: HousekeepingStatus) => {
    try { await api.put(`/hotel/rooms/${room.id}/housekeeping`, { status }); await qc.invalidateQueries({ queryKey: ['htl-rooms'] }) } catch (e) { message.error(errorMessage(e)) }
  }
  const rows = data.filter(r => r.isActive && (filter === 'All' || r.housekeeping === filter))
  const editable = !!propertyId && can('hotel.housekeeping.edit', propertyId)
  return (
    <>
      <div className="page-header"><Space wrap><Typography.Title level={2}>Housekeeping</Typography.Title><Typography.Text type="secondary">{propertyName}</Typography.Text>{picker}</Space></div>
      <Segmented style={{ marginBottom: 16 }} value={filter} onChange={v => setFilter(v as typeof filter)}
        options={['All', 'Dirty', 'Clean', 'Inspected', 'OutOfOrder'].map(s => ({ value: s, label: `${label(s)} (${s === 'All' ? data.filter(r => r.isActive).length : data.filter(r => r.isActive && r.housekeeping === s).length})` }))} />
      {isLoading ? <Card loading /> : (
        <Row gutter={[12, 12]}>
          {rows.map(r => (
            <Col key={r.id} xs={12} sm={8} md={6} xl={4}>
              <Card size="small" title={<Space><b>{r.number}</b><Tag color={HK_COLORS[r.housekeeping]}>{label(r.housekeeping)}</Tag></Space>}
                styles={{ header: { borderBottom: `3px solid ${({ Clean: '#52c41a', Inspected: '#13c2c2', Dirty: '#fa8c16', OutOfOrder: '#f5222d' } as const)[r.housekeeping]}` } }}>
                <div style={{ fontSize: 12 }}>{r.roomTypeName}{r.floor && ` · Floor ${r.floor}`}</div>
                <div style={{ fontSize: 12, minHeight: 18 }}>{r.occupied ? <>Occupied — {r.guestName} (out {dayjs(r.departureDate).format('DD MMM')})</> : <Typography.Text type="secondary">Vacant</Typography.Text>}</div>
                {editable && (
                  <Space size={4} wrap style={{ marginTop: 8 }}>
                    {r.housekeeping !== 'Clean' && <Button size="small" onClick={() => setStatus(r, 'Clean')}>Clean</Button>}
                    {r.housekeeping !== 'Inspected' && <Button size="small" onClick={() => setStatus(r, 'Inspected')}>Inspected</Button>}
                    {r.housekeeping !== 'Dirty' && <Button size="small" onClick={() => setStatus(r, 'Dirty')}>Dirty</Button>}
                    {r.housekeeping !== 'OutOfOrder' && <Button size="small" danger onClick={() => setStatus(r, 'OutOfOrder')}>OOO</Button>}
                  </Space>
                )}
              </Card>
            </Col>
          ))}
          {rows.length === 0 && <Col span={24}><Empty /></Col>}
        </Row>
      )}
    </>
  )
}

// ======================= Setup =======================

export function HotelSetupPage() {
  const { propertyId, propertyName, picker } = useProperty('hotel.rooms.view')
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: types = [] } = useQuery({ queryKey: ['htl-types', propertyId], enabled: !!propertyId, queryFn: async () => (await api.get<RoomType[]>('/hotel/room-types', { params: { entityId: propertyId } })).data })
  const { data: rooms = [] } = useQuery({ queryKey: ['htl-rooms', propertyId], enabled: !!propertyId, queryFn: async () => (await api.get<Room[]>('/hotel/rooms', { params: { entityId: propertyId } })).data })
  const [editingType, setEditingType] = useState<RoomType | 'new' | null>(null)
  const [editingRoom, setEditingRoom] = useState<Room | 'new' | null>(null)
  const [tForm] = Form.useForm()
  const [rForm] = Form.useForm()
  const saveType = async () => {
    const v = await tForm.validateFields()
    try {
      if (editingType === 'new') await api.post('/hotel/room-types', { ...v, entityId: propertyId })
      else if (editingType) await api.put(`/hotel/room-types/${editingType.id}`, { ...v, entityId: propertyId })
      setEditingType(null)
      await qc.invalidateQueries({ queryKey: ['htl-types'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const saveRoom = async () => {
    const v = await rForm.validateFields()
    try {
      if (editingRoom === 'new') await api.post('/hotel/rooms', { ...v, entityId: propertyId })
      else if (editingRoom) await api.put(`/hotel/rooms/${editingRoom.id}`, { ...v, entityId: propertyId })
      setEditingRoom(null)
      await qc.invalidateQueries({ queryKey: ['htl-rooms'] })
      await qc.invalidateQueries({ queryKey: ['htl-types'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  if (!propertyId) return <Alert type="info" showIcon title="No hotel property available." />
  const editable = can('hotel.rooms.create', propertyId)
  return (
    <>
      <div className="page-header"><Space wrap><Typography.Title level={2}>Rooms & rates</Typography.Title><Typography.Text type="secondary">{propertyName}</Typography.Text>{picker}</Space></div>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Card title="Room types" extra={editable && <Button icon={<PlusOutlined />} onClick={() => setEditingType('new')}>Add type</Button>}>
            <Table<RoomType> rowKey="id" size="small" pagination={false} dataSource={types} onRow={t => ({ onClick: () => editable && setEditingType(t), style: { cursor: 'pointer' } })}
              columns={[
                { title: 'Type', render: (_, t) => <><b>{t.name}</b> <Typography.Text type="secondary">{t.code}</Typography.Text>{!t.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}</> },
                { title: 'Rack rate', align: 'right', render: (_, t) => amount(t.baseRate, 0) },
                { title: 'Max', render: (_, t) => `${t.maxAdults}A ${t.maxChildren}C` },
                { title: 'Rooms', dataIndex: 'roomCount' },
              ]} />
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title="Rooms" extra={editable && types.length > 0 && <Button icon={<PlusOutlined />} onClick={() => setEditingRoom('new')}>Add room</Button>}>
            <Table<Room> rowKey="id" size="small" pagination={false} dataSource={rooms} onRow={r => ({ onClick: () => editable && setEditingRoom(r), style: { cursor: 'pointer' } })}
              columns={[{ title: 'Room', dataIndex: 'number' }, { title: 'Type', dataIndex: 'roomTypeName' }, { title: 'Floor', dataIndex: 'floor' },
                { title: 'Status', render: (_, r) => <Space><Tag color={HK_COLORS[r.housekeeping]}>{label(r.housekeeping)}</Tag>{r.occupied && <Tag color="green">Occupied</Tag>}{!r.isActive && <Tag>Inactive</Tag>}</Space> }]} />
          </Card>
        </Col>
      </Row>
      <Modal open={!!editingType} title={editingType === 'new' ? 'New room type' : 'Edit room type'} onCancel={() => setEditingType(null)} onOk={saveType} destroyOnHidden>
        <Form form={tForm} layout="vertical" preserve={false} initialValues={editingType === 'new' ? { maxAdults: 2, maxChildren: 1, isActive: true } : editingType ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input placeholder="Deluxe King" /></Form.Item></Col>
            <Col span={8}><Form.Item name="baseRate" label="Rack rate / night" rules={[{ required: true }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="maxAdults" label="Max adults"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}><Form.Item name="maxChildren" label="Max children"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="taxRateId" label="Sales tax on room nights"><TaxRateSelect /></Form.Item></Col>
            <Col span={12}><Form.Item name="incomeAccountId" label="Income account" extra="Blank = revenue from services"><AccountSelect allowClear types={['Income']} /></Form.Item></Col>
            <Col span={24}><Form.Item name="description" label="Description"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
      <Modal open={!!editingRoom} title={editingRoom === 'new' ? 'New room' : 'Edit room'} onCancel={() => setEditingRoom(null)} onOk={saveRoom} destroyOnHidden>
        <Form form={rForm} layout="vertical" preserve={false} initialValues={editingRoom === 'new' ? { isActive: true } : editingRoom ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="number" label="Room no." rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="floor" label="Floor"><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="isActive" valuePropName="checked" label=" "><Checkbox>Active</Checkbox></Form.Item></Col>
            <Col span={24}><Form.Item name="roomTypeId" label="Room type" rules={[{ required: true }]}><Select options={types.map(t => ({ value: t.id, label: t.name }))} /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

// ======================= Reports =======================

export function HotelReportsPage() {
  const { propertyId, propertyName, picker } = useProperty('hotel.reports.view')
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs(pkToday()).startOf('month'), dayjs(pkToday())])
  const { data, isLoading, error } = useQuery({ queryKey: ['htl-report', propertyId, range[0].format('YYYY-MM-DD'), range[1].format('YYYY-MM-DD')], enabled: !!propertyId, retry: false,
    queryFn: async () => (await api.get<HotelReport>('/hotel/report', { params: { entityId: propertyId, from: range[0].format('YYYY-MM-DD'), to: range[1].format('YYYY-MM-DD') } })).data })
  const max = useMemo(() => Math.max(1, ...(data?.days.map(d => d.roomRevenue) ?? [1])), [data])
  if (!propertyId) return <Alert type="info" showIcon title="No hotel property you can report on." />
  return (
    <>
      <div className="page-header">
        <Space wrap><Typography.Title level={2}>Hotel performance</Typography.Title><Typography.Text type="secondary">{propertyName}</Typography.Text>{picker}</Space>
        <DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} format="DD MMM YYYY" />
      </div>
      {error ? <Alert type="warning" showIcon title={errorMessage(error)} /> : <>
        <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
          {[['Occupancy', `${data?.occupancy ?? 0}%`], ['Room nights sold', `${data?.sold ?? 0} / ${data?.available ?? 0}`], ['Room revenue', amount(data?.roomRevenue, 0)],
            ['ADR (avg daily rate)', amount(data?.adr, 0)], ['RevPAR', amount(data?.revPar, 0)]].map(([t, v]) => (
            <Col key={t} xs={12} md={8} xl={24 / 5 > 4 ? 4 : 4}><Card loading={isLoading} size="small"><Statistic title={t} value={v} /></Card></Col>
          ))}
        </Row>
        <Row gutter={[16, 16]}>
          <Col xs={24} xl={16}>
            <Card title="By night">
              <Table size="small" rowKey="date" dataSource={data?.days} pagination={{ pageSize: 31, hideOnSinglePage: true }} scroll={{ x: 600 }}
                columns={[
                  { title: 'Night', dataIndex: 'date', render: (d: string) => dayjs(d).format('ddd DD MMM') },
                  { title: 'Sold', render: (_, d) => `${d.sold}/${d.available}` },
                  { title: 'Occupancy', render: (_, d) => <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}><div style={{ width: 60, height: 6, background: 'rgba(128,128,128,.2)', borderRadius: 3 }}><div style={{ width: `${d.occupancy}%`, height: 6, background: '#2f54eb', borderRadius: 3 }} /></div>{d.occupancy}%</div> },
                  { title: 'Room revenue', align: 'right', render: (_, d) => <div style={{ display: 'flex', alignItems: 'center', gap: 6, justifyContent: 'flex-end' }}><div style={{ width: `${(d.roomRevenue / max) * 60}px`, height: 6, background: '#52c41a', borderRadius: 3 }} />{amount(d.roomRevenue, 0)}</div> },
                  { title: 'ADR', align: 'right', render: (_, d) => amount(d.adr, 0) },
                  { title: 'RevPAR', align: 'right', render: (_, d) => amount(d.revPar, 0) },
                ]} />
            </Card>
          </Col>
          <Col xs={24} xl={8}>
            <Card title="Revenue by outlet (excl. tax)">
              {Object.entries(data?.revenueByType ?? {}).sort((a, b) => b[1] - a[1]).map(([k, v]) => (
                <div key={k} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid rgba(128,128,128,.15)' }}><Tag>{k}</Tag><b>{amount(v, 0)}</b></div>
              ))}
              {Object.keys(data?.revenueByType ?? {}).length === 0 && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />}
            </Card>
          </Col>
        </Row>
      </>}
    </>
  )
}
