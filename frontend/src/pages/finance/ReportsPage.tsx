import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, Button, Card, Col, DatePicker, Empty, Row, Space, Statistic, Table, Tabs, Tag, Typography } from 'antd'
import { PrinterOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { api } from '../../api/client'
import { amount, type Aging, type FinanceDashboard, type FinancialStatement, type GeneralLedger, type SalesTaxReport, type TrialBalance } from '../../api/finance'
import { fmtDate, monthName } from '../../api/hr'
import EntityPicker from '../../components/EntityPicker'
import { AccountSelect, useFinanceSettings } from '../../components/FinancePickers'
import { JournalView } from './LedgerPages'
import ExportButton from '../../components/ExportButton'

const iso = (d: Dayjs) => d.format('YYYY-MM-DD')
/** Start of the fiscal year containing d. */
const fyStart = (d: Dayjs, startMonth: number) => {
  const m = startMonth - 1
  return (d.month() >= m ? d : d.subtract(1, 'year')).month(m).startOf('month')
}

export function FinanceDashboardPage() {
  const [entityId, setEntityId] = useState<string>()
  const { data, isLoading, error } = useQuery({
    queryKey: ['fin-dashboard', entityId], retry: false,
    queryFn: async () => (await api.get<FinanceDashboard>('/finance/reports/dashboard', { params: { entityId } })).data,
  })
  const max = Math.max(1, ...(data?.monthly.flatMap(m => [m.income, m.expenses]) ?? [1]))
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Finance</Typography.Title>
        <div style={{ minWidth: 260 }}><EntityPicker value={entityId} onChange={setEntityId} permission="finance.reports.view" placeholder="Whole organization" /></div>
      </div>
      {error ? <Alert type="info" showIcon title="Financial summaries need the report permission. Use the menu for invoices, bills and payments." /> : (
        <>
          <Row gutter={[16, 16]}>
            {[
              ['Cash & bank', data?.cashAndBank], ['Receivables', data?.receivables], ['Overdue receivables', data?.overdueReceivables], ['Payables', data?.payables],
              ['Income (this fiscal year)', data?.incomeThisYear], ['Expenses (this fiscal year)', data?.expensesThisYear], ['Profit (this fiscal year)', data?.profitThisYear],
            ].map(([label, v]) => (
              <Col key={label as string} xs={12} md={8} xl={6}>
                <Card loading={isLoading}><Statistic title={`${label} · ${data?.currency ?? ''}`} value={amount(v as number, 0)} styles={{ content: { color: label === 'Overdue receivables' && (v as number) > 0 ? '#cf1322' : undefined } }} /></Card>
              </Col>
            ))}
          </Row>
          <Card title="Income and expenses by month" style={{ marginTop: 16 }} loading={isLoading}>
            {data?.monthly.length ? (
              <Space orientation="vertical" style={{ width: '100%' }}>
                {data.monthly.map(m => (
                  <Row key={`${m.year}-${m.month}`} gutter={12} align="middle">
                    <Col flex="110px"><Typography.Text>{dayjs(new Date(m.year, m.month - 1)).format('MMM YYYY')}</Typography.Text></Col>
                    <Col flex="auto">
                      <div style={{ height: 10, width: `${(m.income / max) * 100}%`, background: '#3f8600', borderRadius: 4, marginBottom: 3, minWidth: m.income ? 2 : 0 }} title={`Income ${amount(m.income, 0)}`} />
                      <div style={{ height: 10, width: `${(m.expenses / max) * 100}%`, background: '#cf1322', borderRadius: 4, minWidth: m.expenses ? 2 : 0 }} title={`Expenses ${amount(m.expenses, 0)}`} />
                    </Col>
                    <Col flex="220px" style={{ textAlign: 'right', fontSize: 12 }}><span style={{ color: '#3f8600' }}>{amount(m.income, 0)}</span> / <span style={{ color: '#cf1322' }}>{amount(m.expenses, 0)}</span></Col>
                  </Row>
                ))}
                <Space><Tag color="green">Income</Tag><Tag color="red">Expenses</Tag></Space>
              </Space>
            ) : <Empty />}
          </Card>
        </>
      )}
    </>
  )
}

