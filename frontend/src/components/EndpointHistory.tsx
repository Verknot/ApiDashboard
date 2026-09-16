import { useEffect, useState } from 'react'
import { fetchHistory } from '../api/client'
import { prettyJson } from '../api/schema'
import type { HistoryItem } from '../api/types'
import { HeaderRow } from './ResponseHeaderList'

type Props = {
  endpointId: number
  reloadToken: number
  splunkUrl?: string | null
  onReplay: (item: HistoryItem) => void
}

function statusTone(status: number | null): string {
  if (status == null) {
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

function formatTime(value: string): string {
  return new Date(value).toLocaleString('en-GB', { hour12: false })
}

function bodyPreview(item: HistoryItem): string {
  if (item.requestBody == null) {
    return ''
  }
  const text = prettyJson(item.requestBody).replace(/\s+/g, ' ').trim()
  return text.length > 88 ? `${text.slice(0, 88)}…` : text
}

function headerEntries(value: Record<string, unknown> | null | undefined): [string, string][] {
  if (!value) {
    return []
  }
  return Object.entries(value)
    .map(([name, item]) => [name, item == null ? '' : String(item)] as [string, string])
    .sort(([a], [b]) => a.localeCompare(b))
}

function HeaderBlock({
  label,
  value,
  splunkUrl,
}: {
  label: string
  value: Record<string, unknown> | null | undefined
  splunkUrl?: string | null
}) {
  const [open, setOpen] = useState(false)
  const rows = headerEntries(value)
  if (rows.length === 0) {
    return null
  }
  return (
    <details
      className="response-headers endpoint-history-headers"
      open={open}
      onToggle={(event) => setOpen(event.currentTarget.open)}
    >
      <summary>
        {label}
        <span className="meta" style={{ marginLeft: 8, textTransform: 'none', letterSpacing: 0 }}>
          {rows.length}
        </span>
      </summary>
      <div className="response-headers-body">
        {rows.map(([name, item]) => (
          <HeaderRow key={name} name={name} value={item} splunkUrl={splunkUrl} />
        ))}
      </div>
    </details>
  )
}

export function EndpointHistory({ endpointId, reloadToken, splunkUrl, onReplay }: Props) {
  const [rows, setRows] = useState<HistoryItem[]>([])
  const [openId, setOpenId] = useState<number | null>(null)

  useEffect(() => {
    let cancelled = false
    void fetchHistory({ endpointId, take: 20 })
      .then((items) => {
        if (!cancelled) {
          setRows(items)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setRows([])
        }
      })
    return () => {
      cancelled = true
    }
  }, [endpointId, reloadToken])

  if (rows.length === 0) {
    return null
  }

  return (
    <section className="endpoint-history">
      <div className="endpoint-history-head">
        <p className="catalog-head" style={{ margin: 0 }}>
          Request history
        </p>
        <span className="meta">{rows.length}</span>
      </div>
      <ul className="endpoint-history-list">
        {rows.map((item) => {
          const open = item.id === openId
          const preview = bodyPreview(item)
          return (
            <li key={item.id} className={`endpoint-history-item${open ? ' open' : ''}`}>
              <div className="endpoint-history-line">
                <button
                  type="button"
                  className="endpoint-history-row"
                  onClick={() => setOpenId(open ? null : item.id)}
                >
                  <span className={`status-pill ${statusTone(item.responseStatus)}`}>
                    {item.responseStatus ?? 'ERR'}
                  </span>
                  <span className="endpoint-history-time">{formatTime(item.createdAt)}</span>
                  <span className="endpoint-history-ms">{item.responseTimeMs ?? '—'} ms</span>
                  {preview ? <span className="endpoint-history-preview">{preview}</span> : null}
                </button>
                <button type="button" className="btn btn-ghost btn-compact" onClick={() => onReplay(item)}>
                  Replay
                </button>
              </div>
              {open ? (
                <div className="endpoint-history-detail">
                  <HeaderBlock
                    key={`${item.id}-req-headers`}
                    label="Request headers"
                    value={item.requestHeaders}
                    splunkUrl={splunkUrl}
                  />
                  {item.requestBody != null ? (
                    <>
                      <p className="field-label">Request</p>
                      <pre>{prettyJson(item.requestBody)}</pre>
                    </>
                  ) : null}
                  <HeaderBlock
                    key={`${item.id}-res-headers`}
                    label="Response headers"
                    value={item.responseHeaders}
                    splunkUrl={splunkUrl}
                  />
                  <p className="field-label">Response</p>
                  <pre>{item.responseBody || ' '}</pre>
                </div>
              ) : null}
            </li>
          )
        })}
      </ul>
    </section>
  )
}
