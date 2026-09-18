import { Input, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { directSend, fetchHistory, getApiMessage, proxySend, saveHistory } from '../api/client'
import { prettyJson } from '../api/schema'
import type { HistoryItem } from '../api/types'
import { normalizeResponseHeaders } from '../api/types'
import { IconCopy, IconSend } from '../icons'
import { sendModeHint, useSession, useWorkbench } from '../store/workbench'
import { JsonResponseViewer } from './JsonResponseViewer'
import { ResponseHeaderList } from './ResponseHeaderList'
import { usePins } from '../store/pins'

const METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'] as const

type SendResult = {
  status: number | null
  timeMs: number
  body: string
  error?: string
  headers: Record<string, string>
}

function hasBody(method: string): boolean {
  return !['GET', 'HEAD'].includes(method.toUpperCase())
}

function statusTone(status: number | null, error?: string): string {
  if (error || status == null) {
    return 's5'
  }
  if (status >= 200 && status < 300) {
    return 's2'
  }
  if (status >= 400 && status < 500) {
    return 's4'
  }
  return 's5'
}

function parseHeaders(text: string): Record<string, string> {
  const trimmed = text.trim()
  if (!trimmed) {
    return {}
  }
  const parsed = JSON.parse(trimmed) as unknown
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('Headers must be a JSON object')
  }
  return Object.fromEntries(
    Object.entries(parsed as Record<string, unknown>).map(([key, value]) => [key, value == null ? '' : String(value)]),
  )
}

type Props = {
  tabId: string
}

