import { Input, Modal, Select, Table, message } from 'antd'
import { useEffect, useState } from 'react'
import {
  createAdminUser,
  fetchAdminUsers,
  getApiMessage,
  resetAdminUserPassword,
  setAdminUserActive,
  setAdminUserRole,
} from '../api/client'
import { ASSIGNABLE_ROLES, type AdminUser, type AssignableRole } from '../api/types'
import { IconUser } from '../icons'
import { useSession } from '../store/workbench'

const ROLE_OPTIONS = ASSIGNABLE_ROLES.map((role) => ({ value: role, label: role }))

function formatWhen(value: string): string {
  return new Date(value).toLocaleString('en-GB', { hour12: false })
}

function primaryRole(roles: string[]): AssignableRole {
  const match = ASSIGNABLE_ROLES.find((role) => roles.includes(role))
  return match ?? 'viewer'
}

export function UsersAdmin() {
  const meId = useSession((s) => s.me?.id)
  const [rows, setRows] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(false)
  const [creating, setCreating] = useState(false)
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<AssignableRole>('tester')
  const [resetUser, setResetUser] = useState<AdminUser | null>(null)
  const [resetPassword, setResetPassword] = useState('')

  const load = async () => {
    setLoading(true)
    try {
      setRows(await fetchAdminUsers())
    } catch (error) {
      message.error(getApiMessage(error, 'Could not load users'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const replace = (next: AdminUser) => {
    setRows((current) => current.map((row) => (row.id === next.id ? next : row)).sort((a, b) => a.email.localeCompare(b.email)))
  }

  return (
    <section>
      <p className="hint-line users-hint">
        Admin sets a temporary password. The user must change it on first login. Roles: admin, tester, developer, viewer.
      </p>
      <form
        className="users-create"
        onSubmit={async (event) => {
          event.preventDefault()
          setCreating(true)
          try {
            const created = await createAdminUser({
              email: email.trim(),
              displayName: displayName.trim() || undefined,
              password,
              role,
            })
            setRows((current) => [...current, created].sort((a, b) => a.email.localeCompare(b.email)))
            setEmail('')
            setDisplayName('')
            setPassword('')
            setRole('tester')
            message.success(`Created ${created.email}`)
          } catch (error) {
            message.error(getApiMessage(error, 'Could not create user'))
          } finally {
            setCreating(false)
          }
        }}
      >
        <label>
          Email
          <Input value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="off" required />
        </label>
        <label>
          Display name
          <Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} autoComplete="off" />
        </label>
        <label>
          Password
          <Input.Password value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" required />
        </label>
        <label>
          Role
          <Select value={role} options={ROLE_OPTIONS} onChange={setRole} />
        </label>
        <button type="submit" className="btn btn-primary" disabled={creating} style={{ width: 'auto', paddingInline: 16 }}>
          <IconUser size={16} />
          {creating ? 'Creating…' : 'Create'}
        </button>
      </form>
      <Table
        rowKey="id"
        size="small"
        loading={loading}
        pagination={false}
        dataSource={rows}
        columns={[
          { title: 'Email', dataIndex: 'email' },
          {
            title: 'Name',
            dataIndex: 'displayName',
            render: (value: string | null) => value || '—',
          },
          {
            title: 'Role',
            width: 160,
            render: (_: unknown, row: AdminUser) => (
              <Select
                size="small"
                style={{ width: 140 }}
                value={primaryRole(row.roles)}
                options={ROLE_OPTIONS}
                disabled={row.id === meId && primaryRole(row.roles) === 'admin'}
                onChange={async (next) => {
                  try {
                    replace(await setAdminUserRole(row.id, next))
                  } catch (error) {
                    message.error(getApiMessage(error, 'Could not change role'))
                  }
                }}
              />
            ),
          },
          {
            title: 'Status',
            width: 120,
            render: (_: unknown, row: AdminUser) => (
              <span className="meta">
                {row.isActive ? 'active' : 'disabled'}
                {row.isFirstLogin ? ' · first login' : ''}
              </span>
            ),
          },
          {
            title: 'Created',
            dataIndex: 'createdAt',
            width: 160,
            render: (value: string) => formatWhen(value),
          },
          {
            title: '',
            width: 220,
            render: (_: unknown, row: AdminUser) => (
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn-ghost"
                  style={{ height: 32, padding: '0 10px', width: 'auto' }}
                  onClick={() => {
                    setResetUser(row)
                    setResetPassword('')
                  }}
                >
                  Reset password
                </button>
                <button
                  type="button"
                  className="btn btn-ghost"
                  style={{ height: 32, padding: '0 10px', width: 'auto' }}
                  disabled={row.id === meId}
                  onClick={async () => {
                    try {
                      replace(await setAdminUserActive(row.id, !row.isActive))
                    } catch (error) {
                      message.error(getApiMessage(error, 'Could not update user'))
                    }
                  }}
                >
                  {row.isActive ? 'Disable' : 'Enable'}
                </button>
              </div>
            ),
          },
        ]}
      />
      <Modal
        title={resetUser ? `Reset password · ${resetUser.email}` : 'Reset password'}
        open={resetUser != null}
        okText="Reset"
        cancelText="Cancel"
        onCancel={() => setResetUser(null)}
        onOk={async () => {
          if (!resetUser) {
            return
          }
          try {
            replace(await resetAdminUserPassword(resetUser.id, resetPassword))
            message.success('Password reset. They must change it on next login.')
            setResetUser(null)
            setResetPassword('')
          } catch (error) {
            message.error(getApiMessage(error, 'Could not reset password'))
            return Promise.reject()
          }
        }}
      >
        <p className="hint-line">Temporary password, at least 8 characters. Sets first login.</p>
        <Input.Password
          value={resetPassword}
          autoComplete="new-password"
          onChange={(event) => setResetPassword(event.target.value)}
        />
      </Modal>
    </section>
  )
}
