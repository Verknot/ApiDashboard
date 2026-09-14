import { headerObservabilityUrl } from '../api/splunk'
import { IconExternalLink } from '../icons'

type Props = {
  headers: Record<string, string>
  splunkUrl?: string | null
  mode?: 'proxy' | 'browser'
  /** When true, headers start expanded. Default collapsed. */
  defaultOpen?: boolean
}

export function ResponseHeaderList({ headers, splunkUrl, mode, defaultOpen = false }: Props) {
  const rows = Object.entries(headers).sort(([a], [b]) => a.localeCompare(b))
  if (rows.length === 0) {
    return null
  }

  return (
    <details className="response-headers" defaultOpen={defaultOpen}>
      <summary>
        Response headers
        <span className="meta" style={{ marginLeft: 8, textTransform: 'none', letterSpacing: 0 }}>
          {rows.length}
        </span>
      </summary>
      <div className="response-headers-body">
        {mode === 'browser' && rows.length <= 3 ? (
          <p className="hint-line" style={{ marginTop: 0 }}>
            Browser mode shows only CORS-exposed headers (often just content-type / content-length). Switch to{' '}
            <strong>proxy</strong> to see date, server, x-conversation-id, traceparent, etc.
          </p>
        ) : null}
        {rows.map(([name, value]) => (
          <HeaderRow key={name} name={name} value={value} splunkUrl={splunkUrl} />
        ))}
      </div>
    </details>
  )
}

export function HeaderRow({
  name,
  value,
  splunkUrl,
}: {
  name: string
  value: string
  splunkUrl?: string | null
}) {
  const href = headerObservabilityUrl(splunkUrl, name, value)
  return (
    <div className="response-header-row">
      <span>{name}</span>
      <span>{value}</span>
      {href ? (
        <a
          className="header-splunk"
          href={href}
          target="_blank"
          rel="noopener noreferrer"
          title="Open in Splunk"
        >
          <IconExternalLink size={14} />
        </a>
      ) : (
        <span />
      )}
    </div>
  )
}
