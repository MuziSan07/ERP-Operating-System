import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, App, Button, Card, Checkbox, Col, DatePicker, Descriptions, Drawer, Form, Input, InputNumber, Modal, Row, Segmented, Select, Space, Statistic,
  Switch, Table, Tabs, Tag, Typography,
} from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import { amount } from '../../api/finance'
import { fmtDate } from '../../api/hr'
import {
  MOVE_LABEL, qty, type BatchRow, type Item, type ItemCategory, type MovementRow, type StockRow, type StockTransaction, type StockTransactionType,
  type Valuation, type Warehouse,
} from '../../api/inventory'
import { useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, TaxRateSelect } from '../../components/FinancePickers'
import { JournalView } from '../finance/LedgerPages'
import ExportButton from '../../components/ExportButton'

/* eslint-disable react-refresh/only-export-components */
export const useItems = () => useQuery({ queryKey: ['inv-items'], queryFn: async () => (await api.get<Item[]>('/inventory/items')).data })
export const useWarehouses = () => useQuery({ queryKey: ['inv-warehouses'], queryFn: async () => (await api.get<Warehouse[]>('/inventory/warehouses')).data })

export function ItemSelect({ value, onChange, stockOnly, style }: { value?: string; onChange?: (v: string) => void; stockOnly?: boolean; style?: React.CSSProperties }) {
  const { data = [] } = useItems()
  return <Select value={value} onChange={onChange} showSearch={{ optionFilterProp: 'label' }} placeholder="Item" style={{ width: '100%', ...style }} popupMatchSelectWidth={false}
    options={data.filter(i => i.isActive && (!stockOnly || i.type === 'Stock')).map(i => ({ value: i.id, label: `${i.code} ${i.name} (${i.unit})` }))} />
}

export function WarehouseSelect({ value, onChange, permission, exclude }: { value?: string; onChange?: (v: string) => void; permission?: string; exclude?: string }) {
  const { data = [] } = useWarehouses()
  const { can } = useAuth()
  return <Select value={value} onChange={onChange} placeholder="Warehouse" style={{ width: '100%' }}
    options={data.filter(w => w.isActive && w.id !== exclude && (!permission || can(permission, w.entityId))).map(w => ({ value: w.id, label: `${w.name} · ${w.entityName}` }))} />
}

// ======================= Items & warehouses =======================

export function InventorySetupPage() {
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Items & warehouses</Typography.Title></div>
      <Tabs items={[
        { key: 'items', label: 'Items', children: <Items /> },
        { key: 'warehouses', label: 'Warehouses', children: <Warehouses /> },
        { key: 'categories', label: 'Categories', children: <Categories /> },
      ]} />
    </>
  )
}

