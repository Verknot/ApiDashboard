import { useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { IconCopy, IconMapPin, IconMaximize, IconMinimize, IconSearch } from '../icons'

type Props = {
  value: string
  onChange: (value: string) => void
  onCopy?: () => void
  onPin?: (payload: { value: string; alias?: string; sourceKey?: string }) => void
}

const MIN_HEIGHT = 160
const DEFAULT_HEIGHT = 360
const TREE_AUTO_CHARS = 1200

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

function tryParseJson(source: string): unknown | undefined {
  try {
    return JSON.parse(source)
  } catch {
    return undefined
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

function clampHeight(value: number): number {
  const max = Math.max(MIN_HEIGHT, Math.floor(window.innerHeight * 0.75))
  return Math.max(MIN_HEIGHT, Math.min(max, value))
}

function previewValue(value: unknown): string {
  if (value === null) {
    return 'null'
  }
  if (Array.isArray(value)) {
    return `Array(${value.length})`
  }
  if (typeof value === 'object') {
    return `{${Object.keys(value as object).length}}`
  }
  if (typeof value === 'string') {
    return value.length > 48 ? `"${value.slice(0, 48)}…"` : JSON.stringify(value)
  }
  return String(value)
}

function JsonTreeNode({
  name,
  value,
  depth,
  path,
}: {
  name?: string
  value: unknown
  depth: number
  path: string
}) {
  const isExpandable = value !== null && typeof value === 'object'
  const [open, setOpen] = useState(depth < 2)

  if (!isExpandable) {
    return (
      <div className="json-tree-row" style={{ paddingLeft: depth * 14 }}>
        {name != null ? <span className="json-tree-key">{name}: </span> : null}
        <span className={`json-tree-leaf json-tree-${typeof value}`}>{previewValue(value)}</span>
      </div>
    )
  }

  const entries: Array<[string, unknown]> = Array.isArray(value)
    ? value.map((item, index) => [String(index), item])
    : Object.entries(value as Record<string, unknown>)

  return (
    <div className="json-tree-node">
      <button
        type="button"
        className="json-tree-toggle"
        style={{ paddingLeft: depth * 14 }}
        onClick={() => setOpen((current) => !current)}
      >
        <span className={`json-tree-chevron${open ? ' open' : ''}`}>▸</span>
        {name != null ? <span className="json-tree-key">{name}: </span> : null}
        <span className="json-tree-preview">{open ? (Array.isArray(value) ? '[' : '{') : previewValue(value)}</span>
      </button>
      {open ? (
        <>
          {entries.map(([key, child]) => (
            <JsonTreeNode key={`${path}.${key}`} name={key} value={child} depth={depth + 1} path={`${path}.${key}`} />
          ))}
          <div className="json-tree-row json-tree-close" style={{ paddingLeft: depth * 14 }}>
            {Array.isArray(value) ? ']' : '}'}
          </div>
        </>
      ) : null}
    </div>
  )
}

function ViewerChrome({
  query,
  setQuery,
  onJump,
  mode,
  setMode,
  canTree,
  fullscreen,
  onToggleFullscreen,
  onPin,
  onCopy,
  pinSelection,
}: {
  query: string
  setQuery: (value: string) => void
  onJump: () => void
  mode: 'tree' | 'text'
  setMode: (mode: 'tree' | 'text') => void
  canTree: boolean
  fullscreen: boolean
  onToggleFullscreen: () => void
  onPin?: Props['onPin']
  onCopy?: () => void
  pinSelection: () => void
}) {
  return (
    <div className="json-response-toolbar">
      <div className="json-search">
        <IconSearch size={14} />
        <input
          value={query}
          placeholder="Search in response"
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              onJump()
            }
          }}
        />
      </div>
      {canTree ? (
        <div className="pills json-mode-pills">
          <button type="button" className={`pill${mode === 'tree' ? ' on' : ''}`} onClick={() => setMode('tree')}>
            Tree
          </button>
          <button type="button" className={`pill${mode === 'text' ? ' on' : ''}`} onClick={() => setMode('text')}>
            Text
          </button>
        </div>
      ) : null}
      <button
        type="button"
        className="btn btn-ghost btn-compact"
        title={fullscreen ? 'Exit fullscreen' : 'Fullscreen'}
        onClick={onToggleFullscreen}
      >
        {fullscreen ? <IconMinimize size={14} /> : <IconMaximize size={14} />}
      </button>
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
  )
}

