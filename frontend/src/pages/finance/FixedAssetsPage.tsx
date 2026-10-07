import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Card, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Row, Segmented, Select, Space, Statistic, Switch, Table, Tabs, Tag, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import { amount, type AssetCategory, type AssetListItem, type AssetSchedule, type AssetStatus, type DepreciationRun, type FixedAsset } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, ContactSelect } from '../../components/FinancePickers'
import ExportButton from '../../components/ExportButton'
import Attachments from '../../components/Attachments'

const d8 = (d?: Dayjs | null) => d?.format('YYYY-MM-DD')
const STATUS_COLORS: Record<AssetStatus, string> = { Active: 'blue', FullyDepreciated: 'default', Disposed: 'red' }
const STATUS_TEXT: Record<AssetStatus, string> = { Active: 'In use', FullyDepreciated: 'Fully depreciated', Disposed: 'Disposed' }
const useCategories = () => useQuery({ queryKey: ['fin-asset-cats'], queryFn: async () => (await api.get<AssetCategory[]>('/finance/assets/categories')).data })
const refresh = (qc: ReturnType<typeof useQueryClient>) => Promise.all(['fin-assets', 'fin-asset', 'fin-dep-runs', 'fin-asset-schedule', 'fin-asset-cats'].map(k => qc.invalidateQueries({ queryKey: [k] })))

export default function FixedAssetsPage() {
  const { can } = useAuth()
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Fixed assets</Typography.Title></div>
      <Card>
        <Tabs items={[
          { key: 'register', label: 'Asset register', children: <Register /> },
          { key: 'depreciation', label: 'Depreciation', children: <Depreciation /> },
          { key: 'schedule', label: 'Schedule', children: <Schedule /> },
          ...(can('finance.settings.manage') ? [{ key: 'categories', label: 'Categories', children: <Categories /> }] : []),
        ]} />
      </Card>
    </>
  )
}

