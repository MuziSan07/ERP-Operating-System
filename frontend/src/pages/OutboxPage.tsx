import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, App, Button, Card, Space, Table, Tag, Typography } from 'antd'
import { ReloadOutlined, SendOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { Link } from 'react-router-dom'
import { api, errorMessage } from '../api/client'

type EmailStatus = 'Pending' | 'Sent' | 'Failed'
interface OutboxItem { id: string; to: string; subject: string; category: string; status: EmailStatus; attempts: number; lastError?: string; createdAt: string; sentAt?: string }
interface ReminderResult { date: string; skipped: boolean; recipients: number; items: { area: string; text: string; link: string }[] }

const STATUS_COLORS: Record<EmailStatus, string> = { Pending: 'orange', Sent: 'green', Failed: 'red' }
const MODE_TEXT: Record<string, { type: 'success' | 'info' | 'warning'; text: string }> = {
  smtp: { type: 'success', text: 'Emails are sent through the configured mail server.' },
  pickup: { type: 'info', text: 'Development mode: emails are written to files in App_Data/mail instead of being sent.' },
  disabled: { type: 'warning', text: 'No mail server is configured: emails wait here and are not delivered. Set the SMTP settings to send them.' },
}

/** Emails the system has queued for this organization (resets, reminders) and an on-demand reminder digest. */
export default function OutboxPage() {
  const { message } = App.useApp()
  const [running, setRunning] = useState(false)
  const [last, setLast] = useState<ReminderResult>()
  const { data, isLoading, refetch, isFetching } = useQuery({ queryKey: ['outbox'], queryFn: async () => (await api.get<{ mode: string; items: OutboxItem[] }>('/admin/outbox')).data })

  const runReminders = async () => {
    setRunning(true)
    try {
      const r = (await api.post<ReminderResult>('/admin/reminders/run')).data
      setLast(r)
      message.success(r.items.length ? `Digest queued for ${r.recipients} administrator${r.recipients === 1 ? '' : 's'}` : 'Nothing needs attention today')
      await refetch()
    } catch (e) { message.error(errorMessage(e)) }
    finally { setRunning(false) }
  }
  const mode = data && (MODE_TEXT[data.mode] ?? MODE_TEXT.disabled)

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Email & reminders</Typography.Title>
        <Space wrap>
          <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>Refresh</Button>
          <Button type="primary" icon={<SendOutlined />} onClick={runReminders} loading={running}>Send reminders now</Button>
        </Space>
      </div>
      {mode && <Alert type={mode.type} showIcon title={mode.text} />}
      <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
        Every morning after 08:00 the administrators get one digest of what needs attention: overdue invoices, bills falling due, undeposited withholding tax, depreciation not run, pending leave and timesheets, donor reports and expiring vehicle papers.
      </Typography.Paragraph>
      {last && <Card size="small" title={`Today's digest — ${dayjs(last.date).format('DD MMM YYYY')}`}>
        {last.items.length ? last.items.map(i => <div key={i.text} style={{ padding: '4px 0' }}><Tag>{i.area}</Tag><Link to={i.link}>{i.text}</Link></div>)
          : <Typography.Text type="secondary">Nothing needs attention today</Typography.Text>}
      </Card>}
      <Table rowKey="id" size="small" loading={isLoading} dataSource={data?.items} pagination={{ pageSize: 25 }} scroll={{ x: 800 }}
        columns={[
          { title: 'Queued', dataIndex: 'createdAt', render: v => dayjs(v).format('DD MMM YYYY HH:mm'), width: 150 },
          { title: 'To', dataIndex: 'to' },
          { title: 'Subject', dataIndex: 'subject' },
          { title: 'Type', dataIndex: 'category', render: v => <Tag>{v}</Tag> },
          { title: 'Status', render: (_, e) => <><Tag color={STATUS_COLORS[e.status]}>{e.status}</Tag>{e.attempts > 1 && <Typography.Text type="secondary"> {e.attempts} tries</Typography.Text>}
            {e.lastError && e.status !== 'Sent' && <div><Typography.Text type="danger" style={{ fontSize: 12 }}>{e.lastError}</Typography.Text></div>}</> },
          { title: 'Sent', dataIndex: 'sentAt', render: v => v ? dayjs(v).format('DD MMM HH:mm') : '—', width: 120 },
        ]} />
    </Space>
  )
}
