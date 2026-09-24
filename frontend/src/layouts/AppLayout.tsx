import { useEffect, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { logout } from '../api/client'
import { IconApi, IconChevronLeft, IconChevronRight, IconHistory, IconLogout, IconSettings } from '../icons'
import { useSession } from '../store/workbench'

const nav = [
  { key: 'workbench', label: 'Workbench', path: '/', icon: IconApi },
  { key: 'history', label: 'History', path: '/history', icon: IconHistory },
] as const

const RAIL_KEY = 'api-workbench-rail-hidden'

export function AppLayout() {
  const me = useSession((s) => s.me)
  const setMe = useSession((s) => s.setMe)
  const navigate = useNavigate()
  const location = useLocation()
  const [railHidden, setRailHidden] = useState(() => {
    try {
      return localStorage.getItem(RAIL_KEY) === '1'
    } catch {
      return false
    }
  })

  useEffect(() => {
    try {
      localStorage.setItem(RAIL_KEY, railHidden ? '1' : '0')
    } catch {
      /* ignore */
    }
  }, [railHidden])

  const current = location.pathname.startsWith('/admin')
    ? 'admin'
    : location.pathname.startsWith('/history')
      ? 'history'
      : 'workbench'

  return (
    <div className={`shell${railHidden ? ' rail-collapsed' : ''}`}>
      <aside className="rail" aria-hidden={railHidden}>
        <div className="brand">
          <img className="brand-mark" src="/favicon.svg" alt="" />
          <div>
            <span className="brand-name">API Workbench</span>
            <span className="brand-sub">SDET</span>
          </div>
          <button
            type="button"
            className="icon-btn rail-toggle"
            title="Hide sidebar"
            onClick={() => setRailHidden(true)}
          >
            <IconChevronLeft size={18} />
          </button>
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
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
            {railHidden ? (
              <button
                type="button"
                className="icon-btn rail-show"
                title="Show sidebar"
                onClick={() => setRailHidden(false)}
              >
                <IconChevronRight size={18} />
              </button>
            ) : null}
            <span>environments from services.yaml · regions in catalog</span>
          </span>
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