function Items() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isLoading } = useItems()
  const { data: categories = [] } = useQuery({ queryKey: ['inv-categories'], queryFn: async () => (await api.get<ItemCategory[]>('/inventory/categories')).data })
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<Item | 'new' | null>(null)
  const [form] = Form.useForm()
  const type = Form.useWatch('type', form)
  const batches = Form.useWatch('trackBatches', form)

  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/inventory/items', v)
      else if (editing) await api.put(`/inventory/items/${editing.id}`, v)
      message.success('Item saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['inv-items'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const rows = data.filter(i => !search || `${i.code} ${i.name} ${i.barcode ?? ''}`.toLowerCase().includes(search.toLowerCase()))

  return (
    <Card extra={<Space>
      <Input.Search allowClear placeholder="Search code, name, barcode" onSearch={setSearch} style={{ width: 240 }} />
      {can('inventory.items.create') && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New item</Button>}
    </Space>}>
      <Table<Item> rowKey="id" loading={isLoading} dataSource={rows} scroll={{ x: 900 }}
        onRow={i => ({ onClick: () => can('inventory.items.edit') && setEditing(i), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Code', dataIndex: 'code', width: 110 },
          { title: 'Item', render: (_, i) => <>{i.name} {!i.isActive && <Tag>Inactive</Tag>}</> },
          { title: 'Category', dataIndex: 'categoryName' },
          { title: 'Type', render: (_, i) => <Space>{i.type === 'Service' ? <Tag color="purple">Service</Tag> : <Tag>Stock</Tag>}{i.trackBatches && <Tag color="blue">Batch</Tag>}{i.trackExpiry && <Tag color="orange">Expiry</Tag>}</Space> },
          { title: 'On hand', align: 'right', render: (_, i) => i.type === 'Stock' ? <>{qty(i.onHand)} {i.unit}{i.reorderLevel > 0 && i.onHand <= i.reorderLevel && <Tag color="red" style={{ marginLeft: 6 }}>Reorder</Tag>}</> : '—' },
          { title: 'Value', align: 'right', render: (_, i) => i.type === 'Stock' ? amount(i.value) : '' },
        ]} />
      <Modal open={!!editing} width={680} title={editing === 'new' ? 'New item' : 'Edit item'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { type: 'Stock', unit: 'pcs', reorderLevel: 0, isActive: true } : editing ?? {}}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code / SKU" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="type" label="Type"><Select options={[{ value: 'Stock', label: 'Stock item' }, { value: 'Service', label: 'Service (no stock)' }]} /></Form.Item></Col>
            <Col span={8}><Form.Item name="unit" label="Unit" rules={[{ required: true }]}><Input placeholder="pcs, kg, box, bag" /></Form.Item></Col>
            <Col span={8}><Form.Item name="categoryId" label="Category"><Select allowClear options={categories.map(c => ({ value: c.id, label: c.name }))} /></Form.Item></Col>
            {type === 'Stock' && <>
              <Col span={8}><Form.Item name="reorderLevel" label="Reorder level"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={8}><Form.Item name="standardCost" label="Standard cost" extra="Used when no average exists"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></Col>
              <Col span={8}><Form.Item name="barcode" label="Barcode"><Input /></Form.Item></Col>
              <Col span={24}>
                <Form.Item name="trackBatches" valuePropName="checked" noStyle><Checkbox>Track batch numbers</Checkbox></Form.Item>
                <Form.Item name="trackExpiry" valuePropName="checked" noStyle><Checkbox disabled={!batches}>Track expiry dates (first-expiry-first-out, blocks expired stock)</Checkbox></Form.Item>
              </Col>
            </>}
            <Col span={12} style={{ marginTop: 12 }}><Form.Item name="inventoryAccountId" label={type === 'Service' ? 'Expense account on receipt' : 'Inventory account'} extra="Blank = default from accounting setup"><AccountSelect allowClear types={type === 'Service' ? ['Expense'] : ['Asset']} /></Form.Item></Col>
            <Col span={12} style={{ marginTop: 12 }}><Form.Item name="consumptionAccountId" label="Consumption / cost account" extra="Charged when stock is issued"><AccountSelect allowClear types={['Expense']} /></Form.Item></Col>
            <Col span={12}><Form.Item name="purchaseTaxRateId" label="Default purchase tax"><TaxRateSelect /></Form.Item></Col>
            <Col span={12} style={{ paddingTop: 30 }}><Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </Card>
  )
}

function Warehouses() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data = [], isLoading } = useWarehouses()
  const [editing, setEditing] = useState<Warehouse | 'new' | null>(null)
  const [form] = Form.useForm()
  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/inventory/warehouses', v)
      else if (editing) await api.put(`/inventory/warehouses/${editing.id}`, v)
      message.success('Warehouse saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['inv-warehouses'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Card extra={can('inventory.warehouses.create') && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add warehouse</Button>}>
      <Table<Warehouse> rowKey="id" loading={isLoading} dataSource={data} pagination={false}
        onRow={w => ({ onClick: () => can('inventory.warehouses.edit', w.entityId) && setEditing(w), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Warehouse', render: (_, w) => <><b>{w.name}</b> <Typography.Text type="secondary">{w.code}</Typography.Text></> },
          { title: 'Branch', dataIndex: 'entityName' },
          { title: 'Address', dataIndex: 'address' },
          { title: 'Stock value', align: 'right', render: (_, w) => amount(w.stockValue) },
          { title: '', render: (_, w) => !w.isActive && <Tag>Inactive</Tag> },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'New warehouse' : 'Edit warehouse'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? { isActive: true } : editing ?? {}}>
          <Form.Item name="entityId" label="Branch / entity" rules={[{ required: true }]}><EntityPicker permission="inventory.warehouses.create" /></Form.Item>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input placeholder="Main store, Kitchen store, Depot…" /></Form.Item></Col>
          </Row>
          <Form.Item name="address" label="Address"><Input /></Form.Item>
          <Form.Item name="isActive" valuePropName="checked"><Checkbox>Active</Checkbox></Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}

function Categories() {
  const qc = useQueryClient()
  const { can } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['inv-categories'], queryFn: async () => (await api.get<ItemCategory[]>('/inventory/categories')).data })
  const add = async (v: { code: string; name: string }) => {
    try { await api.post('/inventory/categories', v); form.resetFields(); await qc.invalidateQueries({ queryKey: ['inv-categories'] }) } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Card>
      {can('inventory.items.create') && (
        <Form form={form} layout="inline" onFinish={add} style={{ marginBottom: 12, rowGap: 8 }}>
          <Form.Item name="code" rules={[{ required: true }]}><Input placeholder="Code" style={{ width: 110 }} /></Form.Item>
          <Form.Item name="name" rules={[{ required: true }]}><Input placeholder="Name, e.g. Kitchen, Medicines, Spare parts" /></Form.Item>
          <Button htmlType="submit" icon={<PlusOutlined />}>Add</Button>
        </Form>
      )}
      <Table size="small" rowKey="id" loading={isLoading} dataSource={data} pagination={false} columns={[{ title: 'Code', dataIndex: 'code' }, { title: 'Name', dataIndex: 'name' }]} />
    </Card>
  )
}

