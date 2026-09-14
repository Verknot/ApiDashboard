import { Button, Form, Input, message } from 'antd'
import { useNavigate } from 'react-router-dom'
import { changePassword, fetchMe } from '../api/client'
import { IconLock } from '../icons'
import { useSession } from '../store/workbench'

export function ChangePasswordPage() {
  const setMe = useSession((s) => s.setMe)
  const navigate = useNavigate()

  return (
    <div className="login">
      <section className="login-copy">
        <p className="login-kicker">First login</p>
        <h1>Set your password</h1>
        <p>The temporary password will not be needed again. At least 8 characters.</p>
      </section>
      <section className="login-form">
        <div className="form-panel">
          <h2>Change password</h2>
          <p className="hint">After save you open the workbench.</p>
          <Form
            layout="vertical"
            requiredMark={false}
            onFinish={async (values: { currentPassword: string; newPassword: string }) => {
              try {
                await changePassword(values.currentPassword, values.newPassword)
                const me = await fetchMe()
                setMe(me)
                message.success('Password updated')
                navigate('/')
              } catch {
                message.error('Could not change password')
              }
            }}
          >
            <Form.Item name="currentPassword" label="Current password" rules={[{ required: true }]}>
              <Input.Password />
            </Form.Item>
            <Form.Item name="newPassword" label="New password" extra="At least 8 characters" rules={[{ required: true, min: 8 }]}>
              <Input.Password />
            </Form.Item>
            <Button type="primary" htmlType="submit" block size="large" icon={<IconLock size={16} />}>
              Save
            </Button>
          </Form>
        </div>
      </section>
    </div>
  )
}
