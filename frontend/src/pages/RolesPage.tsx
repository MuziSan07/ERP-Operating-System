import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Button, Card, Checkbox, Col, Collapse, Drawer, Form, Input, Popconfirm, Row, Space, Table, Tag, Typography,
} from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../api/client'
import type { PermissionGroup, Role } from '../api/types'
import { P, useAuth } from '../auth/AuthContext'

export default function RolesPage() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [editing, setEditing] = useState<Role | 'new' | null>(null)
  const manage = can(P.rolesManage)

  const { data: roles = [], isLoading } = useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<Role[]>('/roles')).data })
  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/roles/${id}`),
    onSuccess: () => { message.success('Role deleted'); void qc.invalidateQueries({ queryKey: ['roles'] }) },
    onError: e => message.error(errorMessage(e)),
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Roles & Permissions</Typography.Title>
        {manage && <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New role</Button>}
      </div>
      <Card>
        <Table<Role> rowKey="id" loading={isLoading} dataSource={roles} pagination={false} scroll={{ x: 600 }}
          columns={[
            { title: 'Role', dataIndex: 'name', render: (n, r) => <Space>{n}{r.isSystem && <Tag>Default</Tag>}</Space> },
            { title: 'Description', dataIndex: 'description' },
            { title: 'Permissions', render: (_, r) => r.permissions.length },
            { title: 'Assigned', dataIndex: 'assignmentCount' },
            {
              key: 'a', align: 'right',
              render: (_, r) => (
                <Space>
                  <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(r)}>{manage ? 'Edit' : 'View'}</Button>
                  {manage && !r.isSystem && (
                    <Popconfirm title={`Delete role ${r.name}?`} onConfirm={() => remove.mutate(r.id)}>
                      <Button size="small" danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                  )}
                </Space>
              ),
            },
          ]} />
      </Card>
      {editing && <RoleEditor role={editing === 'new' ? null : editing} readOnly={!manage} onClose={() => setEditing(null)} />}
    </>
  )
}

function RoleEditor({ role, readOnly, onClose }: { role: Role | null; readOnly: boolean; onClose: () => void }) {
  const [form] = Form.useForm()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [selected, setSelected] = useState<Set<string>>(new Set(role?.permissions))
  const [busy, setBusy] = useState(false)
  const { data: catalog = [] } = useQuery({
    queryKey: ['catalog'], queryFn: async () => (await api.get<PermissionGroup[]>('/catalog/permissions')).data,
    staleTime: Infinity,
  })
  useEffect(() => setSelected(new Set(role?.permissions)), [role])

  const toggle = (codes: string[], on: boolean) => setSelected(prev => {
    const next = new Set(prev)
    codes.forEach(c => (on ? next.add(c) : next.delete(c)))
    return next
  })

  const save = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      const body = { ...v, permissions: [...selected] }
      if (role) await api.put(`/roles/${role.id}`, body)
      else await api.post('/roles', body)
      message.success('Role saved')
      await qc.invalidateQueries({ queryKey: ['roles'] })
      onClose()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Drawer open onClose={onClose} size={820} title={role ? `${readOnly ? 'View' : 'Edit'} role: ${role.name}` : 'New role'}
      extra={!readOnly && <Button type="primary" loading={busy} onClick={save}>Save</Button>}>
      <Form form={form} layout="vertical" initialValues={role ?? {}} disabled={readOnly}>
        <Row gutter={12}>
          <Col xs={24} sm={10}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} sm={14}><Form.Item name="description" label="Description"><Input /></Form.Item></Col>
        </Row>
      </Form>
      <Typography.Paragraph type="secondary">
        {selected.size} permissions selected. A role does nothing on its own; assign it to a user at an entity.
      </Typography.Paragraph>
      <Collapse
        defaultActiveKey={['core']}
        items={catalog.map(g => ({
          key: g.module,
          label: <ModuleHeader group={g} selected={selected} />,
          extra: !readOnly && (
            <Checkbox
              onClick={e => e.stopPropagation()}
              checked={g.permissions.every(p => selected.has(p.code))}
              indeterminate={g.permissions.some(p => selected.has(p.code)) && !g.permissions.every(p => selected.has(p.code))}
              onChange={e => toggle(g.permissions.map(p => p.code), e.target.checked)}
            >All</Checkbox>
          ),
          children: <PermissionMatrix group={g} selected={selected} toggle={toggle} readOnly={readOnly} />,
        }))}
      />
    </Drawer>
  )
}

function ModuleHeader({ group, selected }: { group: PermissionGroup; selected: Set<string> }) {
  const n = group.permissions.filter(p => selected.has(p.code)).length
  return <Space>{group.moduleName}<Tag color={n ? 'blue' : undefined}>{n}/{group.permissions.length}</Tag></Space>
}

/** Rows are resources, cells are actions. */
function PermissionMatrix({ group, selected, toggle, readOnly }: {
  group: PermissionGroup; selected: Set<string>; toggle: (c: string[], on: boolean) => void; readOnly: boolean
}) {
  const rows = useMemo(() => {
    const byRes = new Map<string, PermissionGroup['permissions']>()
    group.permissions.forEach(p => byRes.set(p.resource, [...(byRes.get(p.resource) ?? []), p]))
    return [...byRes.entries()].map(([resource, perms]) => ({ resource, perms }))
  }, [group])

  return (
    <Table size="small" pagination={false} rowKey="resource" dataSource={rows}
      columns={[
        {
          title: 'Resource', dataIndex: 'resource', width: 200,
          render: (r: string, row) => (
            <Checkbox disabled={readOnly}
              checked={row.perms.every(p => selected.has(p.code))}
              indeterminate={row.perms.some(p => selected.has(p.code)) && !row.perms.every(p => selected.has(p.code))}
              onChange={e => toggle(row.perms.map(p => p.code), e.target.checked)}>
              {r.replace(/_/g, ' ')}
            </Checkbox>
          ),
        },
        {
          title: 'Actions',
          render: (_, row) => (
            <Space wrap>
              {row.perms.map(p => (
                <Checkbox key={p.code} disabled={readOnly} checked={selected.has(p.code)}
                  onChange={e => toggle([p.code], e.target.checked)}>{p.action}</Checkbox>
              ))}
            </Space>
          ),
        },
      ]} />
  )
}
