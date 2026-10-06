import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { App, Button, Card, Col, Form, Input, Modal, Popconfirm, Row, Select, Table, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../../api/client'
import type { PagedResult } from '../../api/types'
import type { Department, Designation, EmployeeListItem } from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'

export default function OrgStructurePage() {
  return (
    <>
      <div className="page-header"><Typography.Title level={2}>Departments & Designations</Typography.Title></div>
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={15}><Departments /></Col>
        <Col xs={24} xl={9}><Designations /></Col>
      </Row>
    </>
  )
}

function Departments() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [editing, setEditing] = useState<Department | 'new' | null>(null)
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['departments'], queryFn: async () => (await api.get<Department[]>('/hr/departments')).data })
  const { data: employees } = useQuery({
    queryKey: ['employees', 'pick'], enabled: !!editing,
    queryFn: async () => (await api.get<PagedResult<EmployeeListItem>>('/hr/employees', { params: { status: 'Active', pageSize: 500 } })).data,
  })

  const save = async () => {
    const v = await form.validateFields()
    try {
      if (editing === 'new') await api.post('/hr/departments', v)
      else if (editing) await api.put(`/hr/departments/${editing.id}`, v)
      message.success('Saved')
      setEditing(null)
      await qc.invalidateQueries({ queryKey: ['departments'] })
    } catch (e) { message.error(errorMessage(e)) }
  }
  const remove = async (id: string) => {
    try { await api.delete(`/hr/departments/${id}`); await qc.invalidateQueries({ queryKey: ['departments'] }) } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <Card title="Departments" extra={can(P.departmentsCreate) && <Button icon={<PlusOutlined />} onClick={() => setEditing('new')}>Add</Button>}>
      <Table<Department> rowKey="id" loading={isLoading} dataSource={data} pagination={false} scroll={{ x: 560 }}
        onRow={d => ({ onClick: () => can(P.departmentsEdit, d.entityId) && setEditing(d), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Department', render: (_, d) => <><b>{d.name}</b> <Typography.Text type="secondary">{d.code}</Typography.Text></> },
          { title: 'Entity', dataIndex: 'entityName' },
          { title: 'Head', dataIndex: 'headName' },
          { title: 'Employees', dataIndex: 'employeeCount' },
          {
            key: 'x', align: 'right', render: (_, d) => can(P.departmentsDelete, d.entityId) && (
              <Popconfirm title="Delete department?" onConfirm={e => { e?.stopPropagation(); void remove(d.id) }} onCancel={e => e?.stopPropagation()}>
                <Button size="small" danger icon={<DeleteOutlined />} onClick={e => e.stopPropagation()} />
              </Popconfirm>
            ),
          },
        ]} />
      <Modal open={!!editing} title={editing === 'new' ? 'New department' : 'Edit department'} onCancel={() => setEditing(null)} onOk={save} destroyOnHidden>
        <Form form={form} layout="vertical" preserve={false} initialValues={editing === 'new' ? {} : editing ?? {}}>
          <Form.Item name="entityId" label="Entity" rules={[{ required: true }]}><EntityPicker permission={P.departmentsCreate} /></Form.Item>
          <Row gutter={12}>
            <Col span={16}><Form.Item name="name" label="Name" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="code" label="Code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>
          </Row>
          <Form.Item name="headEmployeeId" label="Head of department" extra="Can be used as a leave approval step.">
            <Select allowClear showSearch={{ optionFilterProp: 'label' }} options={employees?.items.map(e => ({ value: e.id, label: `${e.fullName} (${e.employeeCode})` }))} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}

function Designations() {
  const { can } = useAuth()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const { data = [], isLoading } = useQuery({ queryKey: ['designations'], queryFn: async () => (await api.get<Designation[]>('/hr/designations')).data })
  const add = async (v: { title: string; grade?: string }) => {
    try { await api.post('/hr/designations', v); form.resetFields(); await qc.invalidateQueries({ queryKey: ['designations'] }) } catch (e) { message.error(errorMessage(e)) }
  }
  const remove = async (id: string) => {
    try { await api.delete(`/hr/designations/${id}`); await qc.invalidateQueries({ queryKey: ['designations'] }) } catch (e) { message.error(errorMessage(e)) }
  }
  return (
    <Card title="Designations">
      {can(P.departmentsCreate) && (
        <Form form={form} layout="inline" onFinish={add} style={{ marginBottom: 12, rowGap: 8 }}>
          <Form.Item name="title" rules={[{ required: true }]}><Input placeholder="Title, e.g. Front Office Manager" /></Form.Item>
          <Form.Item name="grade"><Input placeholder="Grade" style={{ width: 90 }} /></Form.Item>
          <Button htmlType="submit" icon={<PlusOutlined />}>Add</Button>
        </Form>
      )}
      <Table<Designation> rowKey="id" size="small" loading={isLoading} dataSource={data} pagination={false}
        columns={[
          { title: 'Title', dataIndex: 'title' },
          { title: 'Grade', dataIndex: 'grade' },
          can(P.departmentsDelete) ? { key: 'x', align: 'right' as const, render: (_: unknown, d: Designation) => <Popconfirm title="Delete?" onConfirm={() => remove(d.id)}><Button size="small" danger icon={<DeleteOutlined />} /></Popconfirm> } : {},
        ]} />
    </Card>
  )
}