export default function ReportsPage() {
  const { data: settings } = useFinanceSettings()
  const [entityId, setEntityId] = useState<string>()
  const start = settings?.fiscalYearStartMonth ?? 7
  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Financial reports</Typography.Title>
        <Space wrap>
          <div style={{ minWidth: 260 }}><EntityPicker value={entityId} onChange={setEntityId} permission="finance.reports.view" placeholder="Whole organization (consolidated)" /></div>
          <Button icon={<PrinterOutlined />} onClick={() => window.print()}>Print</Button>
        </Space>
      </div>
      <Tabs destroyOnHidden items={[
        { key: 'pl', label: 'Profit & loss', children: <ProfitLoss entityId={entityId} start={start} /> },
        { key: 'bs', label: 'Balance sheet', children: <BalanceSheet entityId={entityId} /> },
        { key: 'tb', label: 'Trial balance', children: <TrialBalanceReport entityId={entityId} /> },
        { key: 'gl', label: 'General ledger', children: <Ledger entityId={entityId} start={start} /> },
        { key: 'ar', label: 'Receivables aging', children: <AgingReport kind="Invoice" entityId={entityId} /> },
        { key: 'ap', label: 'Payables aging', children: <AgingReport kind="Bill" entityId={entityId} /> },
        { key: 'tax', label: 'Sales tax', children: <SalesTax entityId={entityId} /> },
      ]} />
    </>
  )
}

function Statement({ data }: { data?: FinancialStatement }) {
  if (!data) return <Card loading />
  const totals = new Set(['Gross profit', 'Profit before tax', 'Total assets', 'Total equity and liabilities'])
  return (
    <Card className="print-area">
      <Typography.Title level={4} style={{ marginTop: 0 }}>{data.title}</Typography.Title>
      <Typography.Text type="secondary">{data.title.includes('position') ? `As at ${fmtDate(data.to)}` : `${fmtDate(data.from)} – ${fmtDate(data.to)}`} · {data.currency}</Typography.Text>
      <table style={{ width: '100%', marginTop: 16, borderCollapse: 'collapse' }}>
        <tbody>
          {data.sections.map(s => totals.has(s.title) ? (
            <tr key={s.title} style={{ borderTop: '2px solid currentColor' }}><td style={{ padding: '8px 0' }}><b>{s.title}</b></td><td style={{ textAlign: 'right' }}><b>{amount(s.total)}</b></td></tr>
          ) : s.lines.length === 0 && s.total === 0 ? null : [
            <tr key={s.title}><td colSpan={2} style={{ paddingTop: 12 }}><Typography.Text strong>{s.title}</Typography.Text></td></tr>,
            ...s.lines.map((l, i) => <tr key={`${s.title}-${i}`}><td style={{ paddingLeft: 16 }}>{l.code && <Typography.Text type="secondary">{l.code} </Typography.Text>}{l.name}</td><td style={{ textAlign: 'right' }}>{amount(l.amount)}</td></tr>),
            <tr key={`${s.title}-t`} style={{ borderTop: '1px solid rgba(128,128,128,.3)' }}><td style={{ paddingLeft: 16 }}><i>Total {s.title.toLowerCase()}</i></td><td style={{ textAlign: 'right' }}><i>{amount(s.total)}</i></td></tr>,
          ])}
          <tr style={{ borderTop: '3px double currentColor' }}>
            <td style={{ padding: '10px 0' }}><b>{data.resultLabel}</b></td>
            <td style={{ textAlign: 'right' }}><b style={{ color: data.title.includes('position') && Math.abs(data.result) > 0.005 ? '#cf1322' : undefined }}>{amount(data.result)}</b></td>
          </tr>
        </tbody>
      </table>
    </Card>
  )
}

function ProfitLoss({ entityId, start }: { entityId?: string; start: number }) {
  const [range, setRange] = useState<[Dayjs, Dayjs]>([fyStart(dayjs(), start), dayjs()])
  const { data } = useQuery({
    queryKey: ['fin-pl', entityId, iso(range[0]), iso(range[1])],
    queryFn: async () => (await api.get<FinancialStatement>('/finance/reports/profit-loss', { params: { entityId, from: iso(range[0]), to: iso(range[1]) } })).data,
  })
  return <><DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} format="DD MMM YYYY" style={{ marginBottom: 12 }} /><Statement data={data} /></>
}

function BalanceSheet({ entityId }: { entityId?: string }) {
  const [asOf, setAsOf] = useState(dayjs())
  const { data } = useQuery({
    queryKey: ['fin-bs', entityId, iso(asOf)],
    queryFn: async () => (await api.get<FinancialStatement>('/finance/reports/balance-sheet', { params: { entityId, asOf: iso(asOf) } })).data,
  })
  return <><DatePicker value={asOf} onChange={v => v && setAsOf(v)} allowClear={false} format="DD MMM YYYY" style={{ marginBottom: 12 }} /><Statement data={data} /></>
}

