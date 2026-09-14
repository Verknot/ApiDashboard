import { ConfigProvider, theme } from 'antd'
import enUS from 'antd/locale/en_US'
import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { fetchMe } from './api/client'
import { RequireAdmin, RequireAuth } from './auth/guards'
import { AppLayout } from './layouts/AppLayout'
import { AdminPage } from './pages/AdminPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { HistoryPage } from './pages/HistoryPage'
import { LoginPage } from './pages/LoginPage'
import { WorkbenchPage } from './pages/WorkbenchPage'
import { useSession } from './store/workbench'

const font = '"Outfit", "Segoe UI", system-ui, sans-serif'

export default function App() {
  const setMe = useSession((s) => s.setMe)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    void fetchMe()
      .then(setMe)
      .catch(() => setMe(null))
      .finally(() => setReady(true))
  }, [setMe])

  if (!ready) {
    return (
      <div className="skel" style={{ maxWidth: 480, margin: '20vh auto' }}>
        <div className="skel-line" style={{ width: '40%' }} />
        <div className="skel-line" style={{ width: '92%' }} />
        <div className="skel-line" style={{ width: '74%' }} />
      </div>
    )
  }

  return (
    <ConfigProvider
      locale={enUS}
      theme={{
        algorithm: theme.darkAlgorithm,
        token: {
          colorPrimary: '#7a8f6a',
          colorInfo: '#7a8f6a',
          colorBgBase: '#18181b',
          colorBgContainer: '#1f1f23',
          colorBorder: 'rgba(228,228,231,0.10)',
          colorText: '#e4e4e7',
          colorTextSecondary: '#a1a1aa',
          borderRadius: 10,
          fontFamily: font,
          fontFamilyCode: '"JetBrains Mono", ui-monospace, monospace',
        },
      }}
    >
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<RequireAuth />}>
            <Route path="/change-password" element={<ChangePasswordPage />} />
            <Route element={<AppLayout />}>
              <Route path="/" element={<WorkbenchPage />} />
              <Route path="/history" element={<HistoryPage />} />
              <Route element={<RequireAdmin />}>
                <Route path="/admin" element={<AdminPage />} />
              </Route>
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  )
}