// ======================= Stock overview =======================

export function StockPage() {
  const { can } = useAuth()
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Stock</Typography.Title></div>
      <Tabs destroyOnHidden items={[
        { key: 'onhand', label: 'On hand', children: <OnHand /> },
        { key: 'batches', label: 'Batches & expiry', children: <Batches /> },
        { key: 'moves', label: 'Movements', children: <Movements /> },
        ...(can('inventory.stock.view') ? [{ key: 'valuation', label: 'Valuation', children: <ValuationTab /> }] : []),
      ]} />
    </>
  )
}

function OnHand() {
  const [warehouseId, setWarehouseId] = useState<string>()
  const [low, setLow] = useState(false)
  const { data = [], isLoading } = useQuery({
    queryKey: ['inv-stock', warehouseId, low],
    queryFn: async () => (await api.get<StockRow[]>('/inventory/stock', { params: { warehouseId, belowReorder: low } })).data,
  })
  return (
    <Card>
      <Space wrap style={{ marginBottom: 16 }}>
        <div style={{ width: 280 }}><WarehouseSelect value={warehouseId} onChange={setWarehouseId} /></div>
        {warehouseId && <Button onClick={() => setWarehouseId(undefined)}>All warehouses</Button>}
        <ExportButton<StockRow> fileName="stock-on-hand" title="Stock on hand" rows={data}
          columns={[{ title: 'Item code', value: r => r.itemCode }, { title: 'Item', value: r => r.itemName, width: 32 }, { title: 'Category', value: r => r.category },
            { title: 'Warehouse', value: r => r.warehouseName, width: 24 }, { title: 'Quantity', value: r => r.quantity, type: 'number' }, { title: 'Unit', value: r => r.unit },
            { title: 'Average cost', value: r => r.averageCost, type: 'number' }, { title: 'Value', value: r => r.value, type: 'money' },
            { title: 'Reorder level', value: r => r.reorderLevel || null, type: 'number' }, { title: 'Below reorder', value: r => (r.belowReorder ? 'Yes' : '') }]} />
        <Space><Switch checked={low} onChange={setLow} />At or below reorder level</Space>
      </Space>
      <Table<StockRow> rowKey={r => `${r.itemId}-${r.warehouseId}`} loading={isLoading} dataSource={data} scroll={{ x: 800 }}
        columns={[
          { title: 'Item', render: (_, r) => <><b>{r.itemCode}</b> {r.itemName}</> },
          { title: 'Category', dataIndex: 'category' },
          { title: 'Warehouse', dataIndex: 'warehouseName' },
          { title: 'Quantity', align: 'right', render: (_, r) => <>{qty(r.quantity)} {r.unit} {r.belowReorder && <Tag color="red">Reorder</Tag>}</> },
          { title: 'Avg cost', align: 'right', render: (_, r) => amount(r.averageCost) },
          { title: 'Value', align: 'right', render: (_, r) => <b>{amount(r.value)}</b> },
        ]}
        summary={rows => <Table.Summary.Row><Table.Summary.Cell index={0} colSpan={5}><b>Total</b></Table.Summary.Cell><Table.Summary.Cell index={5} align="right"><b>{amount(rows.reduce((s, r) => s + r.value, 0))}</b></Table.Summary.Cell></Table.Summary.Row>} />
    </Card>
  )
}

