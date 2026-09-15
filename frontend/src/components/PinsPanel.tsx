import { Input, Modal, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import type { UserPin } from '../api/types'
import { IconMapPin, IconPencil, IconSearch, IconTrash } from '../icons'
import { getApiMessage, usePins } from '../store/pins'

function shortValue(value: string): string {
  const v = value.trim()
  if (v.length <= 18) {
    return v
  }
  return `${v.slice(0, 6)}…${v.slice(-4)}`
}

function matchesQuery(pin: UserPin, query: string): boolean {
  const q = query.trim().toLowerCase()
  if (!q) {
    return true
  }
  return (
    pin.alias.toLowerCase().includes(q) ||
    pin.value.toLowerCase().includes(q) ||
    (pin.comment ?? '').toLowerCase().includes(q) ||
    (pin.sourceKey ?? '').toLowerCase().includes(q)
  )
}

async function applyPin(pin: UserPin, requestApply: (pin: UserPin) => void) {
  requestApply(pin)
  try {
    await navigator.clipboard.writeText(pin.value)
    message.success(`Applied · copied «${pin.alias}»`)
  } catch {
    message.success(`Applied «${pin.alias}»`)
  }
}

function PinRow({
  pin,
  onApply,
  onEdit,
  onDelete,
}: {
  pin: UserPin
  onApply: () => void
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <li className="pins-item">
      <button
        type="button"
        className="pins-main"
        title={`${pin.alias} = ${pin.value}${pin.comment ? `\n${pin.comment}` : ''}\nClick to apply / copy`}
        onClick={() => void onApply()}
      >
        <span className="pins-line">
          <span className="pins-alias">{pin.alias}</span>
          <span className="pins-value">{shortValue(pin.value)}</span>
        </span>
        {pin.comment ? <span className="pins-comment">{pin.comment}</span> : null}
      </button>
      <div className="pins-actions">
        <button type="button" className="icon-btn" title="Edit" onClick={onEdit}>
          <IconPencil size={13} />
        </button>
        <button type="button" className="icon-btn" title="Delete" onClick={onDelete}>
          <IconTrash size={13} />
        </button>
      </div>
    </li>
  )
}

export function PinsPanel() {
  const pins = usePins((s) => s.pins)
  const loading = usePins((s) => s.loading)
  const load = usePins((s) => s.load)
  const openCreate = usePins((s) => s.openCreate)
  const openEdit = usePins((s) => s.openEdit)
  const remove = usePins((s) => s.remove)
  const requestApply = usePins((s) => s.requestApply)
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [managerOpen, setManagerOpen] = useState(false)
  const [managerQuery, setManagerQuery] = useState('')

  useEffect(() => {
    void load().catch((error) => message.error(getApiMessage(error, 'Could not load pins')))
  }, [load])

  const filtered = useMemo(() => pins.filter((pin) => matchesQuery(pin, query)), [pins, query])
  const managed = useMemo(
    () => pins.filter((pin) => matchesQuery(pin, managerQuery)),
    [pins, managerQuery],
  )

  const confirmDelete = (pin: UserPin) => {
    Modal.confirm({
      title: `Delete pin «${pin.alias}»?`,
      okText: 'Delete',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await remove(pin.id)
          message.success('Deleted')
        } catch (error) {
          message.error(getApiMessage(error, 'Could not delete'))
        }
      },
    })
  }

  return (
    <div className="pins-panel">
      <div className="pins-head">
        <button type="button" className="pins-toggle" onClick={() => setOpen((value) => !value)}>
          <span className={`tree-chevron${open ? ' open' : ''}`} aria-hidden>
            ▸
          </span>
          <IconMapPin size={14} />
          <span className="catalog-head" style={{ margin: 0 }}>
            Pinned
          </span>
          <span className="meta">{pins.length}</span>
        </button>
        <div className="pins-head-actions">
          {pins.length > 0 ? (
            <button
              type="button"
              className="btn btn-ghost btn-compact"
              title="Browse all pins"
              onClick={() => setManagerOpen(true)}
            >
              All
            </button>
          ) : null}
          <button
            type="button"
            className="btn btn-ghost btn-compact"
            title="Pin a value"
            onClick={() => openCreate({ value: '', alias: '' })}
          >
            +
          </button>
        </div>
      </div>

      {open ? (
        loading && pins.length === 0 ? (
          <p className="hint-line" style={{ margin: '4px 0 0' }}>
            Loading…
          </p>
        ) : pins.length === 0 ? (
          <p className="hint-line" style={{ margin: '4px 0 0' }}>
            Pin ids from response or params (as + comment).
          </p>
        ) : (
          <>
            {pins.length > 4 ? (
              <div className="pins-search">
                <IconSearch size={13} />
                <input
                  value={query}
                  placeholder="Filter alias, comment, value"
                  onChange={(event) => setQuery(event.target.value)}
                />
              </div>
            ) : null}
            {filtered.length === 0 ? (
              <p className="hint-line" style={{ margin: '6px 0 0' }}>
                Nothing matches
              </p>
            ) : (
              <ul className="pins-list">
                {filtered.map((pin) => (
                  <PinRow
                    key={pin.id}
                    pin={pin}
                    onApply={() => void applyPin(pin, requestApply)}
                    onEdit={() => openEdit(pin)}
                    onDelete={() => confirmDelete(pin)}
                  />
                ))}
              </ul>
            )}
          </>
        )
      ) : null}

      <Modal
        title={`Pinned (${pins.length})`}
        open={managerOpen}
        onCancel={() => {
          setManagerOpen(false)
          setManagerQuery('')
        }}
        footer={null}
        width={560}
        destroyOnClose
      >
        <Input
          allowClear
          prefix={<IconSearch size={14} />}
          placeholder="Search alias, comment, value"
          value={managerQuery}
          onChange={(event) => setManagerQuery(event.target.value)}
          style={{ marginBottom: 12 }}
        />
        {managed.length === 0 ? (
          <p className="hint-line">Nothing found</p>
        ) : (
          <ul className="pins-manager-list">
            {managed.map((pin) => (
              <li key={pin.id} className="pins-manager-item">
                <button
                  type="button"
                  className="pins-manager-main"
                  onClick={() => {
                    void applyPin(pin, requestApply)
                    setManagerOpen(false)
                  }}
                >
                  <span className="pins-alias">{pin.alias}</span>
                  <span className="pins-manager-value">{pin.value}</span>
                  {pin.comment ? <span className="pins-comment">{pin.comment}</span> : null}
                </button>
                <div className="pins-actions pins-actions-always">
                  <button
                    type="button"
                    className="icon-btn"
                    title="Edit"
                    onClick={() => {
                      openEdit(pin)
                      setManagerOpen(false)
                    }}
                  >
                    <IconPencil size={13} />
                  </button>
                  <button type="button" className="icon-btn" title="Delete" onClick={() => confirmDelete(pin)}>
                    <IconTrash size={13} />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </div>
  )
}
