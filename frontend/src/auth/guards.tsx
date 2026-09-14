import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useSession } from '../store/workbench'

export function RequireAuth() {
  const me = useSession((s) => s.me)
  const location = useLocation()

  if (!me) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (me.isFirstLogin && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />
  }

  return <Outlet />
}

export function RequireAdmin() {
  const me = useSession((s) => s.me)
  if (!me?.isAdmin) {
    return <Navigate to="/" replace />
  }
  return <Outlet />
}