function Batches() {
  const [days, setDays] = useState<number | null>(90)
  const { data = [], isLoading } = useQuery({
    queryKey: ['inv-batches', days],
    queryFn: async () => (await api.get<BatchRow[]>('/inventory/batches', { params: { expiringWithinDays: days ?? undefined } })).data,
  })
  return (
    <Card>
      <Space style={{ marginBottom: 16 }}>Show batches expiring within <InputNumber min={0} value={days} onChange={setDays} style={{ width: 90 }} /> days (blank = all)</Space>
      <Table<BatchRow> rowKey={r => `${r.batchId}-${r.warehouseId}`} loading={isLoading} dataSource={data} scroll={{ x: 700 }}
        columns={[
          { title: 'Item', render: (_, r) => <><b>{r.itemCode}</b> {r.itemName}</> },
          { title: 'Warehouse', dataIndex: 'warehouseName' },
          { title: 'Batch', dataIndex: 'batchNo' },
          { title: 'Expiry', render: (_, r) => r.expiryDate ? fmtDate(r.expiryDate) : '—' },
          { title: 'Status', render: (_, r) => r.daysToExpiry == null ? null : r.daysToExpiry < 0 ? <Tag color="red">Expired {-r.daysToExpiry}d ago</Tag> : r.daysToExpiry <= 30 ? <Tag color="orange">{r.daysToExpiry} days left</Tag> : <Tag>{r.daysToExpiry} days left</Tag> },
          { title: 'Quantity', dataIndex: 'quantity', align: 'right', render: qty },
        ]} />
    </Card>
  )
}

function Movements() {
  const [itemId, setItemId] = useState<string>()
  const { data = [], isLoading } = useQuery({ queryKey: ['inv-moves', itemId], queryFn: async () => (await api.get<MovementRow[]>('/inventory/movements', { params: { itemId } })).data })
  return (
    <Card>
      <div style={{ maxWidth: 360, marginBottom: 16 }}><ItemSelect value={itemId} onChange={setItemId} stockOnly /></div>
      <Table<MovementRow> rowKey={(_, i) => String(i)} loading={isLoading} dataSource={data} size="small" scroll={{ x: 1000 }}
        columns={[
          { title: 'Date', dataIndex: 'date', render: fmtDate },
          { title: 'Type', dataIndex: 'type', render: (t: MovementRow['type']) => <Tag color={t.endsWith('In') || t === 'Receipt' || t === 'Opening' ? 'green' : 'orange'}>{MOVE_LABEL[t]}</Tag> },
          { title: 'Ref', dataIndex: 'reference' },
          { title: 'Item', render: (_, r) => `${r.itemCode} ${r.itemName}` },
          { title: 'Warehouse', dataIndex: 'warehouseName' },
          { title: 'Batch', dataIndex: 'batchNo' },
          { title: 'Qty', dataIndex: 'quantity', align: 'right', render: (v: number) => <span style={{ color: v < 0 ? '#cf1322' : '#3f8600' }}>{qty(v)}</span> },
          { title: 'Unit cost', dataIndex: 'unitCost', align: 'right', render: (v: number) => amount(v) },
          { title: 'Value', dataIndex: 'value', align: 'right', render: (v: number) => amount(v) },
          { title: 'Qty after', dataIndex: 'quantityAfter', align: 'right', render: qty },
          { title: 'Avg after', dataIndex: 'averageCostAfter', align: 'right', render: (v: number) => amount(v) },
        ]} />
    </Card>
  )
}

