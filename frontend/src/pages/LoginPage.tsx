import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Alert, App, Button, Card, Form, Input, Modal, Typography } from 'antd'
import { LockOutlined, MailOutlined } from '@ant-design/icons'
import { useAuth } from '../auth/AuthContext'
import { api, errorMessage } from '../api/client'

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)
  const [forgot, setForgot] = useState(false)

  const onFinish = async (v: { email: string; password: string }) => {
    setBusy(true)
    setError(undefined)
    try {
      await login(v.email, v.password)
      navigate('/')
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 16, background: 'linear-gradient(135deg,#1d39c4,#2f54eb 50%,#13c2c2)' }}>
      <Card style={{ width: '100%', maxWidth: 400 }}>
        <Typography.Title level={2} style={{ marginBottom: 0 }}>ERPOS</Typography.Title>
        <Typography.Paragraph type="secondary">Sign in to your organization</Typography.Paragraph>
        {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} />}
        <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}>
            <Input prefix={<MailOutlined />} autoComplete="username" size="large" />
          </Form.Item>
          <Form.Item name="password" label="Password" rules={[{ required: true }]}>
            <Input.Password prefix={<LockOutlined />} autoComplete="current-password" size="large" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block size="large" loading={busy}>Sign in</Button>
        </Form>
        <div style={{ marginTop: 12, textAlign: 'center' }}><Typography.Link onClick={() => setForgot(true)}>Forgot password?</Typography.Link></div>
      </Card>
      {forgot && <ForgotPasswordModal onClose={() => setForgot(false)} />}
    </div>
  )
}

function ForgotPasswordModal({ onClose }: { onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ email: string }>()
  const [busy, setBusy] = useState(false)
  const send = async () => {
    const { email } = await form.validateFields()
    setBusy(true)
    try {
      // The answer is the same whether or not the email exists, so accounts can't be discovered this way.
      const r = (await api.post<{ message: string }>('/auth/forgot-password', { email })).data
      message.success(r.message, 6)
      onClose()
    } catch (e) { message.error(errorMessage(e)) }
    finally { setBusy(false) }
  }
  return (
    <Modal open title="Reset your password" onCancel={onClose} onOk={send} okText="Email me a link" confirmLoading={busy}>
      <Typography.Paragraph type="secondary">We'll email a link to choose a new password. It works once and expires in an hour.</Typography.Paragraph>
      <Form form={form} layout="vertical" requiredMark={false} onFinish={send}>
        <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input prefix={<MailOutlined />} autoComplete="username" autoFocus /></Form.Item>
      </Form>
    </Modal>
  )
}