export function JsonResponseViewer({ value, onChange, onCopy, onPin }: Props) {
  const [query, setQuery] = useState('')
  const [height, setHeight] = useState(DEFAULT_HEIGHT)
  const [fullscreen, setFullscreen] = useState(false)
  const [mode, setMode] = useState<'tree' | 'text'>('text')
  const areaRef = useRef<HTMLTextAreaElement>(null)
  const editorRef = useRef<HTMLDivElement>(null)
  const treeRef = useRef<HTMLDivElement>(null)
  const preRef = useRef<HTMLPreElement>(null)
  const dragRef = useRef<{ startY: number; startH: number } | null>(null)
  const lastExternal = useRef(value)
  const autoFitRef = useRef(true)

  const [draft, setDraft] = useState(() => formatJson(value))
  const parsed = useMemo(() => tryParseJson(draft), [draft])
  const canTree = parsed !== undefined

  useEffect(() => {
    if (value !== lastExternal.current) {
      lastExternal.current = value
      autoFitRef.current = true
      const next = formatJson(value)
      setDraft(next)
      setMode(next.length >= TREE_AUTO_CHARS && tryParseJson(next) !== undefined ? 'tree' : 'text')
    }
  }, [value])

  useLayoutEffect(() => {
    if (!autoFitRef.current || fullscreen) {
      return
    }

    if (mode === 'tree') {
      const tree = treeRef.current
      if (!tree) {
        return
      }
      const prev = tree.style.height
      tree.style.height = 'auto'
      const measured = tree.scrollHeight
      tree.style.height = prev
      // Tree is for browsing structure — give it a tall pane (at least content, up to 75vh).
      setHeight(clampHeight(Math.max(measured + 8, Math.floor(window.innerHeight * 0.55))))
      autoFitRef.current = false
      return
    }

    const area = areaRef.current
    const editor = editorRef.current
    if (!area || !editor) {
      return
    }

    const prevEditorHeight = editor.style.height
    const prevAreaHeight = area.style.height
    editor.style.height = 'auto'
    area.style.height = 'auto'
    const measured = area.scrollHeight
    editor.style.height = prevEditorHeight
    area.style.height = prevAreaHeight

    setHeight(clampHeight(measured))
    autoFitRef.current = false
  }, [draft, mode, fullscreen])

  const setViewerMode = (next: 'tree' | 'text') => {
    if (next === mode) {
      return
    }
    if (next === 'tree') {
      autoFitRef.current = true
    }
    setMode(next)
  }

  const html = useMemo(() => highlightJson(draft || ' ', query), [draft, query])

  useEffect(() => {
    const onMove = (event: MouseEvent) => {
      if (!dragRef.current) {
        return
      }
      const next = dragRef.current.startH + (event.clientY - dragRef.current.startY)
      setHeight(clampHeight(next))
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

  useEffect(() => {
    if (!fullscreen) {
      return
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setFullscreen(false)
      }
    }
    document.body.classList.add('json-fullscreen-open')
    window.addEventListener('keydown', onKey)
    return () => {
      document.body.classList.remove('json-fullscreen-open')
      window.removeEventListener('keydown', onKey)
    }
  }, [fullscreen])

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
    if (!q) {
      return
    }
    if (mode === 'tree') {
      setMode('text')
    }
    window.setTimeout(() => {
      if (!areaRef.current) {
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
    }, 0)
  }

  const pinSelection = () => {
    if (!onPin || !areaRef.current) {
      if (mode === 'tree') {
        setMode('text')
      }
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

  const toolbar = (
    <ViewerChrome
      query={query}
      setQuery={setQuery}
      onJump={jump}
      mode={mode}
      setMode={setViewerMode}
      canTree={canTree}
      fullscreen={fullscreen}
      onToggleFullscreen={() => setFullscreen((current) => !current)}
      onPin={onPin}
      onCopy={onCopy}
      pinSelection={pinSelection}
    />
  )

  const editorBody: ReactNode =
    mode === 'tree' && canTree ? (
      <div ref={treeRef} className="json-response-tree" style={fullscreen ? undefined : { height }}>
        <JsonTreeNode value={parsed} depth={0} path="$" />
      </div>
    ) : (
      <div ref={editorRef} className="json-response-editor" style={fullscreen ? undefined : { height }}>
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
    )

  const grip =
    !fullscreen ? (
      <div
        className="json-response-grip"
        title="Drag to resize height"
        onMouseDown={(event) => {
          event.preventDefault()
          dragRef.current = { startY: event.clientY, startH: height }
          document.body.classList.add('is-row-resizing')
        }}
      />
    ) : null

  if (fullscreen) {
    return (
      <>
        <div className="json-response json-response-placeholder">
          <div className="json-response-toolbar">
            <p className="hint-line" style={{ margin: 0 }}>
              Response is open fullscreen — press Esc to close.
            </p>
            <button type="button" className="btn btn-ghost btn-compact" onClick={() => setFullscreen(false)}>
              <IconMinimize size={14} />
              Exit
            </button>
          </div>
        </div>
        <div className="json-fullscreen">
          <div className="json-fullscreen-panel">
            {toolbar}
            {editorBody}
          </div>
        </div>
      </>
    )
  }

  return (
    <div className="json-response">
      {toolbar}
      {editorBody}
      {grip}
    </div>
  )
}
