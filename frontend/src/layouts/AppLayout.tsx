import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { logout } from '../api/client'
import { IconApi, IconHistory, IconLogout, IconSettings } from '../icons'
import { useSession } from '../store/workbench'

const nav = [
  { key: 'workbench', label: 'Workbench', path: '/', icon: IconApi },
  { key: 'history', label: 'History', path: '/history', icon: IconHistory },
] as const

export function AppLayout() {
  const me = useSession((s) => s.me)
  const setMe = useSession((s) => s.setMe)
  const navigate = useNavigate()
  const location = useLocation()

  const current = location.pathname.startsWith('/admin')
    ? 'admin'
    : location.pathname.startsWith('/history')
      ? 'history'
      : 'workbench'

  return (
    <div className="shell">
      <aside className="rail">
        <div className="brand">
          <img className="brand-mark" src="/favicon.svg" alt="" />
          <div>
            <span className="brand-name">API Workbench</span>
            <span className="brand-sub">SDET</span>
          </div>
        </div>
        <nav className="nav">
          {nav.map((item) => {
            const Icon = item.icon
            return (
              <button
                key={item.key}
                type="button"
                className={`nav-item${current === item.key ? ' active' : ''}`}
                onClick={() => navigate(item.path)}
              >
                <Icon size={18} />
                {item.label}
              </button>
            )
          })}
          {me?.isAdmin ? (
            <button
              type="button"
              className={`nav-item${current === 'admin' ? ' active' : ''}`}
              onClick={() => navigate('/admin')}
            >
              <IconSettings size={18} />
              Admin
            </button>
          ) : null}
        </nav>
        <div className="rail-foot">
          <div className="who">
            <span className="who-name">{me?.displayName ?? me?.email}</span>
            <span className="who-role">{me?.roles.join(' · ')}</span>
          </div>
          <button
            type="button"
            className="icon-btn"
            title="Sign out"
            onClick={async () => {
              await logout()
              setMe(null)
              navigate('/login')
            }}
          >
            <IconLogout size={18} />
          </button>
        </div>
      </aside>
      <div className="stage">
        <header className="topbar">
          <span>environments from services.yaml · regions in catalog</span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
            <span className="status-dot" />
            local
          </span>
        </header>
        <main className="stage-body">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
