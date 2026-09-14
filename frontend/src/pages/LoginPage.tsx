import { Button, Form, Input } from 'antd'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login } from '../api/client'
import { IconKey } from '../icons'
import { useSession } from '../store/workbench'

export function LoginPage() {
  const setMe = useSession((s) => s.setMe)
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  return (
    <div className="login">
      <section className="login-copy">
        <p className="login-kicker">SDET workbench</p>
        <p>Swagger catalog, regions, request history, and DTO — without jumping across microservices.</p>
      </section>
      <section className="login-form">
        <div className="form-panel">
          <h2>Sign in</h2>
          <p className="hint">Corporate account. Local admin: admin@local</p>
          {error ? <div className="inline-error">{error}</div> : null}
          <Form
            layout="vertical"
            requiredMark={false}
            onFinish={async (values: { email: string; password: string }) => {
              setPending(true)
              setError(null)
              try {
                const me = await login(values.email.trim(), values.password)
                setMe(me)
                navigate(me.isFirstLogin ? '/change-password' : '/')
              } catch {
                setError('Invalid email or password.')
              } finally {
                setPending(false)
              }
            }}
          >
            <Form.Item
              name="email"
              label="Email"
              extra="A domain without a dot is accepted too"
              rules={[
                { required: true, message: 'Enter email' },
                { pattern: /^[^\s@]+@[^\s@]+$/, message: 'Use name@domain' },
              ]}
            >
              <Input autoComplete="username" size="large" />
            </Form.Item>
            <Form.Item name="password" label="Password" rules={[{ required: true, message: 'Enter password' }]}>
              <Input.Password autoComplete="current-password" size="large" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block size="large" loading={pending} icon={<IconKey size={16} />}>
              Sign in
            </Button>
          </Form>
        </div>
      </section>
    </div>
  )
}
