import { useEffect, useState } from 'react'
import { fetchContractDiff, fetchSnapshots, getApiMessage } from '../api/client'
import type { ContractDiff, SnapshotItem } from '../api/types'

type Props = {
  serviceId: number
}

function formatWhen(value: string | null): string {
  if (!value) {
    return '—'
  }
  return new Date(value).toLocaleString('en-GB', { hour12: false })
}

export function ContractDiffPanel({ serviceId }: Props) {
  const [snapshots, setSnapshots] = useState<SnapshotItem[]>([])
  const [fromId, setFromId] = useState<number | undefined>()
  const [toId, setToId] = useState<number | undefined>()
  const [diff, setDiff] = useState<ContractDiff | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void fetchSnapshots(serviceId)
      .then((items) => {
        setSnapshots(items)
        const to = items[0]
        const from = items.find((item) => item.id !== to?.id && (item.module ?? '') === (to?.module ?? ''))
        setToId(to?.id)
        setFromId(from?.id)
      })
      .catch((err) => setError(getApiMessage(err, 'Could not load snapshots')))
  }, [serviceId])

  useEffect(() => {
    if (!serviceId) {
      return
    }
    void fetchContractDiff(serviceId, fromId, toId)
      .then((item) => {
        setDiff(item)
        setError(null)
      })
      .catch((err) => setError(getApiMessage(err, 'No snapshots to compare')))
  }, [serviceId, fromId, toId])

  const empty = !diff || (diff.added.length === 0 && diff.removed.length === 0 && diff.changed.length === 0)

  return (
    <div className="diff-panel">
      <div className="diff-toolbar">
        <label>
          Before
          <select value={fromId ?? ''} onChange={(event) => setFromId(event.target.value ? Number(event.target.value) : undefined)}>
            <option value="">—</option>
            {snapshots.map((item) => (
              <option key={item.id} value={item.id}>
                #{item.id}
                {item.module ? ` · ${item.module}` : ''} · {formatWhen(item.fetchedAt)}
              </option>
            ))}
          </select>
        </label>
        <label>
          After
          <select value={toId ?? ''} onChange={(event) => setToId(event.target.value ? Number(event.target.value) : undefined)}>
            {snapshots.map((item) => (
              <option key={item.id} value={item.id}>
                #{item.id}
                {item.module ? ` · ${item.module}` : ''} · {formatWhen(item.fetchedAt)}
              </option>
            ))}
          </select>
        </label>
      </div>
      {error ? <p className="hint-line">{error}</p> : null}
      {diff && !error ? (
        <p className="hint-line">
          {formatWhen(diff.fromFetchedAt)} → {formatWhen(diff.toFetchedAt)} · +{diff.added.length} −{diff.removed.length} ~{diff.changed.length}
        </p>
      ) : null}
      {empty && !error ? <p className="hint-line">Contract did not change, or there is only one snapshot.</p> : null}
      {diff?.added.map((item) => (
        <div key={`a-${item.method}-${item.path}`} className="diff-row added">
          <span className={`method-badge method-${item.method.toLowerCase()}`}>{item.method}</span>
          <span>{item.path}</span>
          <em>added</em>
        </div>
      ))}
      {diff?.removed.map((item) => (
        <div key={`r-${item.method}-${item.path}`} className="diff-row removed">
          <span className={`method-badge method-${item.method.toLowerCase()}`}>{item.method}</span>
          <span>{item.path}</span>
          <em>removed</em>
        </div>
      ))}
      {diff?.changed.map((item) => (
        <div key={`c-${item.method}-${item.path}`} className="diff-row changed">
          <span className={`method-badge method-${item.method.toLowerCase()}`}>{item.method}</span>
          <span>{item.path}</span>
          <em>{item.detail}</em>
        </div>
      ))}
    </div>
  )
}
