import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Badge, Button, Card, Col, Collapse, Drawer, Empty, Form, Input, Modal, Popconfirm, Radio, Row, Select, Space,
  Switch, Table, Tag, Typography,
} from 'antd'
import { DeleteOutlined, EditOutlined, KeyOutlined, PlusOutlined, SafetyOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../api/client'
import type { PagedResult, PermissionGroup, Role, User, UserAccess, UserType } from '../api/types'
import { P, useAuth } from '../auth/AuthContext'
import EntityPicker from '../components/EntityPicker'

const TYPE_COLORS: Record<UserType, string> = {
  PlatformAdmin: 'magenta', SuperAdmin: 'red', Admin: 'volcano', Manager: 'blue', Employee: 'default',
}

/** User types the signed-in user may create or manage (mirrors the server rule). */
function manageableTypes(mine?: UserType): UserType[] {
  switch (mine) {
    case 'SuperAdmin': return ['SuperAdmin', 'Admin', 'Manager', 'Employee']
    case 'Admin': return ['Admin', 'Manager', 'Employee']
    case 'Manager': return ['Employee']
    default: return []
  }
}

export default function UsersPage() {
  const { me, can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [entityId, setEntityId] = useState<string>()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<User | 'new' | null>(null)
  const [accessFor, setAccessFor] = useState<User | null>(null)
  const [resetFor, setResetFor] = useState<User | null>(null)
  const types = manageableTypes(me?.user.userType)

  const { data, isFetching } = useQuery({
    queryKey: ['users', entityId, search, page],
    queryFn: async () => (await api.get<PagedResult<User>>('/users', { params: { entityId, search, page, pageSize: 20 } })).data,
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/users/${id}`),
    onSuccess: () => { message.success('User deleted'); void qc.invalidateQueries({ queryKey: ['users'] }) },
    onError: e => message.error(errorMessage(e)),
  })

  const canManage = (u: User) => types.includes(u.userType) && u.id !== me?.user.id

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Users</Typography.Title>
        {can(P.usersCreate) && types.length > 0 && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>New user</Button>
        )}
      </div>
      <Card>
        <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
          <Col xs={24} md={10}>
            <EntityPicker value={entityId} onChange={v => { setEntityId(v); setPage(1) }} placeholder="All entities I can see" />
          </Col>
          <Col xs={24} md={10}>
            <Input.Search allowClear placeholder="Search name or email" onSearch={v => { setSearch(v); setPage(1) }} />
          </Col>
          {entityId && <Col><Button onClick={() => setEntityId(undefined)}>Clear entity</Button></Col>}
        </Row>
        <Table<User>
          rowKey="id"
          loading={isFetching}
          dataSource={data?.items}
          scroll={{ x: 800 }}
          pagination={{ current: page, pageSize: 20, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            {
              title: 'Name', dataIndex: 'fullName',
              render: (_, u) => <><div><b>{u.fullName}</b></div><Typography.Text type="secondary">{u.email}</Typography.Text></>,
            },
            { title: 'Type', dataIndex: 'userType', render: (t: UserType) => <Tag color={TYPE_COLORS[t]}>{t}</Tag> },
            { title: 'Entity', dataIndex: 'primaryEntityName' },
            { title: 'Status', dataIndex: 'isActive', render: (a: boolean) => <Badge status={a ? 'success' : 'default'} text={a ? 'Active' : 'Disabled'} /> },
            { title: 'Last login', dataIndex: 'lastLoginAt', render: (d?: string) => d ? dayjs(d).format('DD MMM YYYY HH:mm') : '—' },
            {
              title: '', key: 'actions', align: 'right',
              render: (_, u) => (
                <Space>
                  <Button size="small" icon={<SafetyOutlined />} onClick={() => setAccessFor(u)}>Access</Button>
                  {canManage(u) && can(P.usersEdit, u.primaryEntityId) && (
                    <>
                      <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(u)} />
                      <Button size="small" icon={<KeyOutlined />} onClick={() => setResetFor(u)} />
                    </>
                  )}
                  {canManage(u) && can(P.usersDelete, u.primaryEntityId) && (
                    <Popconfirm title={`Delete ${u.fullName}?`} onConfirm={() => remove.mutate(u.id)}>
                      <Button size="small" danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>

      {editing && <UserModal user={editing === 'new' ? null : editing} types={types} onClose={() => setEditing(null)} />}
      {accessFor && <AccessDrawer user={accessFor} onClose={() => setAccessFor(null)} />}
      {resetFor && <ResetPasswordModal user={resetFor} onClose={() => setResetFor(null)} />}
    </>
  )
}

function UserModal({ user, types, onClose }: { user: User | null; types: UserType[]; onClose: () => void }) {
  const [form] = Form.useForm()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)
  const { data: roles = [] } = useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<Role[]>('/roles')).data })

  const submit = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      if (user) await api.put(`/users/${user.id}`, v)
      else await api.post('/users', v)
      message.success(user ? 'User updated' : 'User created')
      await qc.invalidateQueries({ queryKey: ['users'] })
      onClose()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open title={user ? `Edit ${user.fullName}` : 'New user'} onCancel={onClose} onOk={submit} confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false}
        initialValues={user ?? { userType: types[types.length - 1], isActive: true }}>
        <Form.Item name="fullName" label="Full name" rules={[{ required: true }]}><Input /></Form.Item>
        {!user && <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>}
        <Form.Item name="phone" label="Phone"><Input /></Form.Item>
        <Form.Item name="userType" label="User type" rules={[{ required: true }]}>
          <Select options={types.map(t => ({ value: t, label: t }))} />
        </Form.Item>
        <Form.Item name="primaryEntityId" label="Belongs to entity" rules={[{ required: true }]}>
          <EntityPicker permission={P.usersCreate} />
        </Form.Item>
        {!user && (
          <>
            <Form.Item name="password" label="Initial password" rules={[{ required: true, min: 12 }]}
              extra="At least 12 characters with letters and digits.">
              <Input.Password autoComplete="new-password" />
            </Form.Item>
            <Form.Item name="roleId" label="Role at that entity (optional)" extra="Applies to the entity and all its sub-entities.">
              <Select allowClear options={roles.map(r => ({ value: r.id, label: r.name }))} />
            </Form.Item>
          </>
        )}
        {user && <Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item>}
      </Form>
    </Modal>
  )
}

function ResetPasswordModal({ user, onClose }: { user: User; onClose: () => void }) {
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const submit = async () => {
    const v = await form.validateFields()
    try {
      await api.post(`/users/${user.id}/reset-password`, v)
      message.success('Password reset')
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Modal open title={`Reset password: ${user.fullName}`} onCancel={onClose} onOk={submit} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false}>
        <Form.Item name="newPassword" label="New password" rules={[{ required: true, min: 12 }]}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

function AccessDrawer({ user, onClose }: { user: User; onClose: () => void }) {
  const { can, me } = useAuth()
  const { message } = App.useApp()
  const qc = useQueryClient()
  const key = ['access', user.id]
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: async () => (await api.get<UserAccess>(`/users/${user.id}/access`)).data })
  const { data: roles = [] } = useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<Role[]>('/roles')).data })
  const { data: catalog = [] } = useQuery({
    queryKey: ['catalog'], queryFn: async () => (await api.get<PermissionGroup[]>('/catalog/permissions')).data,
    staleTime: Infinity,
  })
  const [roleForm] = Form.useForm()
  const [ovForm] = Form.useForm()
  const canAssign = can(P.usersAssign) && user.id !== me?.user.id
  const entityName = (id: string) => me?.entities.find(e => e.id === id)?.name ?? id

  const mutate = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => qc.invalidateQueries({ queryKey: key }),
    onError: e => message.error(errorMessage(e)),
  })

  return (
    <Drawer open onClose={onClose} title={`Access: ${user.fullName}`} size={720} loading={isLoading}>
      {(user.userType === 'SuperAdmin') && (
        <Typography.Paragraph><Tag color="red">Super Admin</Tag> has every permission in the organization.</Typography.Paragraph>
      )}

      <Typography.Title level={5}>Roles</Typography.Title>
      <Table size="small" rowKey="id" pagination={false} dataSource={data?.assignments} locale={{ emptyText: 'No roles assigned' }}
        columns={[
          { title: 'Role', dataIndex: 'roleName' },
          { title: 'At entity', dataIndex: 'entityName' },
          { title: 'Scope', dataIndex: 'includeDescendants', render: (d: boolean) => d ? 'Entity + sub-entities' : 'This entity only' },
          canAssign ? {
            key: 'x', align: 'right' as const,
            render: (_: unknown, a: { id: string }) => <Button size="small" danger icon={<DeleteOutlined />}
              onClick={() => mutate.mutate(() => api.delete(`/users/${user.id}/assignments/${a.id}`))} />,
          } : {},
        ]} />
      {canAssign && (
        <Form form={roleForm} layout="inline" style={{ marginTop: 12, rowGap: 8 }} initialValues={{ includeDescendants: true }}
          onFinish={v => mutate.mutate(() => api.post(`/users/${user.id}/assignments`, v).then(() => roleForm.resetFields()))}>
          <Form.Item name="roleId" rules={[{ required: true }]} style={{ minWidth: 160 }}>
            <Select placeholder="Role" options={roles.map(r => ({ value: r.id, label: r.name }))} />
          </Form.Item>
          <Form.Item name="entityId" rules={[{ required: true }]} style={{ minWidth: 220 }}>
            <EntityPicker permission={P.usersAssign} />
          </Form.Item>
          <Form.Item name="includeDescendants" valuePropName="checked"><Switch checkedChildren="+ subs" unCheckedChildren="only" /></Form.Item>
          <Button htmlType="submit" type="primary" icon={<PlusOutlined />}>Assign</Button>
        </Form>
      )}

      <Typography.Title level={5} style={{ marginTop: 24 }}>Permission overrides</Typography.Title>
      <Typography.Paragraph type="secondary">Grant extra permissions or deny ones a role gives. Deny always wins.</Typography.Paragraph>
      <Table size="small" rowKey="id" pagination={false} dataSource={data?.overrides} locale={{ emptyText: 'No overrides' }}
        columns={[
          { title: 'Permission', dataIndex: 'permissionCode', render: (c: string) => <code>{c}</code> },
          { title: 'Effect', dataIndex: 'isGranted', render: (g: boolean) => <Tag color={g ? 'green' : 'red'}>{g ? 'Grant' : 'Deny'}</Tag> },
          { title: 'At entity', dataIndex: 'entityName' },
          canAssign ? {
            key: 'x', align: 'right' as const,
            render: (_: unknown, o: { id: string }) => <Button size="small" danger icon={<DeleteOutlined />}
              onClick={() => mutate.mutate(() => api.delete(`/users/${user.id}/overrides/${o.id}`))} />,
          } : {},
        ]} />
      {canAssign && (
        <Form form={ovForm} layout="inline" style={{ marginTop: 12, rowGap: 8 }} initialValues={{ isGranted: true, includeDescendants: true }}
          onFinish={v => mutate.mutate(() => api.post(`/users/${user.id}/overrides`, v).then(() => ovForm.resetFields()))}>
          <Form.Item name="permissionCode" rules={[{ required: true }]} style={{ minWidth: 240 }}>
            <Select showSearch placeholder="Permission"
              options={catalog.map(g => ({ label: g.moduleName, options: g.permissions.map(p => ({ value: p.code, label: p.code })) }))} />
          </Form.Item>
          <Form.Item name="entityId" rules={[{ required: true }]} style={{ minWidth: 200 }}>
            <EntityPicker permission={P.usersAssign} />
          </Form.Item>
          <Form.Item name="isGranted">
            <Radio.Group optionType="button" options={[{ value: true, label: 'Grant' }, { value: false, label: 'Deny' }]} />
          </Form.Item>
          <Button htmlType="submit" icon={<PlusOutlined />}>Add</Button>
        </Form>
      )}

      <Typography.Title level={5} style={{ marginTop: 24 }}>Effective permissions</Typography.Title>
      {data && Object.keys(data.effective).length === 0 ? <Empty description="No access yet" /> : (
        <Collapse size="small" items={Object.entries(data?.effective ?? {}).map(([eid, perms]) => ({
          key: eid,
          label: <Space>{entityName(eid)}<Tag>{perms.length}</Tag></Space>,
          children: <Space size={[4, 4]} wrap>{perms.map(p => <Tag key={p}>{p}</Tag>)}</Space>,
        }))} />
      )}
    </Drawer>
  )
}
