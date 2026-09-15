import { useEffect, useMemo, useRef, useState } from 'react'
import { IconCopy, IconMapPin, IconSearch } from '../icons'

type Props = {
  value: string
  onChange: (value: string) => void
  onCopy?: () => void
  onPin?: (payload: { value: string; alias?: string; sourceKey?: string }) => void
}

const MIN_HEIGHT = 160
const DEFAULT_HEIGHT = 360

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
}

function formatJson(source: string): string {
  try {
    return JSON.stringify(JSON.parse(source), null, 2)
  } catch {
    return source
  }
}

function highlightJson(source: string, query: string): string {
  const escaped = escapeHtml(source || ' ')
  const colored = escaped
    .replace(/(&quot;(?:\\.|[^&])*?&quot;)(\s*:)/g, '<span class="json-key">$1</span>$2')
    .replace(/(:\s*)(&quot;(?:\\.|[^&])*?&quot;)/g, '$1<span class="json-string">$2</span>')
    .replace(/(:\s*)(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/g, '$1<span class="json-number">$2</span>')
    .replace(/(:\s*)(true|false|null)/g, '$1<span class="json-literal">$2</span>')

  if (!query.trim()) {
    return colored
  }

  const needle = escapeHtml(query)
  const re = new RegExp(needle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi')
  return colored.replace(re, (match) => `<mark class="json-mark">${match}</mark>`)
}

function guessAliasFromSelection(draft: string, start: number): string | undefined {
  const before = draft.slice(Math.max(0, start - 80), start)
  const keyMatch = before.match(/"([^"\\]+)"\s*:\s*"?$/)
  return keyMatch?.[1]
}

export function JsonResponseViewer({ value, onChange, onCopy, onPin }: Props) {
  const [query, setQuery] = useState('')
  const [height, setHeight] = useState(DEFAULT_HEIGHT)
  const areaRef = useRef<HTMLTextAreaElement>(null)
  const preRef = useRef<HTMLPreElement>(null)
  const dragRef = useRef<{ startY: number; startH: number } | null>(null)
  const lastExternal = useRef(value)

  const [draft, setDraft] = useState(() => formatJson(value))

  useEffect(() => {
    if (value !== lastExternal.current) {
      lastExternal.current = value
      setDraft(formatJson(value))
    }
  }, [value])

  const html = useMemo(() => highlightJson(draft || ' ', query), [draft, query])

  useEffect(() => {
    const onMove = (event: MouseEvent) => {
      if (!dragRef.current) {
        return
      }
      const next = dragRef.current.startH + (event.clientY - dragRef.current.startY)
      setHeight(Math.max(MIN_HEIGHT, Math.min(window.innerHeight * 0.85, next)))
    }
    const onUp = () => {
      dragRef.current = null
      document.body.classList.remove('is-row-resizing')
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  const syncScroll = () => {
    const area = areaRef.current
    const pre = preRef.current
    if (!area || !pre) {
      return
    }
    pre.scrollTop = area.scrollTop
    pre.scrollLeft = area.scrollLeft
  }

  const jump = () => {
    const q = query.trim()
    if (!q || !areaRef.current) {
      return
    }
    const idx = draft.toLowerCase().indexOf(q.toLowerCase())
    if (idx < 0) {
      return
    }
    areaRef.current.focus()
    areaRef.current.setSelectionRange(idx, idx + q.length)
    const before = draft.slice(0, idx)
    const line = before.split('\n').length
    const lineHeight = 19.5
    areaRef.current.scrollTop = Math.max(0, (line - 3) * lineHeight)
    syncScroll()
  }

  const pinSelection = () => {
    if (!onPin || !areaRef.current) {
      return
    }
    const start = areaRef.current.selectionStart
    const end = areaRef.current.selectionEnd
    let selected = draft.slice(start, end).trim()
    if (!selected) {
      return
    }
    if (
      (selected.startsWith('"') && selected.endsWith('"')) ||
      (selected.startsWith("'") && selected.endsWith("'"))
    ) {
      selected = selected.slice(1, -1)
    }
    if (!selected) {
      return
    }
    const sourceKey = guessAliasFromSelection(draft, start)
    onPin({ value: selected, alias: sourceKey, sourceKey })
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
        {onPin ? (
          <button
            type="button"
            className="btn btn-ghost btn-compact"
            title="Select a value in the response, then pin it"
            onClick={pinSelection}
          >
            <IconMapPin size={14} />
            Pin
          </button>
        ) : null}
        {onCopy ? (
          <button type="button" className="btn btn-ghost btn-compact" onClick={onCopy} title="Copy response">
            <IconCopy size={14} />
            Copy
          </button>
        ) : null}
      </div>
      <div className="json-response-editor" style={{ height }}>
        <pre
          ref={preRef}
          className="json-response-highlight"
          aria-hidden
          dangerouslySetInnerHTML={{ __html: html }}
        />
        <textarea
          ref={areaRef}
          className="json-response-input"
          value={draft}
          spellCheck={false}
          onScroll={syncScroll}
          onChange={(event) => {
            const next = event.target.value
            setDraft(next)
            lastExternal.current = next
            onChange(next)
          }}
        />
      </div>
      <div
        className="json-response-grip"
        title="Drag to resize height"
        onMouseDown={(event) => {
          event.preventDefault()
          dragRef.current = { startY: event.clientY, startH: height }
          document.body.classList.add('is-row-resizing')
        }}
      />
    </div>
  )
}
