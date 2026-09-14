import { Input, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { directSend, fetchHistory, getApiMessage, proxySend, saveHistory } from '../api/client'
import { prettyJson } from '../api/schema'
import type { HistoryItem } from '../api/types'
import { normalizeResponseHeaders } from '../api/types'
import { IconSend } from '../icons'
import { sendModeHint, useSession, useWorkbench } from '../store/workbench'
import { JsonResponseViewer } from './JsonResponseViewer'
import { ResponseHeaderList } from './ResponseHeaderList'

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
  const tab = tabs.find((item) => item.id === tabId && item.kind === 'free')
  const [sending, setSending] = useState(false)
  const [result, setResult] = useState<SendResult | null>(null)
  const [historyTick, setHistoryTick] = useState(0)
  const [recent, setRecent] = useState<HistoryItem[]>([])

  const method = tab?.method ?? 'GET'
  const url = tab?.url ?? ''
  const headersText = tab?.headersText ?? ''
  const body = tab?.body ?? ''
  const token = tab?.token ?? ''
  const sendViaProxy = tab?.proxy ?? true

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
      }
      const relay = sendViaProxy ? await proxySend(payload) : await directSend(payload)
      let pretty = relay.body
      try {
        pretty = JSON.stringify(JSON.parse(relay.body), null, 2)
      } catch {
        pretty = relay.body
      }
      const responseHeaders = normalizeResponseHeaders(relay.headers)
      setResult({
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
      setResult({ status: null, timeMs: 0, body: text, error: text, headers: {} })
    } finally {
      setSending(false)
    }
  }

  return (
    <>
      <div className="endpoint-title">
        <span className={`method-badge method-${method.toLowerCase()}`}>{method}</span>
        <span>Free request</span>
      </div>
      <p style={{ color: 'var(--mute)', maxWidth: '58ch', lineHeight: 1.55, marginTop: 0 }}>
        Paste any absolute http/https URL. proxy goes through the workbench (no CORS). browser is a direct fetch.
      </p>

      <div className="pills" style={{ marginBottom: 12 }}>
        {METHODS.map((item) => (
          <button
            key={item}
            type="button"
            className={`pill${method === item ? ' on' : ''}`}
            onClick={() => apply({ method: item })}
          >
            {item}
          </button>
        ))}
      </div>

      <div className="pills" style={{ marginBottom: 12 }}>
        <button type="button" className={`pill${sendViaProxy ? ' on' : ''}`} onClick={() => apply({ proxy: true })}>
          proxy
        </button>
        <button type="button" className={`pill${!sendViaProxy ? ' on' : ''}`} onClick={() => apply({ proxy: false })}>
          browser
        </button>
        <span className="meta">{sendViaProxy ? 'proxy' : 'browser'}</span>
      </div>

      <label className="field-label">URL</label>
      <input
        className="url-input"
        value={url}
        placeholder="https://api.example.com/path"
        onChange={(event) => apply({ url: event.target.value })}
      />
      <p className="hint-line send-hint">{sendModeHint(sendViaProxy, undefined, url)}</p>

      <label className="field-label">Bearer token</label>
      <input
        className="url-input"
        type="password"
        value={token}
        placeholder="optional · added as Authorization if header is missing"
        onChange={(event) => apply({ token: event.target.value })}
      />

      <label className="field-label">Headers (JSON)</label>
      <Input.TextArea
        className="json-body"
        value={headersText}
        onChange={(event) => apply({ headersText: event.target.value })}
        autoSize={{ minRows: 4, maxRows: 10 }}
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
            autoSize={{ minRows: 8, maxRows: 18 }}
            spellCheck={false}
          />
          {bodyError ? <p className="json-error">{bodyError}</p> : null}
        </>
      ) : null}

      <div className="send-row">
        <button
          type="button"
          className="btn btn-primary"
          style={{ width: 'auto', paddingInline: 18 }}
          disabled={sending || !me?.canSend}
          onClick={() => void send()}
        >
          <IconSend size={16} />
          {sending ? 'Sending…' : sendViaProxy ? 'Send · proxy' : 'Send · browser'}
        </button>
      </div>

      {result ? (
        <section className="response-card">
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
            onChange={(next) => setResult({ ...result, body: next })}
          />
        </section>
      ) : null}

      {recent.length > 0 ? (
        <section className="endpoint-history">
          <div className="endpoint-history-head">
            <p className="catalog-head" style={{ margin: 0 }}>
              Recent free requests
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
                      setResult(null)
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
    </>
  )
}
