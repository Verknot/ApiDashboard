import { Input, Table } from 'antd'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { fetchHistory } from '../api/client'
import type { HistoryItem } from '../api/types'
import { IconInbox } from '../icons'
import { useWorkbench } from '../store/workbench'

export function HistoryPage() {
  const [rows, setRows] = useState<HistoryItem[]>([])
  const [query, setQuery] = useState('')
  const setPendingReplay = useWorkbench((s) => s.setPendingReplay)
  const navigate = useNavigate()

  useEffect(() => {
    void fetchHistory({ take: 100, q: query || undefined }).then(setRows)
  }, [query])

  return (
    <>
      <p className="page-kicker">Journal</p>
      <h1 className="page-title">Request history</h1>
      <div className="search" style={{ maxWidth: 360, marginBottom: 16 }}>
        <Input
          value={query}
          placeholder="URL, service, body"
          onChange={(event) => setQuery(event.target.value)}
          allowClear
        />
      </div>
      {rows.length === 0 ? (
        <div className="empty" style={{ marginLeft: 0 }}>
          <IconInbox size={32} />
          <h2>Nothing yet</h2>
          <p>Rows appear after Send. Last 100 per user; response body is truncated at 256 KB.</p>
        </div>
      ) : (
        <Table
          rowKey="id"
          size="small"
          pagination={false}
          dataSource={rows}
          columns={[
            {
              title: '',
              width: 28,
              render: (_: unknown, row: HistoryItem) => (
                <span className="service-color" style={{ background: row.serviceColor ?? '#7a8f6a', height: 16 }} />
              ),
            },
            {
              title: 'Time',
              dataIndex: 'createdAt',
              width: 160,
              render: (value: string) => new Date(value).toLocaleString('en-GB', { hour12: false }),
            },
            { title: 'Method', dataIndex: 'method', width: 80 },
            { title: 'URL', dataIndex: 'url', ellipsis: true },
            {
              title: 'Status',
              dataIndex: 'responseStatus',
              width: 80,
              render: (status: number | null) => (
                <span className={`status-pill ${status && status < 300 ? 's2' : status && status < 500 ? 's4' : 's5'}`}>
                  {status ?? 'ERR'}
                </span>
              ),
            },
            { title: 'ms', dataIndex: 'responseTimeMs', width: 70 },
            {
              title: '',
              width: 90,
              render: (_: unknown, row: HistoryItem) => (
                <button
                  type="button"
                  className="btn btn-ghost"
                  style={{ height: 32, padding: '0 10px' }}
                  onClick={() => {
                    setPendingReplay(row)
                    navigate('/')
                  }}
                >
                  Replay
                </button>
              ),
            },
          ]}
        />
      )}
    </>
  )
}
