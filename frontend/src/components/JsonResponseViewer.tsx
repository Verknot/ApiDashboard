import { useMemo, useRef, useState } from 'react'
import { IconCopy, IconSearch } from '../icons'

type Props = {
  value: string
  onChange: (value: string) => void
  onCopy?: () => void
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}

function highlightJson(source: string, query: string): string {
  let text = source
  try {
    text = JSON.stringify(JSON.parse(source), null, 2)
  } catch {
    // keep raw
  }

  const escaped = escapeHtml(text)
  const colored = escaped
    .replace(
      /(&quot;(?:\\.|[^&])*?&quot;)(\s*:)/g,
      '<span class="json-key">$1</span>$2',
    )
    .replace(
      /(:\s*)(&quot;(?:\\.|[^&])*?&quot;)/g,
      '$1<span class="json-string">$2</span>',
    )
    .replace(
      /(:\s*)(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/g,
      '$1<span class="json-number">$2</span>',
    )
    .replace(
      /(:\s*)(true|false|null)/g,
      '$1<span class="json-literal">$2</span>',
    )

  if (!query.trim()) {
    return colored
  }

  const needle = escapeHtml(query)
  const re = new RegExp(needle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi')
  return colored.replace(re, (match) => `<mark class="json-mark">${match}</mark>`)
}

export function JsonResponseViewer({ value, onChange, onCopy }: Props) {
  const [query, setQuery] = useState('')
  const areaRef = useRef<HTMLTextAreaElement>(null)
  const pretty = useMemo(() => {
    try {
      return JSON.stringify(JSON.parse(value), null, 2)
    } catch {
      return value
    }
  }, [value])

  const html = useMemo(() => highlightJson(value || ' ', query), [value, query])

  const jump = () => {
    const q = query.trim()
    if (!q || !areaRef.current) {
      return
    }
    const idx = pretty.toLowerCase().indexOf(q.toLowerCase())
    if (idx < 0) {
      return
    }
    areaRef.current.focus()
    areaRef.current.setSelectionRange(idx, idx + q.length)
  }

  return (
    <div className="json-response">
      <div className="json-response-toolbar">
        <div className="json-search">
          <IconSearch size={14} />
          <input
            value={query}
            placeholder="Search in response"
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                jump()
              }
            }}
          />
        </div>
        {onCopy ? (
          <button type="button" className="btn btn-ghost btn-compact" onClick={onCopy} title="Copy response">
            <IconCopy size={14} />
            Copy
          </button>
        ) : null}
      </div>
      <div className="json-response-editor">
        <pre className="json-response-highlight" aria-hidden dangerouslySetInnerHTML={{ __html: html }} />
        <textarea
          ref={areaRef}
          className="json-response-input"
          value={value}
          spellCheck={false}
          onChange={(event) => onChange(event.target.value)}
        />
      </div>
    </div>
  )
}