function TrialBalanceReport({ entityId }: { entityId?: string }) {
  const [asOf, setAsOf] = useState(dayjs())
  const { data, isLoading } = useQuery({
    queryKey: ['fin-tb', entityId, iso(asOf)],
    queryFn: async () => (await api.get<TrialBalance>('/finance/reports/trial-balance', { params: { entityId, asOf: iso(asOf) } })).data,
  })
  return (
    <>
      <Space style={{ marginBottom: 12 }}>
        <DatePicker value={asOf} onChange={v => v && setAsOf(v)} allowClear={false} format="DD MMM YYYY" />
        <ExportButton fileName={`trial-balance-${iso(asOf)}`} title={`Trial balance as at ${fmtDate(iso(asOf))} (${data?.currency ?? ''})`} rows={data?.rows ?? []}
          columns={[{ title: 'Code', value: r => r.code }, { title: 'Account', value: r => r.name, width: 40 }, { title: 'Type', value: r => r.type },
            { title: 'Debit', value: r => r.debit || null, type: 'money' }, { title: 'Credit', value: r => r.credit || null, type: 'money' }]} />
      </Space>
      <Card className="print-area">
        <Typography.Title level={4} style={{ marginTop: 0 }}>Trial balance as at {fmtDate(iso(asOf))} · {data?.currency}</Typography.Title>
        <Table size="small" loading={isLoading} pagination={false} rowKey="accountId" dataSource={data?.rows}
          columns={[
            { title: 'Code', dataIndex: 'code', width: 90 },
            { title: 'Account', dataIndex: 'name' },
            { title: 'Type', dataIndex: 'type', render: (t: string) => <Tag>{t}</Tag> },
            { title: 'Debit', dataIndex: 'debit', align: 'right', render: (v: number) => v ? amount(v) : '' },
            { title: 'Credit', dataIndex: 'credit', align: 'right', render: (v: number) => v ? amount(v) : '' },
          ]}
          summary={() => data && (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={3}><b>Total</b>{Math.abs(data.totalDebit - data.totalCredit) > 0.005 && <Tag color="red" style={{ marginLeft: 8 }}>Out of balance</Tag>}</Table.Summary.Cell>
              <Table.Summary.Cell index={3} align="right"><b>{amount(data.totalDebit)}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={4} align="right"><b>{amount(data.totalCredit)}</b></Table.Summary.Cell>
            </Table.Summary.Row>
          )} />
      </Card>
    </>
  )
}

function Ledger({ entityId, start }: { entityId?: string; start: number }) {
  const [accountId, setAccountId] = useState<string>()
  const [range, setRange] = useState<[Dayjs, Dayjs]>([fyStart(dayjs(), start), dayjs()])
  const [viewing, setViewing] = useState<string | null>(null)
  const { data, isFetching } = useQuery({
    queryKey: ['fin-gl', accountId, entityId, iso(range[0]), iso(range[1])], enabled: !!accountId,
    queryFn: async () => (await api.get<GeneralLedger>('/finance/reports/general-ledger', { params: { accountId, entityId, from: iso(range[0]), to: iso(range[1]) } })).data,
  })
  return (
    <>
      <Row gutter={12} style={{ marginBottom: 12 }}>
        <Col xs={24} md={10}><AccountSelect value={accountId} onChange={setAccountId} placeholder="Choose an account" /></Col>
        <Col xs={24} md={10}><DatePicker.RangePicker value={range} onChange={v => v?.[0] && v[1] && setRange([v[0], v[1]])} allowClear={false} format="DD MMM YYYY" /></Col>
      </Row>
      {!accountId ? <Card><Empty description="Choose an account" /></Card> : (
        <Card className="print-area" title={data && `${data.code} ${data.name}`}>
          <Table size="small" loading={isFetching} pagination={false} rowKey={(_, i) => String(i)} dataSource={data?.rows} scroll={{ x: 800 }}
            onRow={r => ({ onClick: () => setViewing(r.journalEntryId), style: { cursor: 'pointer' } })}
            title={() => <>Opening balance: <b>{amount(data?.opening)}</b></>}
            columns={[
              { title: 'Date', dataIndex: 'date', render: fmtDate, width: 110 },
              { title: 'Entry', dataIndex: 'number', width: 140 },
              { title: 'Description', dataIndex: 'description' },
              { title: 'Contact', dataIndex: 'contactName' },
              { title: 'Debit', dataIndex: 'debit', align: 'right', render: (v: number) => v ? amount(v) : '' },
              { title: 'Credit', dataIndex: 'credit', align: 'right', render: (v: number) => v ? amount(v) : '' },
              { title: 'Balance', dataIndex: 'balance', align: 'right', render: (v: number) => <b>{amount(v)}</b> },
            ]}
            footer={() => <>Closing balance: <b>{amount(data?.closing)}</b></>} />
        </Card>
      )}
      {viewing && <JournalView id={viewing} onClose={() => setViewing(null)} onOpen={setViewing} />}
    </>
  )
}

