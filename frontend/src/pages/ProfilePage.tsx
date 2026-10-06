import { App, Button, Card, Col, Descriptions, Form, Input, Row, Tag, Typography } from 'antd'
import { api, errorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function ProfilePage() {
  const { me } = useAuth()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const u = me!.user

  const changePassword = async (v: { currentPassword: string; newPassword: string }) => {
    try {
      await api.post('/auth/change-password', v)
      message.success('Password changed')
      form.resetFields()
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <>
      <div className="page-header"><Typography.Title level={2}>My profile</Typography.Title></div>
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title="Account">
            <Descriptions column={1} size="small">
              <Descriptions.Item label="Name">{u.fullName}</Descriptions.Item>
              <Descriptions.Item label="Email">{u.email}</Descriptions.Item>
              <Descriptions.Item label="Type"><Tag color="blue">{u.userType}</Tag></Descriptions.Item>
              {me?.tenant && <Descriptions.Item label="Organization">{me.tenant.name}</Descriptions.Item>}
              {u.primaryEntityName && <Descriptions.Item label="Entity">{u.primaryEntityName}</Descriptions.Item>}
              <Descriptions.Item label="Entities I can access">{me?.entities.length ?? 0}</Descriptions.Item>
            </Descriptions>
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card title="Change password">
            <Form form={form} layout="vertical" onFinish={changePassword}>
              <Form.Item name="currentPassword" label="Current password" rules={[{ required: true }]}>
                <Input.Password autoComplete="current-password" />
              </Form.Item>
              <Form.Item name="newPassword" label="New password" rules={[{ required: true, min: 8 }]}>
                <Input.Password autoComplete="new-password" />
              </Form.Item>
              <Button type="primary" htmlType="submit">Change password</Button>
            </Form>
          </Card>
        </Col>
      </Row>
    </>
  )
}
