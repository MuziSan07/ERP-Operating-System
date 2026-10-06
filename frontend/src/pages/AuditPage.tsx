import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Card, Select, Table, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { api } from '../api/client'
import type { AuditLog, PagedResult } from '../api/types'

const TABLES = ['BusinessEntity', 'User', 'Role', 'RolePermission', 'UserRoleAssignment', 'UserPermissionOverride', 'EntityModule']
const ACTION_COLOR: Record<string, string> = { Create: 'green', Update: 'blue', Delete: 'red' }

export default function AuditPage() {
  const [table, setTable] = useState<string>()
  const [page, setPage] = useState(1)
  const { data, isFetching } = useQuery({
    queryKey: ['audit', table, page],
    queryFn: async () => (await api.get<PagedResult<AuditLog>>('/audit', { params: { table, page, pageSize: 25 } })).data,
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Audit Log</Typography.Title>
        <Select allowClear placeholder="All record types" style={{ minWidth: 220 }} value={table}
          onChange={v => { setTable(v); setPage(1) }} options={TABLES.map(t => ({ value: t, label: t }))} />
      </div>
      <Card>
        <Table<AuditLog> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 800 }}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          expandable={{
            rowExpandable: r => !!r.changes,
            expandedRowRender: r => <pre style={{ margin: 0, whiteSpace: 'pre-wrap', fontSize: 12 }}>{JSON.stringify(JSON.parse(r.changes!), null, 2)}</pre>,
          }}
          columns={[
            { title: 'When', dataIndex: 'timestamp', render: (d: string) => dayjs(d).format('DD MMM YYYY HH:mm:ss') },
            { title: 'Who', dataIndex: 'userName', render: (n?: string) => n ?? 'System' },
            { title: 'Action', dataIndex: 'action', render: (a: string) => <Tag color={ACTION_COLOR[a]}>{a}</Tag> },
            { title: 'Record type', dataIndex: 'tableName' },
            { title: 'Record', dataIndex: 'recordId', render: (r?: string) => <Typography.Text code copyable={!!r}>{r?.slice(0, 8)}</Typography.Text> },
          ]} />
      </Card>
    </>
  )
}
