import { useCallback, useEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react'

const STORAGE_KEY = 'api-workbench-catalog-width'
const MIN = 240
const MAX = 640
const DEFAULT = 400
const LEGACY_DEFAULT = 300

type Props = {
  left: ReactNode
  right: ReactNode
}

function readWidth(): number {
  const raw = Number(localStorage.getItem(STORAGE_KEY))
  if (!Number.isFinite(raw) || raw === LEGACY_DEFAULT) {
    localStorage.setItem(STORAGE_KEY, String(DEFAULT))
    return DEFAULT
  }
  return Math.min(MAX, Math.max(MIN, raw))
}

export function ResizableWorkbench({ left, right }: Props) {
  const [width, setWidth] = useState(readWidth)
  const drag = useRef<{ startX: number; startW: number } | null>(null)

  useEffect(() => {
    const onMove = (event: globalThis.MouseEvent) => {
      if (!drag.current) {
        return
      }
      const next = drag.current.startW + (event.clientX - drag.current.startX)
      setWidth(Math.min(MAX, Math.max(MIN, next)))
    }
    const onUp = () => {
      if (!drag.current) {
        return
      }
      drag.current = null
      document.body.classList.remove('is-col-resizing')
      setWidth((current) => {
        localStorage.setItem(STORAGE_KEY, String(current))
        return current
      })
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  const onMouseDown = useCallback(
    (event: MouseEvent) => {
      event.preventDefault()
      drag.current = { startX: event.clientX, startW: width }
      document.body.classList.add('is-col-resizing')
    },
    [width],
  )

  return (
    <div className="workbench" style={{ ['--catalog-width' as string]: `${width}px` }}>
      <aside className="catalog">{left}</aside>
      <div
        className="split-handle"
        role="separator"
        aria-orientation="vertical"
        aria-valuenow={width}
        aria-valuemin={MIN}
        aria-valuemax={MAX}
        title="Drag to resize · double-click to reset"
        onMouseDown={onMouseDown}
        onDoubleClick={() => {
          setWidth(DEFAULT)
          localStorage.setItem(STORAGE_KEY, String(DEFAULT))
        }}
      />
      <section className="pane">{right}</section>
    </div>
  )
}
