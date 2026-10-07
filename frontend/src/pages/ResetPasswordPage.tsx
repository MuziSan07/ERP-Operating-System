import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Alert, Button, Card, Form, Input, Result, Typography } from 'antd'
import { LockOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../api/client'

/** Public page opened from the reset email: /reset-password?token=… */
export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [error, setError] = useState<string>()
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  const onFinish = async (v: { newPassword: string }) => {
    setBusy(true)
    setError(undefined)
    try { await api.post('/auth/reset-password', { token, newPassword: v.newPassword }); setDone(true) }
    catch (e) { setError(errorMessage(e)) }
    finally { setBusy(false) }
  }

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 16, background: 'linear-gradient(135deg,#1d39c4,#2f54eb 50%,#13c2c2)' }}>
      <Card style={{ width: '100%', maxWidth: 400 }}>
        {done ? <Result status="success" title="Password changed" subTitle="Sign in with your new password." extra={<Link to="/login"><Button type="primary">Sign in</Button></Link>} />
          : !token ? <Result status="warning" title="This link is incomplete" subTitle="Open the link from the email again, or ask for a new one." extra={<Link to="/login">Back to sign in</Link>} />
          : <>
            <Typography.Title level={3} style={{ marginBottom: 0 }}>Choose a new password</Typography.Title>
            <Typography.Paragraph type="secondary">At least 12 characters. You'll be signed out on all devices.</Typography.Paragraph>
            {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} />}
            <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
              <Form.Item name="newPassword" label="New password" rules={[{ required: true }, { min: 12, message: 'Use at least 12 characters' }]}>
                <Input.Password prefix={<LockOutlined />} autoComplete="new-password" size="large" />
              </Form.Item>
              <Form.Item name="confirm" label="Repeat it" dependencies={['newPassword']} rules={[{ required: true },
                ({ getFieldValue }) => ({ validator: (_, v) => !v || v === getFieldValue('newPassword') ? Promise.resolve() : Promise.reject(new Error("The passwords don't match")) })]}>
                <Input.Password prefix={<LockOutlined />} autoComplete="new-password" size="large" />
              </Form.Item>
              <Button type="primary" htmlType="submit" block size="large" loading={busy}>Change password</Button>
            </Form>
            <div style={{ marginTop: 12, textAlign: 'center' }}><Link to="/login">Back to sign in</Link></div>
          </>}
      </Card>
    </div>
  )
}
