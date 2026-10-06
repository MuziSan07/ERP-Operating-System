import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Button, Card, Checkbox, Col, Descriptions, Empty, Form, Input, Modal, Popconfirm, Row, Select, Space, Switch,
  Tabs, Tag, Tree, Typography,
} from 'antd'
import { DeleteOutlined, DragOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../api/client'
import { INDUSTRIES, type Entity, type ModuleState } from '../api/types'
import { P, useAuth } from '../auth/AuthContext'
import EntityPicker, { buildTree } from '../components/EntityPicker'

type EntityForm = Omit<Entity, 'id' | 'parentId' | 'depth' | 'childCount' | 'userCount' | 'modules'>

export default function EntitiesPage() {
  const qc = useQueryClient()
  const { can, reload } = useAuth()
  const { message } = App.useApp()
  const [selectedId, setSelectedId] = useState<string>()
  const [modal, setModal] = useState<'create' | 'edit' | 'move' | null>(null)

  const { data: entities = [], isLoading } = useQuery({
    queryKey: ['entities'],
    queryFn: async () => (await api.get<Entity[]>('/entities')).data,
  })
  const selected = entities.find(e => e.id === selectedId) ?? entities[0]
  const tree = useMemo(() => buildTree(entities), [entities])

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ['entities'] })
    await reload() // the user's entity list and permissions changed
  }

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/entities/${id}`),
    onSuccess: async () => { message.success('Entity deleted'); setSelectedId(undefined); await refresh() },
    onError: e => message.error(errorMessage(e)),
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Entities</Typography.Title>
        {selected && can(P.entitiesCreate, selected.id) && (
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setModal('create')}>Add sub-entity</Button>
        )}
      </div>
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={9}>
          <Card title="Structure" loading={isLoading}>
            {tree.length ? (
              <Tree
                treeData={tree}
                defaultExpandAll
                showLine
                selectedKeys={selected ? [selected.id] : []}
                onSelect={k => k[0] && setSelectedId(String(k[0]))}
              />
            ) : <Empty />}
          </Card>
        </Col>
        <Col xs={24} lg={15}>
          {selected && (
            <Card
              title={<Space wrap>{selected.name}<Tag>{selected.code}</Tag>{!selected.isActive && <Tag color="red">Inactive</Tag>}</Space>}
              extra={
                <Space wrap>
                  {can(P.entitiesEdit, selected.id) && <Button icon={<EditOutlined />} onClick={() => setModal('edit')}>Edit</Button>}
                  {selected.parentId && can(P.entitiesEdit, selected.id) && <Button icon={<DragOutlined />} onClick={() => setModal('move')}>Move</Button>}
                  {selected.parentId && can(P.entitiesDelete, selected.id) && (
                    <Popconfirm title="Delete this entity?" onConfirm={() => remove.mutate(selected.id)}>
                      <Button danger icon={<DeleteOutlined />} />
                    </Popconfirm>
                  )}
                </Space>
              }
            >
              <Tabs
                items={[
                  { key: 'details', label: 'Details', children: <EntityDetails entity={selected} /> },
                  { key: 'modules', label: 'Modules', children: <EntityModules entity={selected} onChanged={refresh} /> },
                ]}
              />
            </Card>
          )}
        </Col>
      </Row>

      {modal && selected && (
        <EntityModal
          mode={modal}
          entity={selected}
          onClose={() => setModal(null)}
          onSaved={async (id) => { setModal(null); await refresh(); if (id) setSelectedId(id) }}
        />
      )}
    </>
  )
}

function EntityDetails({ entity: e }: { entity: Entity }) {
  return (
    <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
      <Descriptions.Item label="Industry">{INDUSTRIES.find(i => i.value === e.industry)?.label}</Descriptions.Item>
      <Descriptions.Item label="Level">{e.depth === 0 ? 'Organization (root)' : `Level ${e.depth}`}</Descriptions.Item>
      <Descriptions.Item label="Sub-entities">{e.childCount}</Descriptions.Item>
      <Descriptions.Item label="Users">{e.userCount}</Descriptions.Item>
      <Descriptions.Item label="Currency">{e.currency}</Descriptions.Item>
      <Descriptions.Item label="Time zone">{e.timeZone}</Descriptions.Item>
      <Descriptions.Item label="Email">{e.email || '—'}</Descriptions.Item>
      <Descriptions.Item label="Phone">{e.phone || '—'}</Descriptions.Item>
      <Descriptions.Item label="Address" span="filled">{[e.address, e.city, e.country].filter(Boolean).join(', ') || '—'}</Descriptions.Item>
      <Descriptions.Item label="Tax number">{e.taxNumber || '—'}</Descriptions.Item>
    </Descriptions>
  )
}

function EntityModules({ entity, onChanged }: { entity: Entity; onChanged: () => Promise<void> }) {
  const { can } = useAuth()
  const { message } = App.useApp()
  const qc = useQueryClient()
  const editable = can(P.modulesManage, entity.id)
  const { data = [], isLoading } = useQuery({
    queryKey: ['modules', entity.id],
    queryFn: async () => (await api.get<ModuleState[]>(`/entities/${entity.id}/modules`)).data,
  })
  const [checked, setChecked] = useState<string[]>([])
  useEffect(() => setChecked(data.filter(m => m.enabled && !m.alwaysOn).map(m => m.code)), [data])

  const save = useMutation({
    mutationFn: () => api.put(`/entities/${entity.id}/modules`, { modules: checked }),
    onSuccess: async () => {
      message.success('Modules updated')
      await qc.invalidateQueries({ queryKey: ['modules'] })
      await onChanged()
    },
    onError: e => message.error(errorMessage(e)),
  })

  if (isLoading) return null
  return (
    <>
      <Typography.Paragraph type="secondary">
        A sub-entity can only use modules its parent has. Turning a module off here also turns it off for every sub-entity below.
      </Typography.Paragraph>
      <Row gutter={[12, 12]}>
        {data.map(m => (
          <Col xs={24} sm={12} key={m.code}>
            <Card size="small">
              <Checkbox
                checked={m.alwaysOn || checked.includes(m.code)}
                disabled={!editable || m.alwaysOn || !m.availableFromParent}
                onChange={ev => setChecked(c => ev.target.checked ? [...c, m.code] : c.filter(x => x !== m.code))}
              >
                <b>{m.name}</b> {m.suggested && !m.alwaysOn && <Tag color="green">Suggested</Tag>}
                {!m.availableFromParent && <Tag>Not on parent</Tag>}
              </Checkbox>
              <div><Typography.Text type="secondary" style={{ fontSize: 12 }}>{m.description}</Typography.Text></div>
            </Card>
          </Col>
        ))}
      </Row>
      {editable && (
        <Button type="primary" style={{ marginTop: 16 }} loading={save.isPending} onClick={() => save.mutate()}>Save modules</Button>
      )}
    </>
  )
}

function EntityModal({ mode, entity, onClose, onSaved }: {
  mode: 'create' | 'edit' | 'move'; entity: Entity; onClose: () => void; onSaved: (id?: string) => Promise<void>
}) {
  const [form] = Form.useForm()
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)
  const { me } = useAuth()

  // Descendants of the entity can't become its new parent.
  const descendants = useMemo(() => {
    const all = me?.entities ?? []
    const out = new Set<string>([entity.id])
    let grew = true
    while (grew) {
      grew = false
      for (const e of all) if (e.parentId && out.has(e.parentId) && !out.has(e.id)) { out.add(e.id); grew = true }
    }
    return out
  }, [me, entity.id])

  const initial: Partial<EntityForm> = mode === 'edit'
    ? entity
    : { industry: entity.industry, currency: entity.currency, timeZone: entity.timeZone, isActive: true, country: entity.country }

  const submit = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      if (mode === 'create') {
        const { data } = await api.post<Entity>('/entities', { parentId: entity.id, data: v })
        message.success('Sub-entity created')
        await onSaved(data.id)
      } else if (mode === 'edit') {
        await api.put(`/entities/${entity.id}`, v)
        message.success('Saved')
        await onSaved()
      } else {
        await api.post(`/entities/${entity.id}/move`, v)
        message.success('Entity moved')
        await onSaved()
      }
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const title = mode === 'create' ? `New sub-entity under ${entity.name}` : mode === 'edit' ? `Edit ${entity.name}` : `Move ${entity.name}`

  return (
    <Modal open title={title} onCancel={onClose} onOk={submit} confirmLoading={busy} width={640} destroyOnHidden>
      <Form form={form} layout="vertical" initialValues={initial} preserve={false}>
        {mode === 'move' ? (
          <Form.Item name="newParentId" label="New parent" rules={[{ required: true }]}>
            <EntityPicker permission={P.entitiesCreate} exclude={id => descendants.has(id)} />
          </Form.Item>
        ) : (
          <Row gutter={12}>
            <Col xs={24} sm={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="code" label="Code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/, message: 'Letters, digits, - and _' }]}><Input /></Form.Item></Col>
            <Col xs={24} sm={12}><Form.Item name="industry" label="Industry"><Select options={INDUSTRIES} /></Form.Item></Col>
            <Col xs={12} sm={6}><Form.Item name="currency" label="Currency"><Input maxLength={3} /></Form.Item></Col>
            <Col xs={12} sm={6}><Form.Item name="timeZone" label="Time zone"><Input /></Form.Item></Col>
            <Col xs={24} sm={12}><Form.Item name="email" label="Email" rules={[{ type: 'email' }]}><Input /></Form.Item></Col>
            <Col xs={24} sm={12}><Form.Item name="phone" label="Phone"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="address" label="Address"><Input /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="city" label="City"><Input /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="country" label="Country"><Input /></Form.Item></Col>
            <Col xs={24} sm={8}><Form.Item name="taxNumber" label="Tax number"><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="isActive" label="Active" valuePropName="checked"><Switch /></Form.Item></Col>
          </Row>
        )}
      </Form>
    </Modal>
  )
}
