import { Modal, message } from 'antd'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { deleteFavoriteRequest, fetchFavoriteRequests, getApiMessage } from '../api/client'
import type { FavoriteRequest } from '../api/types'
import { IconSearch, IconStar, IconTrash } from '../icons'
import { useWorkbench } from '../store/workbench'

export const FAVORITES_RELOAD_EVENT = 'favorites:reload'

export function notifyFavoritesChanged() {
  window.dispatchEvent(new Event(FAVORITES_RELOAD_EVENT))
}

export function FavoritesPanel() {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<FavoriteRequest[]>([])
  const [loading, setLoading] = useState(false)
  const [query, setQuery] = useState('')
  const selectEndpoint = useWorkbench((s) => s.selectEndpoint)
  const setTabBody = useWorkbench((s) => s.setTabBody)
  const setTabParamValues = useWorkbench((s) => s.setTabParamValues)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await fetchFavoriteRequests())
    } catch (error) {
      message.error(getApiMessage(error, 'Could not load favorites'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    const handler = () => {
      void load()
    }
    window.addEventListener(FAVORITES_RELOAD_EVENT, handler)
    return () => window.removeEventListener(FAVORITES_RELOAD_EVENT, handler)
  }, [load])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) {
      return items
    }
    return items.filter(
      (item) =>
        item.name.toLowerCase().includes(q) ||
        item.serviceName.toLowerCase().includes(q) ||
        item.path.toLowerCase().includes(q) ||
        item.method.toLowerCase().includes(q),
    )
  }, [items, query])

  const applyFavorite = (item: FavoriteRequest) => {
    const tabId = `${item.serviceId}:${item.endpointId}`
    selectEndpoint(item.serviceId, item.endpointId)
    if (item.paramValues) {
      setTabParamValues(tabId, item.paramValues)
    }
    if (item.requestBody != null) {
      try {
        setTabBody(tabId, JSON.stringify(item.requestBody, null, 2))
      } catch {
        setTabBody(tabId, String(item.requestBody))
      }
    }
    // Same endpoint already open: force local form refresh via custom event.
    window.dispatchEvent(
      new CustomEvent('favorite:apply', {
        detail: {
          tabId,
          paramValues: item.paramValues ?? {},
          body: item.requestBody,
        },
      }),
    )
    message.success(`Opened «${item.name}»`)
  }

  const remove = (item: FavoriteRequest) => {
    Modal.confirm({
      title: `Remove favorite «${item.name}»?`,
      okText: 'Delete',
      okButtonProps: { danger: true },
      cancelText: 'Cancel',
      onOk: async () => {
        await deleteFavoriteRequest(item.id)
        setItems((current) => current.filter((row) => row.id !== item.id))
        message.success('Removed')
      },
    })
  }

  return (
    <div className={`pins-panel${open ? ' open' : ''}`}>
      <button type="button" className="pins-toggle" onClick={() => setOpen((value) => !value)}>
        <IconStar size={14} filled={items.length > 0} />
        <span>Favorites</span>
        <span className="meta">{loading ? '…' : items.length}</span>
      </button>
      {open ? (
        <div className="pins-body">
          <div className="pins-search">
            <IconSearch size={14} />
            <input
              value={query}
              placeholder="Filter favorites…"
              onChange={(event) => setQuery(event.target.value)}
            />
          </div>
          {filtered.length === 0 ? (
            <p className="hint-line" style={{ margin: '6px 0 0' }}>
              {items.length === 0 ? 'Star a request to save it here.' : 'Nothing matches.'}
            </p>
          ) : (
            <ul className="pins-list">
              {filtered.map((item) => (
                <li key={item.id} className="pins-item">
                  <button
                    type="button"
                    className="pins-main"
                    title={`${item.method} ${item.path}\n${item.serviceName}`}
                    onClick={() => applyFavorite(item)}
                  >
                    <span className="pins-line">
                      <span className="pins-alias">{item.name}</span>
                      <span className="pins-value">
                        {item.method} {item.path}
                      </span>
                    </span>
                    <span className="pins-comment">
                      {item.serviceName}
                      {item.module ? ` · ${item.module}` : ''}
                    </span>
                  </button>
                  <div className="pins-actions">
                    <button type="button" className="icon-btn" title="Remove" onClick={() => remove(item)}>
                      <IconTrash size={13} />
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : null}
    </div>
  )
}
