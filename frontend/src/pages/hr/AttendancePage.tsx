import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Col, DatePicker, Form, Input, Modal, Popconfirm, Row, Segmented, Select, Space, Table, Tabs, Tag,
  TimePicker, Typography,
} from 'antd'
import { DeleteOutlined, PlusOutlined, SaveOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { fmtDate, toIsoDate, type AttendanceRow, type AttendanceStatus, type Holiday } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'

const MARKS: { value: AttendanceStatus | 'none'; label: string }[] = [
  { value: 'none', label: '—' }, { value: 'Present', label: 'P' }, { value: 'Absent', label: 'A' },
  { value: 'Late', label: 'L' }, { value: 'HalfDay', label: '½' },
]

export default function AttendancePage() {
  const { can } = useAuth()
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Attendance</Typography.Title></div>
      <Tabs items={[
        { key: 'daily', label: 'Daily sheet', children: <DailySheet /> },
        { key: 'holidays', label: 'Holidays', children: <Holidays canManage={can(P.hrSettings)} /> },
      ]} />
    </>
  )
}

type Draft = Record<string, { status?: AttendanceStatus; checkIn?: string; checkOut?: string; remarks?: string }>

function DailySheet() {
  const { me } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const firstEntity = me?.entities.find(e => e.permissions.includes(P.attendanceView))?.id
  const [entityId, setEntityId] = useState<string | undefined>(firstEntity)
  const [date, setDate] = useState<Dayjs>(dayjs())
  const [draft, setDraft] = useState<Draft>({})
  const [saving, setSaving] = useState(false)

  const key = ['attendance', entityId, date.format('YYYY-MM-DD')]
  const { data = [], isFetching } = useQuery({
    queryKey: key, enabled: !!entityId,
    queryFn: async () => (await api.get<AttendanceRow[]>('/hr/attendance', { params: { entityId, date: toIsoDate(date) } })).data,
  })
  useEffect(() => setDraft(Object.fromEntries(data.map(r => [r.employeeId, { status: r.status, checkIn: r.checkIn, checkOut: r.checkOut, remarks: r.remarks }]))), [data])

  const changed = useMemo(() => data.filter(r => {
    const d = draft[r.employeeId]
    return d && (d.status !== r.status || d.checkIn !== r.checkIn || d.checkOut !== r.checkOut || (d.remarks ?? '') !== (r.remarks ?? ''))
  }), [data, draft])

  const set = (id: string, patch: Draft[string]) => setDraft(d => ({ ...d, [id]: { ...d[id], ...patch } }))
  const markAll = (status: AttendanceStatus) => setDraft(d => Object.fromEntries(data.map(r => [r.employeeId, r.dayType ? d[r.employeeId] : { ...d[r.employeeId], status }])))

  const save = async () => {
    setSaving(true)
    try {
      await api.put('/hr/attendance', {
        date: toIsoDate(date),
        entries: changed.map(r => ({ employeeId: r.employeeId, ...draft[r.employeeId] })),
      })
      message.success(`Saved ${changed.length} change(s)`)
      await qc.invalidateQueries({ queryKey: key })
    } catch (e) { message.error(errorMessage(e)) } finally { setSaving(false) }
  }

  const time = (v?: string) => (v ? dayjs(v, 'HH:mm:ss') : null)

  return (
    <Card>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }} align="middle">
        <Col xs={24} md={9}><EntityPicker value={entityId} onChange={setEntityId} permission={P.attendanceView} /></Col>
        <Col xs={12} md={5}><DatePicker value={date} onChange={v => v && setDate(v)} allowClear={false} format="ddd DD MMM YYYY" style={{ width: '100%' }} disabledDate={d => d.isAfter(dayjs(), 'day')} /></Col>
        <Col xs={12} md={10} style={{ textAlign: 'right' }}>
          <Space wrap>
            <Button onClick={() => markAll('Present')}>All present</Button>
            <Button type="primary" icon={<SaveOutlined />} disabled={!changed.length} loading={saving} onClick={save}>Save {changed.length || ''}</Button>
          </Space>
        </Col>
      </Row>
      <Alert type="info" showIcon style={{ marginBottom: 12 }} title="Only exceptions need marking. Unmarked working days count as present; absences and half days reduce pay." />
      <Table<AttendanceRow> rowKey="employeeId" loading={isFetching} dataSource={data} pagination={false} scroll={{ x: 900 }}
        rowClassName={r => (changed.includes(r) ? 'ant-table-row-selected' : '')}
        columns={[
          { title: 'Employee', render: (_, r) => <><b>{r.fullName}</b><div><Typography.Text type="secondary">{r.employeeCode}{r.department && ` · ${r.department}`}</Typography.Text></div></> },
          { title: 'Day', dataIndex: 'dayType', render: (t?: string) => t ? <Tag color={t.startsWith('On leave') ? 'purple' : 'cyan'}>{t}</Tag> : null },
          {
            title: 'Mark', render: (_, r) => (
              <Segmented size="small" value={draft[r.employeeId]?.status ?? 'none'} options={MARKS}
                onChange={v => set(r.employeeId, { status: v === 'none' ? undefined : (v as AttendanceStatus) })} />
            ),
          },
          { title: 'In', render: (_, r) => <TimePicker size="small" format="HH:mm" value={time(draft[r.employeeId]?.checkIn)} onChange={v => set(r.employeeId, { checkIn: v?.format('HH:mm:ss') })} /> },
          { title: 'Out', render: (_, r) => <TimePicker size="small" format="HH:mm" value={time(draft[r.employeeId]?.checkOut)} onChange={v => set(r.employeeId, { checkOut: v?.format('HH:mm:ss') })} /> },
          { title: 'Remarks', render: (_, r) => <Input size="small" value={draft[r.employeeId]?.remarks} onChange={e => set(r.employeeId, { remarks: e.target.value })} /> },
        ]} />
    </Card>
  )
}