function ValuationTab() {
  const { data, isLoading, error } = useQuery({ queryKey: ['inv-valuation'], retry: false, queryFn: async () => (await api.get<Valuation>('/inventory/valuation')).data })
  if (error) return <Alert type="info" showIcon title="The organization-wide valuation needs stock view rights at the top entity." />
  return (
    <>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={24} md={8}><Card loading={isLoading}><Statistic title={`Stock ledger value · ${data?.currency ?? ''}`} value={amount(data?.stockValue)} /></Card></Col>
        <Col xs={24} md={8}><Card loading={isLoading}><Statistic title="Inventory accounts (general ledger)" value={amount(data?.ledgerValue)} /></Card></Col>
        <Col xs={24} md={8}><Card loading={isLoading}><Statistic title="Difference" value={amount(data?.difference)} styles={{ content: { color: data && Math.abs(data.difference) > 0.005 ? '#cf1322' : '#3f8600' } }} /></Card></Col>
      </Row>
      {data && Math.abs(data.difference) > 0.005 && <Alert type="warning" showIcon style={{ marginBottom: 16 }} title="Stock and ledger disagree — usually a manual journal posted to an inventory account. Correct it with a journal or stock adjustment." />}
      <Card><Table<StockRow> rowKey={r => `${r.itemId}-${r.warehouseId}`} dataSource={data?.rows} size="small" columns={[
        { title: 'Item', render: (_, r) => `${r.itemCode} ${r.itemName}` }, { title: 'Warehouse', dataIndex: 'warehouseName' },
        { title: 'Qty', align: 'right', render: (_, r) => `${qty(r.quantity)} ${r.unit}` }, { title: 'Avg cost', align: 'right', render: (_, r) => amount(r.averageCost) },
        { title: 'Value', align: 'right', render: (_, r) => amount(r.value) }]} /></Card>
    </>
  )
}

// ======================= Stock transactions =======================

const TX_LABEL: Record<StockTransactionType, string> = { Issue: 'Issue / consumption', Transfer: 'Transfer', Adjustment: 'Adjustment / count', Opening: 'Opening stock' }
const TX_PERM: Record<StockTransactionType, string> = { Issue: 'inventory.stock.issue', Transfer: 'inventory.stock.transfer', Adjustment: 'inventory.stock.adjust', Opening: 'inventory.stock.adjust' }

export function StockTransactionsPage() {
  const { can } = useAuth()
  const [type, setType] = useState<StockTransactionType | 'All'>('All')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState<StockTransactionType | null>(null)
  const [viewing, setViewing] = useState<StockTransaction | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['inv-tx', type, page],
    queryFn: async () => (await api.get<PagedResult<StockTransaction>>('/inventory/transactions', { params: { type: type === 'All' ? undefined : type, page, pageSize: 25 } })).data,
  })
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Stock transactions</Typography.Title>
        <Space wrap>
          {(['Issue', 'Transfer', 'Adjustment', 'Opening'] as StockTransactionType[]).filter(t => can(TX_PERM[t])).map(t => (
            <Button key={t} type={t === 'Issue' ? 'primary' : 'default'} icon={<PlusOutlined />} onClick={() => setCreating(t)}>{TX_LABEL[t]}</Button>
          ))}
        </Space>
      </div>
      <Card>
        <Segmented style={{ marginBottom: 16 }} value={type} onChange={v => { setType(v as typeof type); setPage(1) }}
          options={[{ value: 'All', label: 'All' }, ...(['Issue', 'Transfer', 'Adjustment', 'Opening'] as const).map(t => ({ value: t, label: t }))]} />
        <Table<StockTransaction> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          onRow={t => ({ onClick: () => setViewing(t), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Number', dataIndex: 'number' },
            { title: 'Type', dataIndex: 'type', render: (t: StockTransactionType) => <Tag>{t}</Tag> },
            { title: 'Date', dataIndex: 'date', render: fmtDate },
            { title: 'Warehouse', render: (_, t) => t.toWarehouseName ? `${t.warehouseName} → ${t.toWarehouseName}` : t.warehouseName },
            { title: 'Items', render: (_, t) => t.lines.map(l => l.itemCode).join(', ') },
            { title: 'Value', align: 'right', render: (_, t) => amount(t.totalValue) },
          ]} />
      </Card>
      {creating && <StockTransactionModal type={creating} onClose={() => setCreating(null)} />}
      {viewing && <StockTransactionView tx={viewing} onClose={() => setViewing(null)} />}
    </>
  )
}