function Register() {
  const { can } = useAuth()
  const [status, setStatus] = useState<AssetStatus | 'All'>('Active')
  const [open, setOpen] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const { data, isFetching } = useQuery({ queryKey: ['fin-assets', status], queryFn: async () => (await api.get<AssetListItem[]>('/finance/assets', { params: { status: status === 'All' ? undefined : status } })).data })
  const totals = (data ?? []).reduce((t, a) => ({ cost: t.cost + a.cost, dep: t.dep + a.accumulatedDepreciation, nbv: t.nbv + a.bookValue }), { cost: 0, dep: 0, nbv: 0 })
  return (
    <>
      <Space wrap style={{ marginBottom: 16 }}>
        <Segmented value={status} onChange={v => setStatus(v as typeof status)} options={[{ value: 'Active', label: 'In use' }, { value: 'FullyDepreciated', label: 'Fully depreciated' }, { value: 'Disposed', label: 'Disposed' }, { value: 'All', label: 'All' }]} />
        {can('finance.assets.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setAdding(true)}>Register asset</Button>}
        <ExportButton<AssetListItem> fileName="fixed-asset-register" title="Fixed asset register" rows={data ?? []}
          columns={[{ title: 'Code', value: a => a.code }, { title: 'Asset', value: a => a.name, width: 34 }, { title: 'Category', value: a => a.categoryName, width: 24 },
            { title: 'Entity', value: a => a.entityName, width: 22 }, { title: 'Location', value: a => a.location }, { title: 'Acquired', value: a => a.acquisitionDate, type: 'date' },
            { title: 'Cost', value: a => a.cost, type: 'money' }, { title: 'Accumulated depreciation', value: a => a.accumulatedDepreciation, type: 'money' },
            { title: 'Book value', value: a => a.bookValue, type: 'money' }, { title: 'Depreciated to', value: a => a.depreciatedThrough }, { title: 'Status', value: a => STATUS_TEXT[a.status] }]} />
      </Space>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={8}><Statistic title="Cost" value={amount(totals.cost, 0)} /></Col>
        <Col xs={8}><Statistic title="Accumulated depreciation" value={amount(totals.dep, 0)} /></Col>
        <Col xs={8}><Statistic title="Book value" value={amount(totals.nbv, 0)} /></Col>
      </Row>
      <Table<AssetListItem> rowKey="id" size="small" loading={isFetching} dataSource={data} scroll={{ x: 900 }} onRow={a => ({ onClick: () => setOpen(a.id), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Asset', render: (_, a) => <><b>{a.code}</b> {a.name}<div><Typography.Text type="secondary">{a.categoryName}{a.location ? ` · ${a.location}` : ''}</Typography.Text></div></> },
          { title: 'Acquired', dataIndex: 'acquisitionDate', render: fmtDate }, { title: 'Cost', align: 'right', render: (_, a) => amount(a.cost, 0) },
          { title: 'Depreciation', align: 'right', render: (_, a) => amount(a.accumulatedDepreciation, 0) }, { title: 'Book value', align: 'right', render: (_, a) => <b>{amount(a.bookValue, 0)}</b> },
          { title: 'Depreciated to', render: (_, a) => a.depreciatedThrough ?? '—' }, { title: 'Status', dataIndex: 'status', render: (s: AssetStatus) => <Tag color={STATUS_COLORS[s]}>{STATUS_TEXT[s]}</Tag> }]} />
      {adding && <RegisterModal onClose={() => setAdding(false)} onSaved={setOpen} />}
      {open && <AssetDrawer id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function RegisterModal({ onClose, onSaved }: { onClose: () => void; onSaved: (id: string) => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me } = useAuth()
  const { data: cats = [] } = useCategories()
  const [form] = Form.useForm()
  const acquisition = Form.useWatch('acquisition', form) ?? 'PaidNow'
  const catId = Form.useWatch('categoryId', form)
  const cat = cats.find(c => c.id === catId)
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const a = (await api.post<FixedAsset>('/finance/assets', { ...v, acquisitionDate: d8(v.acquisitionDate), depreciationStart: d8(v.depreciationStart), depreciatedThrough: d8(v.depreciatedThrough),
        salvageValue: v.salvageValue ?? 0, openingAccumulatedDepreciation: v.openingAccumulatedDepreciation ?? 0, usefulLifeMonths: v.usefulLifeMonths || undefined })).data
      message.success(`${a.code} registered`); await refresh(qc); onClose(); onSaved(a.id)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open width={720} title="Register asset" onCancel={onClose} onOk={ok} okText="Register">
      <Form form={form} layout="vertical" initialValues={{ acquisition: 'PaidNow', acquisitionDate: dayjs(), entityId: me?.entities.find(e => e.permissions.includes('finance.assets.create'))?.id }}>
        <Row gutter={12}>
          <Col span={14}><Form.Item name="name" label="Description" rules={[{ required: true }]}><Input placeholder="Toyota Hilux, LES-1234" /></Form.Item></Col>
          <Col span={10}><Form.Item name="categoryId" label="Category" rules={[{ required: true }]}><Select options={cats.filter(c => c.isActive).map(c => ({ value: c.id, label: c.name }))} /></Form.Item></Col>
          <Col span={8}><Form.Item name="cost" label="Cost" rules={[{ required: true }]}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="salvageValue" label="Salvage value" tooltip="What it should be worth at the end of its life"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="usefulLifeMonths" label="Life (months)" extra={cat ? `Category: ${cat.usefulLifeMonths}` : undefined}><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={8}><Form.Item name="acquisitionDate" label="Acquired on" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col span={8}><Form.Item name="depreciationStart" label="Depreciate from" extra="Blank = month acquired"><DatePicker picker="month" style={{ width: '100%' }} format="MMM YYYY" /></Form.Item></Col>
          <Col span={8}><Form.Item name="serialNo" label="Serial / registration no."><Input /></Form.Item></Col>
          <Col span={12}><Form.Item name="location" label="Location / custodian"><Input /></Form.Item></Col>
          <Col span={12}><Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission="finance.assets.create" /></Form.Item></Col>
        </Row>
        <Form.Item name="acquisition" label="How was it acquired?">
          <Segmented block options={[{ value: 'PaidNow', label: 'Paid now' }, { value: 'OnCredit', label: 'On a vendor bill' }, { value: 'AlreadyInBooks', label: 'Already in the books' }]} />
        </Form.Item>
        {acquisition === 'PaidNow' && <Form.Item name="paidFromAccountId" label="Paid from" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item>}
        {acquisition === 'OnCredit' && <Form.Item name="vendorId" label="Vendor" extra="An approved bill is posted to the asset account" rules={[{ required: true }]}><ContactSelect vendors /></Form.Item>}
        {acquisition === 'AlreadyInBooks' && <Row gutter={12}>
          <Col span={24}><Alert type="info" showIcon style={{ marginBottom: 12 }} title="For assets you already own: nothing is posted (cost and past depreciation are in your opening balances)." /></Col>
          <Col span={12}><Form.Item name="openingAccumulatedDepreciation" label="Depreciation already charged"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
          <Col span={12}><Form.Item name="depreciatedThrough" label="…up to the end of"><DatePicker picker="month" style={{ width: '100%' }} format="MMM YYYY" /></Form.Item></Col>
        </Row>}
      </Form>
    </Modal>
  )
}

function AssetDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [disposing, setDisposing] = useState(false)
  const [form] = Form.useForm()
  const { data: a, isLoading } = useQuery({ queryKey: ['fin-asset', id], queryFn: async () => (await api.get<FixedAsset>(`/finance/assets/${id}`)).data })
  const dispose = async () => {
    const v = await form.validateFields()
    try { await api.post(`/finance/assets/${id}/dispose`, { ...v, date: d8(v.date), proceeds: v.proceeds ?? 0 }); message.success('Asset disposed'); await refresh(qc); setDisposing(false) }
    catch (e) { message.error(errorMessage(e)) }
  }
  const proceeds = Form.useWatch('proceeds', form) ?? 0
  return (
    <Drawer open onClose={onClose} size={720} loading={isLoading} title={a && <Space>{a.code} · {a.name}<Tag color={STATUS_COLORS[a.status]}>{STATUS_TEXT[a.status]}</Tag></Space>}
      extra={a && a.status !== 'Disposed' && can('finance.assets.edit') && <Button danger onClick={() => { form.resetFields(); setDisposing(true) }}>Sell / scrap</Button>}>
      {a && <>
        <Row gutter={16} style={{ marginBottom: 16 }}>
          <Col span={6}><Statistic title="Cost" value={amount(a.cost, 0)} /></Col>
          <Col span={6}><Statistic title="Depreciation" value={amount(a.accumulatedDepreciation, 0)} /></Col>
          <Col span={6}><Statistic title="Book value" value={amount(a.bookValue, 0)} /></Col>
          <Col span={6}><Statistic title="Monthly charge" value={amount(a.monthlyCharge, 0)} /></Col>
        </Row>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} bordered items={[
          { label: 'Category', children: a.categoryName }, { label: 'Entity', children: a.entityName },
          { label: 'Acquired', children: fmtDate(a.acquisitionDate) }, { label: 'Depreciating from', children: dayjs(a.depreciationStart).format('MMM YYYY') },
          { label: 'Method', children: a.method === 'StraightLine' ? `Straight line over ${a.usefulLifeMonths} months` : `Reducing balance ${+(a.reducingRate * 100).toFixed(2)}% a year` },
          { label: 'Salvage value', children: amount(a.salvageValue, 0) }, { label: 'Depreciated to', children: a.depreciatedThrough ?? 'not yet' },
          { label: 'Serial / reg. no.', children: a.serialNo ?? '—' }, { label: 'Location', children: a.location ?? '—', span: 'filled' as const },
          ...(a.disposalDate ? [{ label: 'Disposed', children: `${fmtDate(a.disposalDate)} for ${amount(a.disposalProceeds, 0)}`, span: 'filled' as const }] : []),
        ]} />
        <Typography.Title level={5} style={{ marginTop: 24 }}>Depreciation charged</Typography.Title>
        <Table size="small" rowKey={r => `${r.year}-${r.month}`} pagination={false} dataSource={a.history} locale={{ emptyText: 'None yet' }}
          columns={[{ title: 'Run', render: (_, r) => dayjs(new Date(r.year, r.month - 1, 1)).format('MMMM YYYY') }, { title: 'Months', dataIndex: 'months' },
            { title: 'Amount', align: 'right', render: (_, r) => amount(r.amount) }]} />
        <div style={{ marginTop: 24 }}><Attachments recordType="asset" recordId={a.id} canEdit={can('finance.assets.edit', a.entityId)} /></div>
        <Modal open={disposing} title={`Sell or scrap ${a.code}`} onCancel={() => setDisposing(false)} onOk={dispose} okText="Dispose" okButtonProps={{ danger: true }} forceRender>
          <Form form={form} layout="vertical" initialValues={{ date: dayjs(), proceeds: 0 }}>
            <Row gutter={12}>
              <Col span={12}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
              <Col span={12}><Form.Item name="proceeds" label="Sale proceeds" extra="0 if scrapped"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
              {proceeds > 0 && <Col span={24}><Form.Item name="receivedIntoAccountId" label="Received into" rules={[{ required: true }]}><AccountSelect subTypes={['Bank', 'Cash']} /></Form.Item></Col>}
              <Col span={24}><Form.Item name="reason" label="Reason"><Input /></Form.Item></Col>
            </Row>
          </Form>
          <Alert type={proceeds - a.bookValue >= 0 ? 'success' : 'warning'} showIcon
            title={`Book value ${amount(a.bookValue, 0)} → ${proceeds - a.bookValue >= 0 ? 'gain' : 'loss'} of ${amount(Math.abs(proceeds - a.bookValue), 0)} on disposal.`} />
        </Modal>
      </>}
    </Drawer>
  )
}

function Depreciation() {
  const qc = useQueryClient()
  const { can, me } = useAuth()
  const { message, modal } = App.useApp()
  const [month, setMonth] = useState<Dayjs>(dayjs().subtract(1, 'month'))
  const [entityId, setEntityId] = useState<string | undefined>(me?.entities.find(e => e.permissions.includes('finance.assets.depreciate'))?.id)
  const { data, isFetching } = useQuery({ queryKey: ['fin-dep-runs'], queryFn: async () => (await api.get<DepreciationRun[]>('/finance/assets/depreciation')).data })
  const runIt = () => modal.confirm({
    title: `Post depreciation for ${month.format('MMMM YYYY')}?`, content: 'Every asset in use is charged up to this month (missed months are caught up) in one journal.',
    onOk: async () => {
      try { const r = (await api.post<DepreciationRun>('/finance/assets/depreciation', { entityId, year: month.year(), month: month.month() + 1 })).data; message.success(`${amount(r.total, 0)} posted (${r.journalNumber})`); await refresh(qc) }
      catch (e) { message.error(errorMessage(e)) }
    },
  })
  return (
    <>
      {can('finance.assets.depreciate') && <Space wrap style={{ marginBottom: 16 }}>
        <DatePicker picker="month" value={month} onChange={d => d && setMonth(d)} allowClear={false} format="MMMM YYYY" disabledDate={d => d.isAfter(dayjs(), 'month')} />
        <div style={{ width: 260 }}><EntityPicker permission="finance.assets.depreciate" value={entityId} onChange={setEntityId} /></div>
        <Button type="primary" onClick={runIt} disabled={!entityId}>Run depreciation</Button>
      </Space>}
      <Table<DepreciationRun> rowKey="id" size="small" loading={isFetching} dataSource={data} pagination={false} locale={{ emptyText: 'No depreciation posted yet' }}
        expandable={{ expandedRowRender: r => <Table size="small" rowKey="assetId" pagination={false} dataSource={r.lines}
          columns={[{ title: 'Asset', render: (_, l) => `${l.assetCode} ${l.assetName}` }, { title: 'Months', dataIndex: 'months' }, { title: 'Amount', align: 'right', render: (_, l) => amount(l.amount) }]} /> }}
        columns={[{ title: 'Month', render: (_, r) => dayjs(new Date(r.year, r.month - 1, 1)).format('MMMM YYYY') }, { title: 'Assets', dataIndex: 'assets' },
          { title: 'Journal', dataIndex: 'journalNumber' }, { title: 'Charge', align: 'right', render: (_, r) => amount(r.total) }]} />
    </>
  )
}

function Schedule() {
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => {
    const now = dayjs()
    return [now.month() >= 6 ? now.month(6).startOf('month') : now.subtract(1, 'year').month(6).startOf('month'), now.endOf('month')]
  })
  const { data, isFetching } = useQuery({ queryKey: ['fin-asset-schedule', d8(range[0]), d8(range[1])],
    queryFn: async () => (await api.get<AssetSchedule>('/finance/assets/schedule', { params: { from: d8(range[0]), to: d8(range[1]) } })).data })
  const n = (v: number) => amount(v, 0)
  return (
    <>
      <Space style={{ marginBottom: 12 }}><DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} format="DD MMM YYYY" allowClear={false} />
        <Typography.Text type="secondary">Property, plant and equipment movement (IAS 16 note)</Typography.Text></Space>
      <Table size="small" rowKey="category" loading={isFetching} dataSource={data ? [...data.rows, data.total] : []} pagination={false} scroll={{ x: 1100 }}
        rowClassName={r => (r.category === 'Total' ? 'ant-table-row-selected' : '')}
        columns={[{ title: 'Category', dataIndex: 'category', render: (c: string) => (c === 'Total' ? <b>{c}</b> : c) },
          { title: 'Cost b/f', align: 'right', render: (_, r) => n(r.openingCost) }, { title: '+ Additions', align: 'right', render: (_, r) => n(r.additions) },
          { title: '− Disposals', align: 'right', render: (_, r) => n(r.disposals) }, { title: 'Cost c/f', align: 'right', render: (_, r) => <b>{n(r.closingCost)}</b> },
          { title: 'Depr. b/f', align: 'right', render: (_, r) => n(r.openingDepreciation) }, { title: '+ Charge', align: 'right', render: (_, r) => n(r.charge) },
          { title: '− On disposals', align: 'right', render: (_, r) => n(r.depreciationOnDisposals) }, { title: 'Depr. c/f', align: 'right', render: (_, r) => <b>{n(r.closingDepreciation)}</b> },
          { title: 'Book value', align: 'right', render: (_, r) => <b>{n(r.closingBookValue)}</b> }]} />
    </>
  )
}

function Categories() {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isFetching } = useCategories()
  const [editing, setEditing] = useState<AssetCategory | 'new' | null>(null)
  const [form] = Form.useForm()
  const method = Form.useWatch('method', form)
  const open = (c: AssetCategory | 'new') => { setEditing(c); form.resetFields(); form.setFieldsValue(c === 'new' ? { method: 'StraightLine', isActive: true, usefulLifeMonths: 60 } : { ...c, reducingRate: +(c.reducingRate * 100).toFixed(4) }) }
  const ok = async () => {
    const v = await form.validateFields()
    try {
      const body = { ...v, reducingRate: (v.reducingRate ?? 0) / 100, usefulLifeMonths: v.usefulLifeMonths ?? 0 }
      if (editing && editing !== 'new') await api.put(`/finance/assets/categories/${editing.id}`, body); else await api.post('/finance/assets/categories', body)
      message.success('Category saved'); await refresh(qc); setEditing(null)
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <>
      <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => open('new')}>Add category</Button>
      <Table size="small" rowKey="id" loading={isFetching} dataSource={data} pagination={false} onRow={c => ({ onClick: () => open(c), style: { cursor: 'pointer' } })}
        columns={[{ title: 'Category', render: (_, c) => <><b>{c.code}</b> {c.name}{!c.isActive && <Tag style={{ marginLeft: 6 }}>Inactive</Tag>}</> },
          { title: 'Method', render: (_, c) => c.method === 'StraightLine' ? `Straight line, ${c.usefulLifeMonths} months (${+(1200 / c.usefulLifeMonths).toFixed(1)}% a year)` : `Reducing balance ${+(c.reducingRate * 100).toFixed(2)}%` },
          { title: 'Assets in use', dataIndex: 'assets' }]} />
      <Modal open={!!editing} title={editing === 'new' ? 'Add asset category' : 'Edit asset category'} onCancel={() => setEditing(null)} onOk={ok} forceRender>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="method" label="Method"><Segmented block options={[{ value: 'StraightLine', label: 'Straight line' }, { value: 'ReducingBalance', label: 'Reducing balance' }]} /></Form.Item></Col>
            {method === 'ReducingBalance'
              ? <Col span={12}><Form.Item name="reducingRate" label="Rate per year (%)" rules={[{ required: true }]} extra="Tax rates: buildings 10%, plant 15%, computers 30%"><InputNumber min={1} max={99} style={{ width: '100%' }} /></Form.Item></Col>
              : <Col span={12}><Form.Item name="usefulLifeMonths" label="Useful life (months)" rules={[{ required: true }]}><InputNumber min={1} max={1200} style={{ width: '100%' }} /></Form.Item></Col>}
            <Col span={12}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
            <Col span={8}><Form.Item name="assetAccountId" label="Asset account" extra="Blank = 1510"><AccountSelect types={['Asset']} allowClear /></Form.Item></Col>
            <Col span={8}><Form.Item name="accumulatedAccountId" label="Accumulated" extra="Blank = 1520"><AccountSelect types={['Asset']} allowClear /></Form.Item></Col>
            <Col span={8}><Form.Item name="expenseAccountId" label="Expense" extra="Blank = 6700"><AccountSelect types={['Expense']} allowClear /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}
