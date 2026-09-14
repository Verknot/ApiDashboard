import { Input, Modal, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  deleteTemplate,
  directSend,
  downloadDto,
  fetchServiceToken,
  fetchTemplates,
  getApiMessage,
  proxySend,
  saveHistory,
  saveTemplate,
  saveUserTags,
} from '../api/client'
import { isBlankRequestBody, prettyJson, seedRequestBody } from '../api/schema'
import type { CatalogService, HistoryItem, RequestTemplate, ServiceEndpoint } from '../api/types'
import { IconDeviceFloppy, IconFileCode, IconKey, IconRefresh, IconSend, IconTag, IconTrash } from '../icons'
import { resolveBaseUrl, sendModeHint, tokenScopeKey, useSession, useWorkbench } from '../store/workbench'
import { ContractDiffPanel } from './ContractDiffPanel'
import { EndpointHistory } from './EndpointHistory'
import { ResponseHeaderList } from './ResponseHeaderList'

type Props = {
  service: CatalogService
  endpoint: ServiceEndpoint
  onEndpointPatch: (endpoint: ServiceEndpoint) => void
}

type SendResult = {
  status: number | null
  timeMs: number
  body: string
  error?: string
  headers: Record<string, string>
}

function hasBody(method: string): boolean {
  return !['GET', 'HEAD'].includes(method.toUpperCase())
}

function statusTone(status: number | null, error?: string): string {
  if (error || status == null) {
    return 's5'
  }
  if (status >= 200 && status < 300) {
    return 's2'
  }
  if (status >= 400 && status < 500) {
    return 's4'
  }
  return 's5'
}

function stringMap(value: Record<string, unknown> | null | undefined): Record<string, string> {
  if (!value) {
    return {}
  }
  return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, item == null ? '' : String(item)]))
}