function Holidays({ canManage }: { canManage: boolean }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [year, setYear] = useState(dayjs().year())
  const [adding, setAdding] = useState(false)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['holidays', year], queryFn: async () => (await api.get<Holiday[]>('/hr/attendance/holidays', { params: { year } })).data })

  const add = async () => {
    const v = await form.validateFields()
    try {
      await api.post('/hr/attendance/holidays', { date: toIsoDate(v.date), name: v.name, entityId: v.entityId })
      message.success('Holiday added')
      setAdding(false)
      await qc.invalidateQueries({ queryKey: ['holidays'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const remove = async (id: string) => {
    try { await api.delete(`/hr/attendance/holidays/${id}`); await qc.invalidateQueries({ queryKey: ['holidays'] }) } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Card title={<Space>Holidays <Select value={year} onChange={setYear} options={[-1, 0, 1].map(o => ({ value: dayjs().year() + o, label: dayjs().year() + o }))} /></Space>}
      extra={canManage && <Button icon={<PlusOutlined />} onClick={() => setAdding(true)}>Add holiday</Button>}>
      <Typography.Paragraph type="secondary">Gazetted and religious holidays (Eid dates change each year — add them once announced). A holiday on an entity also applies to its sub-entities.</Typography.Paragraph>
      <Table<Holiday> rowKey="id" loading={isLoading} dataSource={data} pagination={false}
        columns={[
          { title: 'Date', dataIndex: 'date', render: (d: string) => `${fmtDate(d)} (${dayjs(d).format('ddd')})` },
          { title: 'Holiday', dataIndex: 'name' },
          { title: 'Applies to', dataIndex: 'entityName', render: (n?: string) => n ?? 'Whole organization' },
          canManage ? { key: 'x', align: 'right' as const, render: (_: unknown, h: Holiday) => <Popconfirm title="Remove holiday?" onConfirm={() => remove(h.id)}><Button size="small" danger icon={<DeleteOutlined />} /></Popconfirm> } : {},
        ]} />
      <Modal open={adding} title="Add holiday" onCancel={() => setAdding(false)} onOk={add} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false}>
          <Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item>
          <Form.Item name="name" label="Name" rules={[{ required: true }]}><Input placeholder="e.g. Quaid-e-Azam Day" /></Form.Item>
          <Form.Item name="entityId" label="Only for entity" extra="Leave empty for the whole organization."><EntityPicker permission={P.hrSettings} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}
