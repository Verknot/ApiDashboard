import { Input, Table, Tabs, message } from 'antd'
import { useEffect, useState } from 'react'
import { fetchConfig, fetchServices, getApiMessage, refreshSwagger, reloadConfig, saveConfig } from '../api/client'
import type { CatalogService, SwaggerRefreshResponse } from '../api/types'
import { ContractDiffPanel } from '../components/ContractDiffPanel'
import { UsersAdmin } from '../components/UsersAdmin'
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
              disabled={!writable || !dirty}
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
        <div className="yaml-input">
          <Input.TextArea
            value={yaml}
            onChange={(event) => setYaml(event.target.value)}
            disabled={!writable}
            spellCheck={false}
            autoSize={{ minRows: 22, maxRows: 40 }}
          />
        </div>
        <div className="config-help">
          <p className="field-label">swagger.auth</p>
          <p>
            <code>none</code> — без авторизации. <code>basic</code> — креды в таком порядке, первое найденное
            побеждает:
          </p>
          <ol>
            <li>
              <code>username</code> + <code>password</code> прямо в YAML
            </li>
            <li>
              <code>vault_username</code> + <code>vault_password</code> — два секрета Vault
            </li>
            <li>
              <code>vault_path</code> — логин и пароль в одном секрете
            </li>
          </ol>
          <p>
            <code>vault_base64: true</code> — значения (и из YAML, и из Vault) декодируются из base64. Ключи в секрете
            по умолчанию: <code>SwaggerBasicAuthUsername</code> / <code>SwaggerBasicAuthPassword</code>. Другой ключ:{' '}
            <code>secret/app/login#SwaggerBasicAuthUsername</code>.
          </p>
          <p>
            HTTP swagger за mTLS: <code>auth.type: certificate</code> плюс <code>cert_path</code> /{' '}
            <code>cert_base64</code> / <code>cert_vault</code> — workbench подложит PFX. <code>swagger.auth: basic</code>{' '}
            можно одновременно. URL swagger должен быть HTTPS. Локальный <code>samples/*.json</code> читается с диска, без
            Basic и без сертификата.
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            несколько swagger
          </p>
          <p>
            Один сервис может тянуть несколько спек: UserPortal, backend, backOffice. Тогда <code>swagger</code> —
            список, у каждой обязательный <code>name</code>. В дереве:{' '}
            <code>service → swagger → tags → endpoints</code>. Один swagger объектом, без <code>name</code> — без
            лишнего уровня. URL swagger и Basic у каждой спеки свои. Сертификат, <code>proxy</code> и Splunk общие на
            сервис.
          </p>
          <pre>{`swagger:
  - name: UserPortal
    url: https://host/swagger/userportal/swagger.json
    auth: none
    environments:
      dev: http://localhost:5157/userportal
      stage: https://api.stage.company.com/userportal
      prod: https://api.prod.company.com/userportal
  - name: backend
    url: https://host/swagger/backend/swagger.json
    auth: none
    environments:
      dev: http://localhost:5157/backend
      stage: https://api.stage.company.com/backend
      prod: https://api.prod.company.com/backend`}</pre>
          <p className="field-label" style={{ marginTop: 14 }}>
            environments
          </p>
          <p>
            На сервисе обязательны <code>dev</code> / <code>stage</code> / <code>prod</code> — запасной base URL. Те же
            ключи можно указать у swagger: Send идёт на них; чего нет — с сервиса. Для регионов в шаблоне{' '}
            <code>{'{region}'}</code>, как у notify-service. Либо явные URL у каждого региона, как у geo-catalog. Список{' '}
            <code>regions</code> общий на сервис; адрес выбирается как swagger + среда + регион.
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            api_auth у swagger (разный Send на подсервисах)
          </p>
          <p>
            Один корень сервиса, у каждого swagger свой Send: UserPortal — token, backend — certificate. Поле{' '}
            <code>swagger.auth</code> по-прежнему только для скачивания спеки (basic/none). Для API используйте{' '}
            <code>api_auth</code>. Без <code>api_auth</code> модуль наследует сервисный <code>auth</code>. PFX можно
            задать на сервисе один раз — модули его переиспользуют.
          </p>
          <pre>{`swagger:
  - name: UserPortal
    url: https://host/userportal/swagger.yml
    auth: basic
    username: u1
    password: "123"
    environments:
      dev: https://api.dev/userportal
      stage: https://api.stage/userportal
      prod: https://api.prod/userportal
    api_auth:
      type: token
      token_url: https://auth.company.com/token
      cert_path: client.pfx
  - name: backend
    url: https://host/backend/swagger.json
    auth: none
    environments:
      dev: https://api.dev/order
      stage: https://api.stage/order
      prod: https://api.prod/order
    api_auth:
      type: certificate
      cert_path: client.pfx
environments:
  dev: https://api.dev/userportal
  stage: https://api.stage/userportal
  prod: https://api.prod/userportal
auth:
  type: none`}</pre>
          <p className="field-label" style={{ marginTop: 14 }}>
            auth.token
          </p>
          <p>
            API вызывается с Bearer. Кнопка Get делает GET на <code>auth.token_url</code> с тем же PFX из{' '}
            <code>C:\pult-certs</code>, что и Send, и забирает <code>accessToken</code> из JSON. URL должен быть HTTPS.
            Токен общий на сервис + среду + регион.
          </p>
          <pre>{`auth:
  type: token
  token_url: https://auth.company.com/token
  cert_path: certs/client.pfx
  # or: cert_base64 / cert_vault
  cert_password: optional`}</pre>
          <p>
            <code>token_url</code> может быть картой <code>dev</code>/<code>stage</code>/<code>prod</code>, шаблоном с{' '}
            <code>{'{environment}'}</code> / <code>{'{region}'}</code> или относительным путём к base URL сервиса.
            Другое поле в ответе — <code>token_field</code>.
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            auth.certificate / PFX
          </p>
          <p>
            Для proxy Send, swagger mTLS и Get token нужен PFX на сервере (не выбор в браузере). Варианты, первое
            найденное:
          </p>
          <ol>
            <li>
              <code>cert_path</code> — файл в <code>C:\pult-certs</code> / <code>/certs</code>
            </li>
            <li>
              <code>cert_base64</code> — весь PFX одной Base64-строкой (PowerShell:{' '}
              <code>[Convert]::ToBase64String([IO.File]::ReadAllBytes(&apos;client.pfx&apos;))</code>)
            </li>
            <li>
              <code>cert_vault</code> — путь Vault, например <code>secret/pult/client-cert</code> или{' '}
              <code>secret/pult/client-cert#certificate</code>. Ключи по умолчанию:{' '}
              <code>certificate</code> / <code>cert_base64</code> / <code>pfx</code>. Пароль: <code>cert_password</code>{' '}
              в YAML или в том же секрете (<code>password</code> / <code>cert_password</code>).
            </li>
          </ol>
          <pre>{`auth:
  type: certificate
  cert_vault: secret/pult/client-cert#certificate
  cert_password: optional`}</pre>
          <p className="field-label" style={{ marginTop: 14 }}>
            proxy
          </p>
          <p>
            В <code>services.yaml</code> у каждого сервиса. <code>proxy: true</code> (если поле не указано — тоже) —
            Send через workbench, сертификат с сервера (path / base64 / Vault). <code>proxy: false</code> — напрямую из
            браузера: нужен CORS или Allow CORS, клиентский сертификат — окно Windows (только HTTPS).
          </p>
          <p className="field-label" style={{ marginTop: 14 }}>
            splunk_url
          </p>
          <p>
            Полная ссылка на поиск Splunk. <code>#ConversationId#</code> (и любой другой{' '}
            <code>#HeaderName#</code>) подставляется из response headers — рядом появляется иконка. Можно один раз
            сверху файла или у сервиса. Ссылку обязательно в кавычках, иначе YAML съест <code>#</code> как комментарий.
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
            {refreshing ? 'Refreshing…' : 'Refresh all'}
          </button>
        </div>
        <p className="hint-line">
          swagger.url — local file or HTTP. Several specs: a list with name. Basic: username/password in YAML or Vault
          (see the hint under the editor).
        </p>
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
            { title: 'Auth', dataIndex: 'authType', width: 110 },
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
