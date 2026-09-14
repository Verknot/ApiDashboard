import { Table, Tabs, message } from 'antd'
import { useEffect, useState } from 'react'
import { fetchConfig, fetchServices, getApiMessage, refreshSwagger, reloadConfig, saveConfig } from '../api/client'
import type { CatalogService, SwaggerRefreshResponse } from '../api/types'
import { ContractDiffPanel } from '../components/ContractDiffPanel'
import { UsersAdmin } from '../components/UsersAdmin'
import { YamlEditor } from '../components/YamlEditor'
import { IconAlertTriangle, IconDeviceFloppy, IconFileCode, IconRefresh } from '../icons'

export function AdminPage() {
  return (
    <>
      <p className="page-kicker">Admin</p>
      <h1 className="page-title">Admin</h1>
      <Tabs
        className="admin-tabs"
        items={[
          { key: 'catalog', label: 'Catalog', children: <CatalogAdmin /> },
          { key: 'users', label: 'Users', children: <UsersAdmin /> },
        ]}
      />
    </>
  )
}

function CatalogAdmin() {
  const [services, setServices] = useState<CatalogService[]>([])
  const [yaml, setYaml] = useState('')
  const [savedYaml, setSavedYaml] = useState('')
  const [configPath, setConfigPath] = useState('')
  const [writable, setWritable] = useState(true)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const [swaggerResult, setSwaggerResult] = useState<SwaggerRefreshResponse | null>(null)
  const [diffServiceId, setDiffServiceId] = useState<number | null>(null)
  const dirty = yaml !== savedYaml

  const loadServices = () => {
    void fetchServices().then(setServices)
  }

  const loadConfig = async () => {
    const file = await fetchConfig()
    setYaml(file.content)
    setSavedYaml(file.content)
    setConfigPath(file.path)
    setWritable(file.writable)
  }

  useEffect(() => {
    loadServices()
    void loadConfig().catch(() => message.error('Could not load services.yaml'))
  }, [])

  const applyReloadResult = (result: { upserted: number; deactivated: number; warnings: string[] }) => {
    message.success(`Catalog: ${result.upserted} services, deactivated ${result.deactivated}`)
    if (result.warnings.length > 0) {
      message.warning(result.warnings.join('; '))
    }
    loadServices()
  }

  return (
    <div className="admin-grid">
      <section className="editor-frame">
        <div className="editor-toolbar">
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <IconFileCode size={16} />
              <strong>Catalog source</strong>
            </div>
            <div className="path-label">
              {configPath || '…'}
              {dirty ? ' · unsaved' : ''}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button
              type="button"
              className="btn btn-ghost"
              disabled={loading}
              onClick={async () => {
                setLoading(true)
                try {
                  const result = await reloadConfig()
                  await loadConfig()
                  applyReloadResult(result)
                } catch (error) {
                  message.error(getApiMessage(error, 'Could not reload config'))
                } finally {
                  setLoading(false)
                }
              }}
            >
              <IconRefresh size={16} />
              From disk
            </button>
            <button
              type="button"
              className="btn btn-primary"
              style={{ width: 'auto', paddingInline: 16 }}
              disabled={!writable || !dirty || saving}
              onClick={async () => {
                setSaving(true)
                try {
                  const result = await saveConfig(yaml)
                  setSavedYaml(yaml)
                  applyReloadResult(result)
                  await loadConfig()
                } catch (error) {
                  message.error(getApiMessage(error, 'Could not save config'))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <IconDeviceFloppy size={16} />
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>
        {writable ? null : (
          <div className="notice">
            <IconAlertTriangle size={16} />
            Read-only file. In Kubernetes edit the ConfigMap, then From disk.
          </div>
        )}
        <YamlEditor value={yaml} onChange={setYaml} readOnly={!writable} />
        <div className="config-help">
          <p className="field-label">portals</p>
          <p>
            Сервис = корень. Внутри <code>portals</code>: UserPortal / BackOffice / BackendPortal. У каждого{' '}
            <code>swagger</code> (скачать спеку), <code>urls</code> (среды), <code>auth</code> (Send).
          </p>
          <pre>{`portals:
  - name: UserPortal
    swagger:
      url: https://host/userportal/swagger.yml
      basic:
        username: reader
        password: "secret"
    urls:
      dev: https://api.dev/userportal
      qa: https://api.qa/userportal
    auth:
      type: token
      token_url: https://auth.{environment}/token
      cert: client.pfx
      cert_password: "pfx"
  - name: BackOffice
    swagger:
      url: https://host/backoffice/swagger.json
      basic: { username: reader, password: "secret" }
    urls:
      dev: https://api.dev/backoffice
    auth:
      type: certificate
      cert: client.pfx
      cert_password: "pfx"`}</pre>
          <p>
            <strong>UserPortal</strong> — обычно <code>auth.type: token</code> + cert. <strong>BackOffice / Backend</strong>{' '}
            — <code>type: certificate</code> + <code>cert_password</code>. Регионы:{' '}
            <code>regions: [eu, tr, br, mx]</code>, в urls — <code>{'{region}'}</code>.
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            swagger.basic
          </p>
          <p>
            Скачивание спеки: <code>username</code>/<code>password</code> или{' '}
            <code>vault_username</code>/<code>vault_password</code> / <code>vault_path</code>. Без basic — открытый URL
            или <code>samples/*.json</code>.
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            splunk_url
          </p>
          <p>
            Один раз сверху файла (в кавычках). Плейсхолдер <code>#ConversationId#</code> — из response headers (
            <code>x-conversation-id</code> и т.п.).
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            proxy
          </p>
          <p>
            <code>proxy: true</code> — Send через workbench с PFX на сервере. <code>false</code> — из браузера (CORS /
            Windows cert).
          </p>
        </div>
      </section>
      <section>
        <div className="catalog-toolbar">
          <p className="catalog-head" style={{ margin: 0 }}>
            Active catalog
          </p>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={refreshing}
            onClick={async () => {
              setRefreshing(true)
              try {
                const result = await refreshSwagger()
                setSwaggerResult(result)
                loadServices()
                if (result.failed === 0) {
                  message.success(`Swagger: ${result.ok} services`)
                } else {
                  message.warning(`Swagger: ok ${result.ok}, failed ${result.failed}`)
                }
              } catch (error) {
                message.error(getApiMessage(error, 'Could not refresh swagger'))
              } finally {
                setRefreshing(false)
              }
            }}
          >
            <IconRefresh size={16} />
            {refreshing ? 'Refreshing…' : 'Refresh swagger'}
          </button>
        </div>
        <p className="hint-line">Refresh swagger также доступен всем пользователям на Workbench.</p>
        <Table
          rowKey="id"
          size="small"
          dataSource={services}
          pagination={false}
          onRow={(row) => ({
            onClick: () => setDiffServiceId(row.id),
            style: { cursor: 'pointer' },
          })}
          rowClassName={(row) => (row.id === diffServiceId ? 'ant-table-row-selected' : '')}
          columns={[
            {
              title: '',
              dataIndex: 'color',
              width: 28,
              render: (color: string | null) => (
                <span className="service-color" style={{ background: color ?? '#7a8f6a', height: 16 }} />
              ),
            },
            { title: 'Service', dataIndex: 'name' },
            {
              title: 'Portals',
              render: (_: unknown, row: CatalogService) =>
                row.modules.length > 0 ? row.modules.map((m) => m.name).join(' · ') : '—',
            },
            {
              title: 'Endpoints',
              dataIndex: 'endpointCount',
              width: 96,
              render: (count: number | undefined) => count ?? 0,
            },
            {
              title: 'Regions',
              render: (_: unknown, row: CatalogService) =>
                row.isRegional ? row.regions.map((region) => region.code).join(' · ') : '—',
            },
          ]}
        />
        {swaggerResult ? (
          <ul className="swagger-log">
            {swaggerResult.services.map((row) => (
              <li key={row.service} className={row.status === 'ok' ? 'ok' : 'err'}>
                <strong>{row.service}</strong>
                {row.status === 'ok'
                  ? ` · ${row.endpoints}  +${row.added ?? 0} −${row.removed ?? 0} ~${row.changed ?? 0}`
                  : ` · ${row.error}`}
              </li>
            ))}
          </ul>
        ) : null}
        {diffServiceId ? (
          <div style={{ marginTop: 18 }}>
            <p className="catalog-head">Contract diff</p>
            <ContractDiffPanel serviceId={diffServiceId} />
          </div>
        ) : (
          <p className="hint-line">Click a service to compare swagger snapshots.</p>
        )}
      </section>
    </div>
  )
}