export function FreeRequestPane({ tabId }: Props) {
  const me = useSession((s) => s.me)
  const tabs = useWorkbench((s) => s.tabs)
  const patchFreeTab = useWorkbench((s) => s.patchFreeTab)
  const setTabResult = useWorkbench((s) => s.setTabResult)
  const tab = tabs.find((item) => item.id === tabId && item.kind === 'free')
  const [sending, setSending] = useState(false)
  const [result, setResultLocal] = useState<SendResult | null>(
    () => useWorkbench.getState().tabs.find((item) => item.id === tabId)?.lastResult ?? null,
  )
  const commitResult = (next: SendResult | null) => {
    setResultLocal(next)
    setTabResult(tabId, next)
  }
  const [historyTick, setHistoryTick] = useState(0)
  const [recent, setRecent] = useState<HistoryItem[]>([])
  const [detailsOpen, setDetailsOpen] = useState(false)
  const openCreatePin = usePins((s) => s.openCreate)

  const method = tab?.method ?? 'GET'
  const url = tab?.url ?? ''
  const headersText = tab?.headersText ?? ''
  const body = tab?.body ?? ''
  const token = tab?.token ?? ''
  const sendViaProxy = tab?.proxy ?? true
  const clientCertPath = tab?.clientCertPath ?? ''
  const clientCertPassword = tab?.clientCertPassword ?? ''

  const headersError = useMemo(() => {
    try {
      parseHeaders(headersText)
      return null
    } catch (error) {
      return error instanceof Error ? error.message : 'Invalid headers JSON'
    }
  }, [headersText])

  const bodyError = useMemo(() => {
    if (!hasBody(method) || !body.trim()) {
      return null
    }
    try {
      JSON.parse(body)
      return null
    } catch (error) {
      return error instanceof Error ? error.message : 'Invalid JSON'
    }
  }, [body, method])

  useEffect(() => {
    void fetchHistory({ take: 40 })
      .then((items) => setRecent(items.filter((item) => !item.endpointId).slice(0, 12)))
      .catch(() => setRecent([]))
  }, [historyTick])

  useEffect(() => {
    if (hasBody(method) || clientCertPath.trim()) {
      setDetailsOpen(true)
    }
  }, [method, clientCertPath])

  if (!tab) {
    return null
  }

  const apply = (patch: Partial<typeof tab>) => patchFreeTab(tabId, patch)

  const send = async () => {
    if (!me?.canSend) {
      message.warning('Send is not available for this role')
      return
    }
    if (!url.trim()) {
      message.error('Enter a URL')
      return
    }
    if (headersError) {
      message.error(headersError)
      return
    }
    if (bodyError) {
      message.error(bodyError)
      return
    }

    let headers: Record<string, string>
    try {
      headers = parseHeaders(headersText)
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Invalid headers')
      return
    }

    if (token.trim() && !Object.keys(headers).some((key) => key.toLowerCase() === 'authorization')) {
      headers.Authorization = `Bearer ${token.trim()}`
    }
    if (hasBody(method) && !Object.keys(headers).some((key) => key.toLowerCase() === 'content-type')) {
      headers['Content-Type'] = 'application/json'
    }

    setSending(true)
    try {
      const payload = {
        serviceId: null,
        url: url.trim(),
        method,
        headers,
        body: hasBody(method) ? body : null,
        clientCertPath: sendViaProxy && clientCertPath.trim() ? clientCertPath.trim() : null,
        clientCertPassword: sendViaProxy && clientCertPassword ? clientCertPassword : null,
      }
      const relay = sendViaProxy ? await proxySend(payload) : await directSend(payload)
      let pretty = relay.body
      try {
        pretty = JSON.stringify(JSON.parse(relay.body), null, 2)
      } catch {
        pretty = relay.body
      }
      const responseHeaders = normalizeResponseHeaders(relay.headers)
      commitResult({
        status: relay.status,
        timeMs: relay.timeMs,
        body: pretty,
        error: relay.error ?? undefined,
        headers: responseHeaders,
      })
      void saveHistory({
        serviceId: null,
        endpointId: null,
        environment: null,
        regionCode: '',
        url: url.trim(),
        method,
        requestHeaders: JSON.stringify(headers),
        requestBody: hasBody(method) ? body : null,
        responseStatus: relay.status,
        responseBody: pretty,
        responseTimeMs: relay.timeMs,
        responseHeaders: JSON.stringify(responseHeaders),
      }).then(() => setHistoryTick((tick) => tick + 1))
    } catch (error) {
      const text = getApiMessage(error, 'Send failed')
      commitResult({ status: null, timeMs: 0, body: text, error: text, headers: {} })
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="free-pane">
      <div className="free-composer">
        <div className="free-toolbar">
          <select
            className="free-method"
            value={method}
            onChange={(event) => apply({ method: event.target.value })}
            aria-label="HTTP method"
          >
            {METHODS.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
          <div className="pills free-mode-pills">
            <button type="button" className={`pill${sendViaProxy ? ' on' : ''}`} onClick={() => apply({ proxy: true })}>
              proxy
            </button>
            <button
              type="button"
              className={`pill${!sendViaProxy ? ' on' : ''}`}
              onClick={() => apply({ proxy: false })}
            >
              browser
            </button>
          </div>
          <button
            type="button"
            className="btn btn-primary free-send"
            disabled={sending || !me?.canSend}
            onClick={() => void send()}
          >
            <IconSend size={16} />
            {sending ? 'Sending…' : 'Send'}
          </button>
        </div>

        <div className="req-url-row">
          <input
            className="url-input free-url"
            value={url}
            placeholder="https://api.example.com/path"
            onChange={(event) => apply({ url: event.target.value })}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
                void send()
              }
            }}
          />
          <button
            type="button"
            className="btn btn-ghost btn-compact"
            title="Copy URL"
            disabled={!url.trim()}
            onClick={async () => {
              await navigator.clipboard.writeText(url.trim())
              message.success('URL copied')
            }}
          >
            <IconCopy size={14} />
            URL
          </button>
        </div>
        <p className="hint-line send-hint free-hint">{sendModeHint(sendViaProxy, undefined, url)}</p>

        <details
          className="free-details"
          open={detailsOpen}
          onToggle={(event) => setDetailsOpen((event.target as HTMLDetailsElement).open)}
        >
          <summary>
            Token · headers{hasBody(method) ? ' · body' : ''}
            {sendViaProxy ? ' · client cert' : ''}
          </summary>
          <div className="free-details-body">
            <label className="field-label">Bearer token</label>
            <input
              className="url-input"
              type="password"
              value={token}
              placeholder="optional"
              onChange={(event) => apply({ token: event.target.value })}
            />

            <label className="field-label">Headers (JSON)</label>
            <Input.TextArea
              className="json-body"
              value={headersText}
              onChange={(event) => apply({ headersText: event.target.value })}
              autoSize={{ minRows: 2, maxRows: 8 }}
              spellCheck={false}
            />
            {headersError ? <p className="json-error">{headersError}</p> : null}

            {hasBody(method) ? (
              <>
                <label className="field-label">Body</label>
                <Input.TextArea
                  className="json-body"
                  value={body}
                  onChange={(event) => apply({ body: event.target.value })}
                  autoSize={{ minRows: 4, maxRows: 14 }}
                  spellCheck={false}
                />
                {bodyError ? <p className="json-error">{bodyError}</p> : null}
              </>
            ) : null}

            {sendViaProxy ? (
              <div className="free-cert-row">
                <div>
                  <label className="field-label">Client cert (PFX in C:\pult-certs)</label>
                  <input
                    className="url-input"
                    value={clientCertPath}
                    placeholder="client.pfx"
                    onChange={(event) => apply({ clientCertPath: event.target.value })}
                  />
                </div>
                <div>
                  <label className="field-label">Cert password</label>
                  <input
                    className="url-input"
                    type="password"
                    value={clientCertPassword}
                    placeholder="optional"
                    onChange={(event) => apply({ clientCertPassword: event.target.value })}
                  />
                </div>
              </div>
            ) : null}
          </div>
        </details>
      </div>

      {result ? (
        <section className="response-card free-response">
          <div className="response-head">
            <span className={`status-pill ${statusTone(result.status, result.error)}`}>
              {result.status ?? 'ERR'}
            </span>
            <span>{result.timeMs} ms</span>
            {result.error ? <span>network</span> : null}
          </div>
          {Object.keys(result.headers).length > 0 ? (
            <ResponseHeaderList headers={result.headers} mode={sendViaProxy ? 'proxy' : 'browser'} />
          ) : null}
          <JsonResponseViewer
            value={result.body || ' '}
            onChange={(next) => commitResult({ ...result, body: next })}
            onPin={(payload) =>
              openCreatePin({
                value: payload.value,
                alias: payload.alias,
                sourceKey: payload.sourceKey,
              })
            }
          />
        </section>
      ) : (
        <div className="free-empty-response">
          <p className="meta">Response will show here after Send</p>
        </div>
      )}

      {recent.length > 0 ? (
        <section className="endpoint-history free-history">
          <div className="endpoint-history-head">
            <p className="catalog-head" style={{ margin: 0 }}>
              Recent
            </p>
            <span className="meta">{recent.length}</span>
          </div>
          <ul className="endpoint-history-list">
            {recent.map((item) => (
              <li key={item.id} className="endpoint-history-item">
                <div className="endpoint-history-line">
                  <button
                    type="button"
                    className="endpoint-history-row"
                    onClick={() => {
                      apply({
                        method: (item.method ?? 'GET').toUpperCase(),
                        url: item.url ?? '',
                        body: item.requestBody == null ? '' : prettyJson(item.requestBody),
                        headersText: item.requestHeaders
                          ? prettyJson(item.requestHeaders)
                          : '{\n  "Accept": "application/json"\n}',
                      })
                      let pretty = item.responseBody ?? ''
                      try {
                        pretty = JSON.stringify(JSON.parse(pretty), null, 2)
                      } catch {
                        pretty = item.responseBody ?? ''
                      }
                      commitResult({
                        status: item.responseStatus,
                        timeMs: item.responseTimeMs ?? 0,
                        body: pretty,
                        headers: normalizeResponseHeaders(
                          item.responseHeaders
                            ? Object.fromEntries(
                                Object.entries(item.responseHeaders).map(([key, value]) => [
                                  key,
                                  value == null ? '' : String(value),
                                ]),
                              )
                            : {},
                        ),
                      })
                    }}
                  >
                    <span className={`status-pill ${statusTone(item.responseStatus)}`}>
                      {item.responseStatus ?? 'ERR'}
                    </span>
                    <span className="endpoint-history-time">
                      {new Date(item.createdAt).toLocaleString('en-GB', { hour12: false })}
                    </span>
                    <span className="endpoint-history-preview">
                      {(item.method ?? 'GET').toUpperCase()} {item.url}
                    </span>
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  )
}