export function RequestPane({ service, endpoint, onEndpointPatch }: Props) {
  const me = useSession((s) => s.me)
  const sendViaProxy = service.proxy
  const environment = useWorkbench((s) => s.environment)
  const regionByServiceId = useWorkbench((s) => s.regionByServiceId)
  const setTabBody = useWorkbench((s) => s.setTabBody)
  const setServiceToken = useWorkbench((s) => s.setServiceToken)
  const tokenByScope = useWorkbench((s) => s.tokenByScope)
  const openTab = useWorkbench((s) => s.openTab)
  const tabKey = `${service.id}:${endpoint.id}`
  const generatedBody = useMemo(
    () => seedRequestBody(endpoint),
    [endpoint.id, endpoint.method, endpoint.requestExample, endpoint.requestSchema],
  )
  const [draftBody, setDraftBody] = useState(() => {
    const stored = useWorkbench.getState().tabs.find((item) => item.id === tabKey)?.body ?? ''
    return isBlankRequestBody(stored) ? seedRequestBody(endpoint) : stored
  })
  const region = regionByServiceId[service.id] ?? service.defaultRegion ?? undefined
  const baseUrl = resolveBaseUrl(service, environment, region, endpoint.module)
  const defaultUrl = endpoint && baseUrl ? `${baseUrl.replace(/\/$/, '')}${endpoint.path}` : (baseUrl ?? '')

  const [url, setUrl] = useState(defaultUrl)
  const [tagDraft, setTagDraft] = useState('')
  const [templates, setTemplates] = useState<RequestTemplate[]>([])
  const [sending, setSending] = useState(false)
  const [result, setResult] = useState<SendResult | null>(null)
  const [showDiff, setShowDiff] = useState(false)
  const [historyTick, setHistoryTick] = useState(0)
  const [templateOpen, setTemplateOpen] = useState(false)
  const [templateName, setTemplateName] = useState('')
  const [fetchingToken, setFetchingToken] = useState(false)

  const body = draftBody
  const token = (tokenByScope ?? {})[tokenScopeKey(service.id, environment, region)] ?? ''

  useEffect(() => {
    openTab(service.id, endpoint.id)
  }, [endpoint.id, openTab, service.id])

  useEffect(() => {
    setUrl(defaultUrl)
    setResult(null)
  }, [defaultUrl, endpoint.id, environment, region])

  useEffect(() => {
    const stored = useWorkbench.getState().tabs.find((item) => item.id === tabKey)?.body ?? ''
    if (isBlankRequestBody(stored)) {
      setDraftBody(generatedBody)
      if (generatedBody) {
        setTabBody(tabKey, generatedBody)
      }
      return
    }
    setDraftBody(stored)
  }, [endpoint.id, generatedBody, setTabBody, tabKey])

  useEffect(() => {
    void fetchTemplates(endpoint.id).then(setTemplates).catch(() => setTemplates([]))
  }, [endpoint.id])

  const userTags = endpoint.userTags ?? []
  const jsonError = useMemo(() => {
    if (!body.trim() || !hasBody(endpoint.method)) {
      return null
    }
    try {
      JSON.parse(body)
      return null
    } catch (error) {
      return error instanceof Error ? error.message : 'Invalid JSON'
    }
  }, [body, endpoint.method])

  const send = async () => {
    if (!me?.canSend) {
      message.warning('Send is not available for this role')
      return
    }
    if (jsonError) {
      message.error(jsonError)
      return
    }

    const run = async () => {
      setSending(true)
      const headers: Record<string, string> = { Accept: 'application/json' }
      if (hasBody(endpoint.method)) {
        headers['Content-Type'] = 'application/json'
      }
      if (service.authType === 'token' && token.trim()) {
        headers.Authorization = `Bearer ${token.trim()}`
      }

      try {
        const payload = {
          serviceId: service.id,
          url,
          method: endpoint.method,
          headers,
          body: hasBody(endpoint.method) ? body : null,
        }
        const relay = sendViaProxy ? await proxySend(payload) : await directSend(payload)
        let pretty = relay.body
        try {
          pretty = JSON.stringify(JSON.parse(relay.body), null, 2)
        } catch {
          pretty = relay.body
        }
        setResult({
          status: relay.status,
          timeMs: relay.timeMs,
          body: pretty,
          error: relay.error ?? undefined,
          headers: relay.headers ?? {},
        })
        void saveHistory({
          serviceId: service.id,
          endpointId: endpoint.id,
          environment,
          regionCode: region ?? '',
          url,
          method: endpoint.method,
          requestHeaders: JSON.stringify(headers),
          requestBody: hasBody(endpoint.method) ? body : null,
          responseStatus: relay.status,
          responseBody: pretty,
          responseTimeMs: relay.timeMs,
          responseHeaders: JSON.stringify(relay.headers ?? {}),
        }).then(() => setHistoryTick((tick) => tick + 1))
      } catch (error) {
        const text = getApiMessage(error, 'Send failed')
        setResult({ status: null, timeMs: 0, body: text, error: text, headers: {} })
      } finally {
        setSending(false)
      }
    }

    if (environment === 'prod') {
      Modal.confirm({
        title: 'Send to prod',
        content: `${service.name}${region ? ` · ${region}` : ''}\n${url}`,
        okText: 'Send',
        cancelText: 'Cancel',
        onOk: run,
      })
      return
    }

    await run()
  }

  const applyBody = (next: string) => {
    setDraftBody(next)
    setTabBody(tabKey, next)
  }

  const replayHistory = (item: HistoryItem) => {
    if (item.url) {
      setUrl(item.url)
    }
    if (item.requestBody != null) {
      applyBody(prettyJson(item.requestBody))
    }
    let pretty = item.responseBody ?? ''
    try {
      pretty = JSON.stringify(JSON.parse(pretty), null, 2)
    } catch {
      pretty = item.responseBody ?? ''
    }
    setResult({
      status: item.responseStatus,
      timeMs: item.responseTimeMs ?? 0,
      body: pretty,
      headers: stringMap(item.responseHeaders),
    })
  }

  const removeTemplate = (item: RequestTemplate) => {
    Modal.confirm({
      title: 'Delete template',
      content: item.name,
      okText: 'Delete',
      okButtonProps: { danger: true },
      cancelText: 'Cancel',
      onOk: async () => {
        await deleteTemplate(item.id)
        setTemplates((current) => current.filter((row) => row.id !== item.id))
        message.success('Deleted')
      },
    })
  }

  const bodyMatchesSpec = body.trim() === generatedBody.trim()

  return (
    <>
      <div className="endpoint-title">
        <span className={`method-badge method-${endpoint.method.toLowerCase()}`}>{endpoint.method}</span>
        <span>{endpoint.path}</span>
      </div>
      {endpoint.description ? (
        <p style={{ color: 'var(--mute)', maxWidth: '58ch', lineHeight: 1.55, marginTop: 0 }}>{endpoint.description}</p>
      ) : null}

      <div className="tag-row">
        <IconTag size={14} />
        {(endpoint.tags ?? []).map((tag) => (
          <span key={`o-${tag}`} className="tag-chip">
            {tag}
          </span>
        ))}
        {userTags.map((tag) => (
          <button
            key={`u-${tag}`}
            type="button"
            className="tag-chip user"
            title="Remove your tag"
            onClick={async () => {
              const next = userTags.filter((item) => item !== tag)
              onEndpointPatch(await saveUserTags(endpoint.id, next))
            }}
          >
            {tag} ×
          </button>
        ))}
        <form
          className="tag-add"
          onSubmit={async (event) => {
            event.preventDefault()
            const value = tagDraft.trim()
            if (!value) {
              return
            }
            onEndpointPatch(await saveUserTags(endpoint.id, [...userTags, value]))
            setTagDraft('')
          }}
        >
          <input value={tagDraft} onChange={(event) => setTagDraft(event.target.value)} placeholder="your tag" />
        </form>
      </div>

      <label className="field-label">URL</label>
      <input className="url-input" value={url} onChange={(event) => setUrl(event.target.value)} />
      <p className="hint-line send-hint">{sendModeHint(sendViaProxy, service.authType, url)}</p>

      {service.authType === 'token' ? (
        <>
          <label className="field-label">Bearer token</label>
          <div className="token-row">
            <input
              className="url-input"
              type="password"
              value={token}
              placeholder="not stored in JWT, session tab only"
              onChange={(event) => setServiceToken(service.id, environment, region, event.target.value)}
            />
            {service.canFetchToken && me?.canSend ? (
              <button
                type="button"
                className="btn btn-ghost"
                disabled={fetchingToken}
                title="Fetch accessToken"
                onClick={async () => {
                  setFetchingToken(true)
                  try {
                    const next = await fetchServiceToken(service.id, environment, region)
                    setServiceToken(service.id, environment, region, next)
                    message.success('Token fetched')
                  } catch (error) {
                    message.error(getApiMessage(error, 'Could not fetch token'))
                  } finally {
                    setFetchingToken(false)
                  }
                }}
              >
                <IconKey size={16} />
                {fetchingToken ? 'Fetching…' : 'Get'}
              </button>
            ) : null}
          </div>
        </>
      ) : null}

      {hasBody(endpoint.method) ? (
        <>
          <div className="editor-split">
            <label className="field-label">Body</label>
            <div className="body-actions">
              {templates.map((item) => (
                <div key={item.id} className="tpl-chip">
                  <button
                    type="button"
                    className="tpl-chip-use"
                    title="Apply template"
                    onClick={() => applyBody(prettyJson(item.templateBody))}
                  >
                    {item.name}
                  </button>
                  <button
                    type="button"
                    className="tpl-chip-del"
                    title="Delete template"
                    onClick={() => removeTemplate(item)}
                  >
                    <IconTrash size={12} />
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="btn btn-ghost btn-compact"
                title="Reset body to spec example"
                disabled={bodyMatchesSpec}
                onClick={() => applyBody(generatedBody)}
              >
                <IconRefresh size={14} />
                Reset
              </button>
              <button
                type="button"
                className="btn btn-ghost btn-compact"
                onClick={() => {
                  setTemplateName(endpoint.operationId || `Template ${templates.length + 1}`)
                  setTemplateOpen(true)
                }}
              >
                <IconDeviceFloppy size={14} />
                Save
              </button>
            </div>
          </div>
          <Input.TextArea
            className="json-body"
            value={body}
            onChange={(event) => applyBody(event.target.value)}
            autoSize={{ minRows: 8, maxRows: 18 }}
            spellCheck={false}
          />
          {jsonError ? <p className="json-error">{jsonError}</p> : null}
        </>
      ) : null}

      <div className="send-row">
        <button
          type="button"
          className="btn btn-primary"
          style={{ width: 'auto', paddingInline: 18 }}
          disabled={sending || !me?.canSend}
          onClick={() => void send()}
        >
          <IconSend size={16} />
          {sending ? 'Sending…' : sendViaProxy ? 'Send · proxy' : 'Send · browser'}
        </button>
        {me?.canGenerateDto ? (
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() =>
              void downloadDto(endpoint.id).catch((error) => message.error(getApiMessage(error, 'DTO failed')))
            }
          >
            <IconFileCode size={16} />
            DTO
          </button>
        ) : null}
        <button type="button" className="btn btn-ghost" onClick={() => setShowDiff((value) => !value)}>
          Diff
        </button>
      </div>

      {result ? (
        <section className="response-card">
          <div className="response-head">
            <span className={`status-pill ${statusTone(result.status, result.error)}`}>
              {result.status ?? 'ERR'}
            </span>
            <span>{result.timeMs} ms</span>
            {result.error ? <span>network</span> : null}
          </div>
          {Object.keys(result.headers).length > 0 ? (
            <ResponseHeaderList headers={result.headers} splunkUrl={service.splunkUrl} />
          ) : null}
          <pre>{result.body || ' '}</pre>
        </section>
      ) : null}

      <EndpointHistory
        endpointId={endpoint.id}
        reloadToken={historyTick}
        splunkUrl={service.splunkUrl}
        onReplay={replayHistory}
      />

      {showDiff ? (
        <section className="diff-wrap">
          <p className="catalog-head">Contract</p>
          <ContractDiffPanel serviceId={service.id} />
        </section>
      ) : null}

      <Modal
        title="Save template"
        open={templateOpen}
        okText="Save"
        cancelText="Cancel"
        onCancel={() => setTemplateOpen(false)}
        onOk={async () => {
          const name = templateName.trim()
          if (!name) {
            message.error('Enter a name')
            return Promise.reject()
          }
          let payload: unknown = {}
          try {
            payload = JSON.parse(body.trim() || seedRequestBody(endpoint) || '{}')
          } catch {
            message.error('Body must be valid JSON')
            return Promise.reject()
          }
          try {
            const created = await saveTemplate(endpoint.id, name, payload)
            setTemplates((current) => [created, ...current])
            setTemplateOpen(false)
            message.success('Saved')
          } catch (error) {
            message.error(getApiMessage(error, 'Could not save template'))
            return Promise.reject()
          }
        }}
      >
        <p className="hint-line">Name appears as a chip above the editor. Use × on the chip to delete.</p>
        <Input
          value={templateName}
          autoFocus
          placeholder="e.g. happy-path"
          onChange={(event) => setTemplateName(event.target.value)}
          onPressEnter={() => {
            const ok = document.querySelector('.ant-modal-footer .ant-btn-primary') as HTMLButtonElement | null
            ok?.click()
          }}
        />
      </Modal>
    </>
  )
}