function StockTransactionView({ tx, onClose }: { tx: StockTransaction; onClose: () => void }) {
  const { can } = useAuth()
  const [journal, setJournal] = useState(false)
  return (
    <Drawer open onClose={onClose} size={760} title={<Space>{tx.number}<Tag>{tx.type}</Tag></Space>}
      extra={tx.journalEntryId && can('finance.journals.view') && <Button onClick={() => setJournal(true)}>Ledger entry</Button>}>
      <Descriptions size="small" bordered column={{ xs: 1, md: 2 }} style={{ marginBottom: 16 }}>
        <Descriptions.Item label="Date">{fmtDate(tx.date)}</Descriptions.Item>
        <Descriptions.Item label="Warehouse">{tx.toWarehouseName ? `${tx.warehouseName} → ${tx.toWarehouseName}` : tx.warehouseName}</Descriptions.Item>
        {tx.accountName && <Descriptions.Item label="Account">{tx.accountName}</Descriptions.Item>}
        {tx.reference && <Descriptions.Item label="Reference">{tx.reference}</Descriptions.Item>}
        <Descriptions.Item label="By">{tx.createdByName}</Descriptions.Item>
        {tx.notes && <Descriptions.Item label="Notes" span={2}>{tx.notes}</Descriptions.Item>}
      </Descriptions>
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={tx.lines}
        columns={[
          { title: 'Item', render: (_, l) => `${l.itemCode} ${l.itemName}` },
          { title: 'Batch', render: (_, l) => l.batchNo ? `${l.batchNo}${l.expiryDate ? ` (exp ${fmtDate(l.expiryDate)})` : ''}` : '' },
          { title: 'Qty', align: 'right', render: (_, l) => `${qty(l.quantity)} ${l.unit}` },
          { title: 'Unit cost', align: 'right', render: (_, l) => amount(l.unitCost) },
          { title: 'Value', align: 'right', render: (_, l) => amount(l.value) },
        ]} />
      {journal && tx.journalEntryId && <JournalView id={tx.journalEntryId} onClose={() => setJournal(false)} />}
    </Drawer>
  )
}

interface TxLine { itemId?: string; quantity: number; batchNo?: string; expiryDate?: Dayjs | null; unitCost?: number; notes?: string }

