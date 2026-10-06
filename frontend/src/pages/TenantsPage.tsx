import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { App, Button, Card, Col, Divider, Form, Input, InputNumber, Modal, Row, Select, Table, Tag, Typography } from 'antd'
import { EditOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../api/client'
import { INDUSTRIES, type Tenant, type TenantStatus } from '../api/types'
import { BUSINESS_MODULES } from '../layout/AppLayout'

const STATUS_COLOR: Record<TenantStatus, string> = { Active: 'green', Trial: 'gold', Suspended: 'red' }

export default function TenantsPage() {
  const [editing, setEditing] = useState<Tenant | 'new' | null>(null)
  const { data = [], isLoading } = useQuery({
    queryKey: ['tenants'], queryFn: async () => (await api.get<Tenant[]>('/platform/tenants')).data,
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Organizations</Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing('new')}>Onboard organization</Button>
      </div>
      <Card>
        <Table<Tenant> rowKey="id" loading={isLoading} dataSource={data} scroll={{ x: 800 }}
          columns={[
            { title: 'Organization', dataIndex: 'name', render: (n, t) => <><b>{n}</b><div><Tag>{t.code}</Tag></div></> },
            { title: 'Status', dataIndex: 'status', render: (s: TenantStatus) => <Tag color={STATUS_COLOR[s]}>{s}</Tag> },
            { title: 'Users', render: (_, t) => `${t.userCount} / ${t.maxUsers}` },
            { title: 'Entities', dataIndex: 'entityCount' },
            { title: 'Contact', dataIndex: 'contactEmail' },
            { title: 'Since', dataIndex: 'createdAt', render: (d: string) => dayjs(d).format('DD MMM YYYY') },
            { key: 'a', align: 'right', render: (_, t) => <Button size="small" icon={<EditOutlined />} onClick={() => setEditing(t)} /> },
          ]} />
      </Card>
      {editing && <TenantModal tenant={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function TenantModal({ tenant, onClose }: { tenant: Tenant | null; onClose: () => void }) {
  const [form] = Form.useForm()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    const v = await form.validateFields()
    setBusy(true)
    try {
      if (tenant) await api.put(`/platform/tenants/${tenant.id}`, v)
      else await api.post('/platform/tenants', v)
      message.success(tenant ? 'Organization updated' : 'Organization created. Its Super Admin can sign in now.')
      await qc.invalidateQueries({ queryKey: ['tenants'] })
      onClose()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open width={680} title={tenant ? `Edit ${tenant.name}` : 'Onboard organization'} onCancel={onClose} onOk={submit}
      confirmLoading={busy} destroyOnHidden>
      <Form form={form} layout="vertical" preserve={false}
        initialValues={tenant ?? { industry: 'General', maxUsers: 50, currency: 'USD', timeZone: 'UTC', status: 'Active' }}>
        <Row gutter={12}>
          <Col xs={24} sm={16}><Form.Item name="name" label="Organization name" rules={[{ required: true }]}><Input /></Form.Item></Col>
          {!tenant && <Col xs={24} sm={8}><Form.Item name="code" label="Code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>}
          {tenant && (
            <Col xs={24} sm={8}><Form.Item name="status" label="Status">
              <Select options={['Active', 'Trial', 'Suspended'].map(s => ({ value: s, label: s }))} />
            </Form.Item></Col>
          )}
          {!tenant && <Col xs={24} sm={12}><Form.Item name="industry" label="Primary industry"><Select options={INDUSTRIES} /></Form.Item></Col>}
          <Col xs={12} sm={6}><Form.Item name="maxUsers" label="Max users"><InputNumber min={1} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={12} sm={6}><Form.Item name="country" label="Country"><Input /></Form.Item></Col>
          <Col xs={24} sm={12}><Form.Item name="contactEmail" label="Contact email" rules={[{ type: 'email' }]}><Input /></Form.Item></Col>
          <Col xs={24} sm={12}><Form.Item name="contactPhone" label="Contact phone"><Input /></Form.Item></Col>
          {!tenant && (
            <>
              <Col xs={12} sm={6}><Form.Item name="currency" label="Currency"><Input maxLength={3} /></Form.Item></Col>
              <Col xs={12} sm={6}><Form.Item name="timeZone" label="Time zone"><Input /></Form.Item></Col>
              <Col xs={24} sm={12}>
                <Form.Item name="modules" label="Modules" extra="Leave empty to use the suggested set for the industry.">
                  <Select mode="multiple" allowClear options={BUSINESS_MODULES.map(m => ({ value: m.code, label: m.label }))} />
                </Form.Item>
              </Col>
              <Divider titlePlacement="start" plain>Super Admin account</Divider>
              <Col xs={24} sm={12}><Form.Item name="superAdminName" label="Full name" rules={[{ required: true }]}><Input /></Form.Item></Col>
              <Col xs={24} sm={12}><Form.Item name="superAdminEmail" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item></Col>
              <Col span={24}>
                <Form.Item name="superAdminPassword" label="Initial password" rules={[{ required: true, min: 8 }]}
                  extra="At least 8 characters with letters and digits.">
                  <Input.Password autoComplete="new-password" />
                </Form.Item>
              </Col>
            </>
          )}
        </Row>
      </Form>
    </Modal>
  )
}