function AgingReport({ kind, entityId }: { kind: 'Invoice' | 'Bill'; entityId?: string }) {
  const [asOf, setAsOf] = useState(dayjs())
  const { data, isLoading } = useQuery({
    queryKey: ['fin-aging', kind, entityId, iso(asOf)],
    queryFn: async () => (await api.get<Aging>('/finance/reports/aging', { params: { kind, entityId, asOf: iso(asOf) } })).data,
  })
  const cols = [['Not due', 'current'], ['1–30', 'days1To30'], ['31–60', 'days31To60'], ['61–90', 'days61To90'], ['90+', 'over90'], ['Total', 'total']] as const
  return (
    <>
      <Space style={{ marginBottom: 12 }}>
        <DatePicker value={asOf} onChange={v => v && setAsOf(v)} allowClear={false} format="DD MMM YYYY" />
        <ExportButton fileName={`${kind === 'Invoice' ? 'receivables' : 'payables'}-aging-${iso(asOf)}`} title={`${kind === 'Invoice' ? 'Receivables' : 'Payables'} aging as at ${fmtDate(iso(asOf))}`}
          rows={data?.rows ?? []} columns={[{ title: kind === 'Invoice' ? 'Customer' : 'Vendor', value: r => r.contactName, width: 32 },
            ...cols.map(([title, key]) => ({ title, value: (r: (typeof data & object)['rows'][number]) => r[key] || null, type: 'money' as const }))]} />
      </Space>
      <Card className="print-area" title={`${kind === 'Invoice' ? 'Receivables' : 'Payables'} aging as at ${fmtDate(iso(asOf))} · ${data?.currency ?? ''}`}>
        <Table size="small" loading={isLoading} pagination={false} rowKey="contactId" dataSource={data?.rows} scroll={{ x: 700 }}
          columns={[{ title: kind === 'Invoice' ? 'Customer' : 'Vendor', dataIndex: 'contactName' },
            ...cols.map(([title, key]) => ({ title, dataIndex: key, align: 'right' as const, render: (v: number) => v ? (key === 'total' ? <b>{amount(v)}</b> : amount(v)) : '' }))]}
          summary={() => data && (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0}><b>Total</b></Table.Summary.Cell>
              {cols.map(([, key], i) => <Table.Summary.Cell key={key} index={i + 1} align="right"><b>{amount(data.totals[key])}</b></Table.Summary.Cell>)}
            </Table.Summary.Row>
          )} />
      </Card>
    </>
  )
}

function SalesTax({ entityId }: { entityId?: string }) {
  const [month, setMonth] = useState(dayjs().subtract(1, 'month').startOf('month'))
  const from = month.startOf('month'), to = month.endOf('month')
  const { data, isLoading } = useQuery({
    queryKey: ['fin-st', entityId, iso(from)],
    queryFn: async () => (await api.get<SalesTaxReport>('/finance/reports/sales-tax', { params: { entityId, from: iso(from), to: iso(to) } })).data,
  })
  return (
    <>
      <DatePicker picker="month" value={month} onChange={v => v && setMonth(v)} allowClear={false} format="MMMM YYYY" style={{ marginBottom: 12 }} />
      <Card className="print-area" title={`Sales tax summary — ${monthName(month.year(), month.month() + 1)} · ${data?.currency ?? ''}`}>
        <Table size="small" loading={isLoading} pagination={false} rowKey="taxRateId" dataSource={data?.rows} scroll={{ x: 800 }}
          columns={[
            { title: 'Authority', dataIndex: 'authority', render: (a?: string) => a ?? '—' },
            { title: 'Tax', dataIndex: 'name' },
            { title: 'Sales value', dataIndex: 'salesValue', align: 'right', render: (v: number) => amount(v) },
            { title: 'Output tax', dataIndex: 'outputTax', align: 'right', render: (v: number) => amount(v) },
            { title: 'Purchases value', dataIndex: 'purchaseValue', align: 'right', render: (v: number) => amount(v) },
            { title: 'Input tax', dataIndex: 'inputTax', align: 'right', render: (v: number) => amount(v) },
            { title: 'Net', dataIndex: 'net', align: 'right', render: (v: number) => <b>{amount(v)}</b> },
          ]}
          summary={() => data && (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={3}><b>Total</b></Table.Summary.Cell>
              <Table.Summary.Cell index={3} align="right"><b>{amount(data.totalOutput)}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={4} />
              <Table.Summary.Cell index={5} align="right"><b>{amount(data.totalInput)}</b></Table.Summary.Cell>
              <Table.Summary.Cell index={6} align="right"><b>{amount(data.netPayable)}</b></Table.Summary.Cell>
            </Table.Summary.Row>
          )} />
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, fontSize: 12 }}>
          From approved invoices (output) and bills (input) dated in the month. Input tax adjustability and provincial
          rules differ by authority — review with your tax advisor before filing with FBR / PRA / SRB / KPRA.
        </Typography.Paragraph>
      </Card>
    </>
  )
}