function StockTransactionModal({ type, onClose }: { type: StockTransactionType; onClose: () => void }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { data: items = [] } = useItems()
  const [form] = Form.useForm()
  const [busy, setBusy] = useState(false)
  const [lines, setLines] = useState<TxLine[]>([{ quantity: 1 }])
  const fromId = Form.useWatch('warehouseId', form) as string | undefined
  const set = (i: number, p: Partial<TxLine>) => setLines(ls => ls.map((l, j) => (j === i ? { ...l, ...p } : l)))
  const needsCost = type === 'Opening' || type === 'Adjustment'
  const itemOf = (id?: string) => items.find(i => i.id === id)

  const save = async () => {
    const v = await form.validateFields()
    if (lines.some(l => !l.itemId || !l.quantity)) { message.error('Every line needs an item and a quantity.'); return }
    setBusy(true)
    try {
      await api.post('/inventory/transactions', {
        type, warehouseId: v.warehouseId, toWarehouseId: v.toWarehouseId, date: (v.date as Dayjs).format('YYYY-MM-DD'), accountId: v.accountId,
        chargeEntityId: v.chargeEntityId, reference: v.reference, notes: v.notes,
        lines: lines.map(l => ({ itemId: l.itemId, quantity: l.quantity, batchNo: l.batchNo, expiryDate: l.expiryDate?.format('YYYY-MM-DD'), unitCost: l.unitCost, notes: l.notes })),
      })
      message.success(`${TX_LABEL[type]} posted`)
      await Promise.all(['inv-tx', 'inv-stock', 'inv-items', 'inv-batches', 'inv-warehouses'].map(k => qc.invalidateQueries({ queryKey: [k] })))
      onClose()
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }

  return (
    <Modal open width={940} title={TX_LABEL[type]} onCancel={onClose} onOk={save} okText="Post" confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false} initialValues={{ date: dayjs() }}>
        <Row gutter={12}>
          <Col xs={24} md={type === 'Transfer' ? 8 : 10}><Form.Item name="warehouseId" label={type === 'Transfer' ? 'From warehouse' : 'Warehouse'} rules={[{ required: true }]}><WarehouseSelect permission={TX_PERM[type]} /></Form.Item></Col>
          {type === 'Transfer' && <Col xs={24} md={8}><Form.Item name="toWarehouseId" label="To warehouse" rules={[{ required: true }]}><WarehouseSelect permission={TX_PERM[type]} exclude={fromId} /></Form.Item></Col>}
          <Col xs={12} md={type === 'Transfer' ? 4 : 5}><Form.Item name="date" label="Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col xs={12} md={type === 'Transfer' ? 4 : 9}><Form.Item name="reference" label="Reference"><Input /></Form.Item></Col>
          {type === 'Issue' && <>
            <Col xs={24} md={12}><Form.Item name="accountId" label="Charge to account" extra="Blank = each item's consumption account"><AccountSelect allowClear types={['Expense']} /></Form.Item></Col>
            <Col xs={24} md={12}><Form.Item name="chargeEntityId" label="Charge to department / branch" extra="Blank = the warehouse's branch"><EntityPicker permission="inventory.stock.issue" /></Form.Item></Col>
          </>}
          {needsCost && <Col xs={24} md={12}><Form.Item name="accountId" label="Offset account" extra={type === 'Opening' ? 'Blank = retained earnings' : 'Blank = inventory adjustments and write-offs'}><AccountSelect allowClear /></Form.Item></Col>}
        </Row>
      </Form>
      {type === 'Adjustment' && <Alert type="info" showIcon style={{ marginBottom: 12 }} title="Positive quantity adds stock (enter a unit cost or the current average is used); negative removes it — including expired batches." />}
      <Table size="small" pagination={false} rowKey={(_, i) => String(i)} dataSource={lines} scroll={{ x: 800 }}
        columns={[
          { title: 'Item', width: 260, render: (_, l, i) => <ItemSelect value={l.itemId} onChange={v => set(i, { itemId: v })} stockOnly /> },
          { title: 'Qty', width: 110, render: (_, l, i) => <InputNumber value={l.quantity} min={type === 'Adjustment' ? undefined : 0} onChange={v => set(i, { quantity: v ?? 0 })} style={{ width: '100%' }} /> },
          ...(needsCost ? [
            { title: 'Unit cost', width: 120, render: (_: unknown, l: TxLine, i: number) => <InputNumber value={l.unitCost} min={0} disabled={l.quantity < 0} onChange={v => set(i, { unitCost: v ?? undefined })} style={{ width: '100%' }} /> },
            { title: 'Batch / expiry (new stock)', render: (_: unknown, l: TxLine, i: number) => itemOf(l.itemId)?.trackBatches && l.quantity > 0 ? (
              <Space.Compact><Input placeholder="Batch" value={l.batchNo} onChange={e => set(i, { batchNo: e.target.value })} style={{ width: 110 }} />
                {itemOf(l.itemId)?.trackExpiry && <DatePicker value={l.expiryDate} onChange={d => set(i, { expiryDate: d })} placeholder="Expiry" />}</Space.Compact>
            ) : itemOf(l.itemId)?.trackBatches ? <Typography.Text type="secondary">Earliest expiry first</Typography.Text> : null },
          ] : [{ title: 'Batch', render: (_: unknown, l: TxLine) => itemOf(l.itemId)?.trackBatches ? <Typography.Text type="secondary">Earliest expiry first (expired blocked)</Typography.Text> : null }]),
          { title: 'Notes', render: (_, l, i) => <Input value={l.notes} onChange={e => set(i, { notes: e.target.value })} /> },
          { key: 'x', render: (_, __, i) => <Button size="small" danger icon={<DeleteOutlined />} disabled={lines.length === 1} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))} /> },
        ]} />
      <Button style={{ marginTop: 8 }} icon={<PlusOutlined />} onClick={() => setLines(ls => [...ls, { quantity: 1 }])}>Add line</Button>
    </Modal>
  )
}
