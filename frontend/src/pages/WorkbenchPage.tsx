import { message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { fetchServices, getApiMessage, refreshSwagger } from '../api/client'
import { prettyJson } from '../api/schema'
import type { CatalogService, ServiceEndpoint } from '../api/types'
import { EnvRegionBar } from '../components/EnvRegionBar'
import { FreeRequestPane } from '../components/FreeRequestPane'
import { PinDialog } from '../components/PinDialog'
import { PinsPanel } from '../components/PinsPanel'
import { RequestPane } from '../components/RequestPane'
import { ResizableWorkbench } from '../components/ResizableWorkbench'
import { ServiceTree } from '../components/ServiceTree'
import { IconPlus, IconRoute, IconTerminal, IconX } from '../icons'
import { useSession, useWorkbench } from '../store/workbench'

export function WorkbenchPage() {
  const [services, setServices] = useState<CatalogService[]>([])
  const [loading, setLoading] = useState(true)
  const [refreshingSwagger, setRefreshingSwagger] = useState(false)
  const canSend = useSession((s) => s.me?.canSend)
  const selectedServiceId = useWorkbench((s) => s.selectedServiceId)
  const selectedEndpointId = useWorkbench((s) => s.selectedEndpointId)
  const environment = useWorkbench((s) => s.environment)
  const regionByServiceId = useWorkbench((s) => s.regionByServiceId)
  const rememberDefaultRegion = useWorkbench((s) => s.rememberDefaultRegion)
  const tabs = useWorkbench((s) => s.tabs)
  const activeTabId = useWorkbench((s) => s.activeTabId)
  const setActiveTab = useWorkbench((s) => s.setActiveTab)
  const closeTab = useWorkbench((s) => s.closeTab)
  const closeAllTabs = useWorkbench((s) => s.closeAllTabs)
  const openFreeTab = useWorkbench((s) => s.openFreeTab)
  const pendingReplay = useWorkbench((s) => s.pendingReplay)
  const setPendingReplay = useWorkbench((s) => s.setPendingReplay)
  const selectEndpoint = useWorkbench((s) => s.selectEndpoint)
  const setTabBody = useWorkbench((s) => s.setTabBody)
  const setEnvironment = useWorkbench((s) => s.setEnvironment)
  const setRegion = useWorkbench((s) => s.setRegion)

  useEffect(() => {
    void fetchServices()
      .then((items) => {
        setServices(items)
        items.forEach(rememberDefaultRegion)
      })
      .finally(() => setLoading(false))
  }, [rememberDefaultRegion])

  useEffect(() => {
    if (!pendingReplay) {
      return
    }

    if (pendingReplay.endpointId && pendingReplay.serviceId) {
      if (services.length === 0) {
        return
      }
      selectEndpoint(pendingReplay.serviceId, pendingReplay.endpointId)
      if (pendingReplay.environment?.trim()) {
        setEnvironment(pendingReplay.environment.trim().toLowerCase())
      }
      if (pendingReplay.regionCode) {
        setRegion(pendingReplay.serviceId, pendingReplay.regionCode)
      }
      const tabId = `${pendingReplay.serviceId}:${pendingReplay.endpointId}`
      window.setTimeout(() => {
        if (pendingReplay.requestBody) {
          setTabBody(tabId, JSON.stringify(pendingReplay.requestBody, null, 2))
        }
      }, 0)
      setPendingReplay(null)
      return
    }

    openFreeTab({
      method: (pendingReplay.method ?? 'GET').toUpperCase(),
      url: pendingReplay.url ?? '',
      body: pendingReplay.requestBody == null ? '' : prettyJson(pendingReplay.requestBody),
      headersText: pendingReplay.requestHeaders
        ? prettyJson(pendingReplay.requestHeaders)
        : '{\n  "Accept": "application/json"\n}',
      proxy: true,
    })
    setPendingReplay(null)
  }, [
    openFreeTab,
    pendingReplay,
    selectEndpoint,
    services.length,
    setEnvironment,
    setPendingReplay,
    setRegion,
    setTabBody,
  ])

  const activeTab = tabs.find((tab) => tab.id === activeTabId)
  const selected = useMemo(
    () => services.find((service) => service.id === selectedServiceId),
    [services, selectedServiceId],
  )
  const endpoint = selected?.endpoints.find((item) => item.id === selectedEndpointId)
  const region = selected ? regionByServiceId[selected.id] ?? selected.defaultRegion ?? undefined : undefined
  const tabTitle =
    activeTab?.kind === 'free'
      ? `Free · ${(activeTab.method || 'GET').toUpperCase()}`
      : selected
        ? `${selected.name}${endpoint?.module ? ` · ${endpoint.module}` : ''}${selected.isRegional && region ? ` · ${region}` : ''} · ${environment}`
        : 'Start'

  const patchEndpoint = (next: ServiceEndpoint) => {
    setServices((current) =>
      current.map((service) =>
        service.id !== selected?.id
          ? service
          : {
              ...service,
              endpoints: service.endpoints.map((item) => (item.id === next.id ? { ...item, ...next } : item)),
            },
      ),
    )
  }

  const onRefreshSwagger = async () => {
    setRefreshingSwagger(true)
    try {
      const result = await refreshSwagger()
      const items = await fetchServices()
      setServices(items)
      items.forEach(rememberDefaultRegion)
      if (result.failed === 0) {
        message.success(`Swagger: ${result.ok} services`)
      } else {
        message.warning(`Swagger: ok ${result.ok}, failed ${result.failed}`)
      }
    } catch (error) {
      message.error(getApiMessage(error, 'Could not refresh swagger'))
    } finally {
      setRefreshingSwagger(false)
    }
  }

  return (
    <>
      <div className="page-head page-head-compact">
        <p className="page-kicker">Request</p>
        <h1 className="page-title">{tabTitle}</h1>
      </div>
      {tabs.length > 0 ? (
        <div className="tabs">
          {tabs.map((tab) => {
            const svc = services.find((item) => item.id === tab.serviceId)
            const ep = svc?.endpoints.find((item) => item.id === tab.endpointId)
            const label =
              tab.kind === 'free'
                ? `${(tab.method || 'GET').toUpperCase()} free`
                : ep
                  ? `${ep.method} ${ep.path}`
                  : (svc?.name ?? 'tab')
            const method = tab.kind === 'free' ? (tab.method || 'GET').toUpperCase() : (ep?.method ?? '')
            const pathLabel = tab.kind === 'free' ? 'free' : (ep?.path ?? (svc?.name ?? 'tab'))
            return (
              <button
                key={tab.id}
                type="button"
                className={`tab${tab.id === activeTabId ? ' on' : ''}`}
                title={label}
                onClick={() => setActiveTab(tab.id)}
              >
                <span className="tab-color" style={{ background: tab.kind === 'free' ? '#8a9bb5' : (svc?.color ?? '#7a8f6a') }} />
                <span className="tab-text">
                  {method ? <span className="tab-method">{method}</span> : null}
                  <span className="tab-path">{pathLabel}</span>
                </span>
                <span
                  className="tab-close"
                  onClick={(event) => {
                    event.stopPropagation()
                    closeTab(tab.id)
                  }}
                >
                  <IconX size={12} />
                </span>
              </button>
            )
          })}
          <button
            type="button"
            className="btn btn-ghost btn-compact tabs-close-all"
            title="Close all tabs"
            onClick={() => closeAllTabs()}
          >
            Close all
          </button>
        </div>
      ) : null}
      <ResizableWorkbench
        left={
          <div className="catalog-stack">
            <PinsPanel />
            <ServiceTree
              services={services}
              loading={loading}
              onOpenFree={canSend ? () => openFreeTab() : undefined}
              onRefreshSwagger={() => void onRefreshSwagger()}
              refreshingSwagger={refreshingSwagger}
            />
          </div>
        }
        right={
          <>
            {activeTab?.kind === 'free' ? (
              <div className="pane-bar">
                <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
                  <span className="meta">free request</span>
                  <span className="meta">{activeTab.proxy ? 'proxy' : 'browser'}</span>
                </div>
                <button type="button" className="btn btn-ghost btn-compact" onClick={() => openFreeTab()}>
                  <IconPlus size={14} />
                  New
                </button>
              </div>
            ) : (
              <EnvRegionBar service={selected} />
            )}
            <div className="pane-body">
              {activeTab?.kind === 'free' ? (
                <FreeRequestPane key={activeTab.id} tabId={activeTab.id} />
              ) : selected && endpoint ? (
                <RequestPane
                  key={endpoint.id}
                  service={selected}
                  endpoint={endpoint}
                  onEndpointPatch={patchEndpoint}
                />
              ) : selected ? (
                <>
                  <div className="meta-row">
                    <span className="meta">{selected.authType}</span>
                    {selected.modules.length > 0 ? (
                      <span className="meta" title={selected.modules.map((m) => `${m.name}:${m.authType}`).join(', ')}>
                        modules: {selected.modules.map((m) => m.authType).join(' / ')}
                      </span>
                    ) : null}
                    {selected.isRegional ? <span className="meta">regional</span> : null}
                    <span className="meta">{selected.proxy ? 'proxy' : 'browser'}</span>
                  </div>
                  <p style={{ color: 'var(--mute)', maxWidth: '58ch', lineHeight: 1.55, marginTop: 0 }}>
                    {selected.endpointCount
                      ? 'Pick an endpoint on the left — a tab with the form will open.'
                      : 'Contract is empty. Click Swagger in the catalog toolbar to refresh.'}
                  </p>
                </>
              ) : (
                <div className="empty">
                  <IconTerminal size={32} />
                  <h2>Select a service</h2>
                  <p>Tree: service → tag → method and path. Or open Free request and paste any URL.</p>
                  <p style={{ marginTop: 12, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                    <IconRoute size={16} />
                    Double-click also opens a tab
                  </p>
                  {canSend ? (
                    <button
                      type="button"
                      className="btn btn-primary"
                      style={{ width: 'auto', marginTop: 16, paddingInline: 16 }}
                      onClick={() => openFreeTab()}
                    >
                      <IconPlus size={16} />
                      Free request
                    </button>
                  ) : null}
                </div>
              )}
            </div>
          </>
        }
      />
      <PinDialog />
    </>
  )
}
