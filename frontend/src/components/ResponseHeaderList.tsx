import { headerObservabilityUrl } from '../api/splunk'
import { IconExternalLink } from '../icons'

type Props = {
  headers: Record<string, string>
  splunkUrl?: string | null
}

export function ResponseHeaderList({ headers, splunkUrl }: Props) {
  const rows = Object.entries(headers).sort(([a], [b]) => a.localeCompare(b))
  if (rows.length === 0) {
    return null
  }

  return (
    <div className="response-headers">
      <p className="field-label" style={{ margin: '0 0 8px' }}>
        Headers
      </p>
      {rows.map(([name, value]) => (
        <HeaderRow key={name} name={name} value={value} splunkUrl={splunkUrl} />
      ))}
    </div>
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
