import { useEffect, useMemo, useState } from 'react'
import { allEndpointTags } from '../api/schema'
import type { CatalogService, ServiceEndpoint } from '../api/types'
import { IconChevronRight, IconCloudOff, IconPlus, IconRefresh, IconSearch, IconStar, IconX } from '../icons'
import { useFavorites, useWorkbench } from '../store/workbench'

type Props = {
  services: CatalogService[]
  loading?: boolean
  onOpenFree?: () => void
  onRefreshSwagger?: () => void
  refreshingSwagger?: boolean
}

type TaggedEndpoints = {
  tag: string
  endpoints: ServiceEndpoint[]
}

type ModuleGroup = {
  module: string
  tags: TaggedEndpoints[]
}

type SearchTab = {
  id: string
  label: string
  query: string
}

const SEARCH_TABS_KEY = 'api-workbench-catalog-search-tabs'

function newSearchTab(index: number, query = ''): SearchTab {
  return {
    id: `search-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    label: `Search ${index}`,
    query,
  }
}

function loadSearchTabs(): { tabs: SearchTab[]; activeId: string } {
  try {
    const raw = sessionStorage.getItem(SEARCH_TABS_KEY)
    if (!raw) {
      const tab = newSearchTab(1)
      return { tabs: [tab], activeId: tab.id }
    }
    const parsed = JSON.parse(raw) as { tabs?: SearchTab[]; activeId?: string }
    const tabs = Array.isArray(parsed.tabs)
      ? parsed.tabs
          .filter((item) => item && typeof item.id === 'string')
          .map((item, index) => ({
            id: item.id,
            label: typeof item.label === 'string' && item.label.trim() ? item.label : `Search ${index + 1}`,
            query: typeof item.query === 'string' ? item.query : '',
          }))
      : []
    if (tabs.length === 0) {
      const tab = newSearchTab(1)
      return { tabs: [tab], activeId: tab.id }
    }
    const activeId = tabs.some((item) => item.id === parsed.activeId) ? (parsed.activeId as string) : tabs[0].id
    return { tabs, activeId }
  } catch {
    const tab = newSearchTab(1)
    return { tabs: [tab], activeId: tab.id }
  }
}

function endpointsOf(service: CatalogService): ServiceEndpoint[] {
  return service.endpoints ?? []
}

function groupByTag(endpoints: ServiceEndpoint[]): TaggedEndpoints[] {
  const groups = new Map<string, ServiceEndpoint[]>()
  for (const endpoint of endpoints) {
    for (const tag of allEndpointTags(endpoint.tags, endpoint.userTags)) {
      const list = groups.get(tag) ?? []
      list.push(endpoint)
      groups.set(tag, list)
    }
  }
  return [...groups.entries()].map(([tag, items]) => ({ tag, endpoints: items }))
}

function groupByModule(
  endpoints: ServiceEndpoint[],
  preferredOrder: Array<string | { name: string }> = [],
): ModuleGroup[] | null {
  const named = endpoints.some((item) => (item.module ?? '').trim().length > 0)
  if (!named) {
    return null
  }
  const groups = new Map<string, ServiceEndpoint[]>()
  for (const endpoint of endpoints) {
    const module = (endpoint.module ?? '').trim() || 'swagger'
    const list = groups.get(module) ?? []
    list.push(endpoint)
    groups.set(module, list)
  }
  const orderNames = preferredOrder.map((item) => (typeof item === 'string' ? item : item.name))
  const rank = new Map(orderNames.map((name, index) => [name.toLowerCase(), index]))
  return [...groups.entries()]
    .sort(([a], [b]) => {
      const ia = rank.get(a.toLowerCase())
      const ib = rank.get(b.toLowerCase())
      if (ia != null && ib != null) {
        return ia - ib
      }
      if (ia != null) {
        return -1
      }
      if (ib != null) {
        return 1
      }
      return a.localeCompare(b, 'ru')
    })
    .map(([module, items]) => ({ module, tags: groupByTag(items) }))
}

function matchesQuery(service: CatalogService, query: string): { serviceHit: boolean; endpoints: ServiceEndpoint[] } {
  const q = query.trim().toLowerCase()
  const endpoints = endpointsOf(service)
  if (!q) {
    return { serviceHit: true, endpoints }
  }

  const serviceHit =
    service.name.toLowerCase().includes(q) ||
    (service.description ?? '').toLowerCase().includes(q) ||
    service.regions.some((region) => region.code.includes(q) || region.label.toLowerCase().includes(q))

  const matchingEndpoints = endpoints.filter((endpoint) => endpointMatchesQuery(endpoint, q))

  return { serviceHit, endpoints: serviceHit ? endpoints : matchingEndpoints }
}

function endpointMatchesQuery(endpoint: ServiceEndpoint, q: string): boolean {
  return (
    endpoint.path.toLowerCase().includes(q) ||
    endpoint.method.toLowerCase().includes(q) ||
    (endpoint.module ?? '').toLowerCase().includes(q) ||
    (endpoint.description ?? '').toLowerCase().includes(q) ||
    (endpoint.operationId ?? '').toLowerCase().includes(q) ||
    allEndpointTags(endpoint.tags, endpoint.userTags).some((tag) => tag.toLowerCase().includes(q))
  )
}

function filterEndpoints(endpoints: ServiceEndpoint[], query: string): ServiceEndpoint[] {
  const q = query.trim().toLowerCase()
  if (!q) {
    return endpoints
  }
  return endpoints.filter((endpoint) => endpointMatchesQuery(endpoint, q))
}

function methodClass(method: string): string {
  return `method-badge method-${method.toLowerCase()}`
}

function tagKey(serviceId: number, module: string, tag: string): string {
  return `${serviceId}:${module}:${tag}`
}

function moduleKey(serviceId: number, module: string): string {
  return `${serviceId}::${module}`
}

export function ServiceTree({ services, loading, onOpenFree, onRefreshSwagger, refreshingSwagger }: Props) {
  const initialSearch = useMemo(() => loadSearchTabs(), [])
  const [searchTabs, setSearchTabs] = useState<SearchTab[]>(initialSearch.tabs)
  const [activeSearchTabId, setActiveSearchTabId] = useState(initialSearch.activeId)
  const [serviceQueries, setServiceQueries] = useState<Record<number, string>>({})
  const [collapsedServices, setCollapsedServices] = useState<number[]>([])
  const [collapsedTags, setCollapsedTags] = useState<string[]>([])
  const [collapsedModules, setCollapsedModules] = useState<string[]>([])
  const selectedServiceId = useWorkbench((s) => s.selectedServiceId)
  const selectedEndpointId = useWorkbench((s) => s.selectedEndpointId)
  const selectService = useWorkbench((s) => s.selectService)
  const selectEndpoint = useWorkbench((s) => s.selectEndpoint)
  const rememberDefaultRegion = useWorkbench((s) => s.rememberDefaultRegion)
  const regionByServiceId = useWorkbench((s) => s.regionByServiceId)
  const favoriteIds = useFavorites((s) => s.favoriteIds)
  const toggleFavorite = useFavorites((s) => s.toggleFavorite)

  const query = searchTabs.find((tab) => tab.id === activeSearchTabId)?.query ?? ''

  useEffect(() => {
    sessionStorage.setItem(SEARCH_TABS_KEY, JSON.stringify({ tabs: searchTabs, activeId: activeSearchTabId }))
  }, [searchTabs, activeSearchTabId])

  const filtered = useMemo(() => {
    const favoriteSet = new Set(favoriteIds)
    return services
      .map((service) => {
        const match = matchesQuery(service, query)
        return { service, ...match, favorite: favoriteSet.has(service.id) }
      })
      .filter((row) => row.serviceHit || row.endpoints.length > 0)
      .sort((a, b) => {
        if (a.favorite !== b.favorite) {
          return a.favorite ? -1 : 1
        }
        return a.service.name.localeCompare(b.service.name, 'ru')
      })
  }, [favoriteIds, query, services])

  const setActiveQuery = (next: string) => {
    setSearchTabs((tabs) =>
      tabs.map((tab) => (tab.id === activeSearchTabId ? { ...tab, query: next } : tab)),
    )
  }

  const addSearchTab = () => {
    const tab = newSearchTab(searchTabs.length + 1)
    setSearchTabs((tabs) => [...tabs, tab])
    setActiveSearchTabId(tab.id)
  }

  const closeSearchTab = (id: string) => {
    if (searchTabs.length <= 1) {
      return
    }
    const index = searchTabs.findIndex((tab) => tab.id === id)
    const next = searchTabs.filter((tab) => tab.id !== id)
    const relabeled = next.map((tab, i) => ({ ...tab, label: `Search ${i + 1}` }))
    setSearchTabs(relabeled)
    if (activeSearchTabId === id) {
      const fallback = relabeled[Math.max(0, index - 1)] ?? relabeled[0]
      setActiveSearchTabId(fallback.id)
    }
  }

  const toggleService = (id: number) => {
    setCollapsedServices((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    )
  }

  const toggleTag = (key: string) => {
    setCollapsedTags((current) =>
      current.includes(key) ? current.filter((item) => item !== key) : [...current, key],
    )
  }

  const toggleModule = (key: string) => {
    setCollapsedModules((current) =>
      current.includes(key) ? current.filter((item) => item !== key) : [...current, key],
    )
  }

  const allCollapsed =
    filtered.length > 0 && filtered.every((row) => collapsedServices.includes(row.service.id))

  const collapseAll = () => {
    setCollapsedServices(services.map((item) => item.id))
    setCollapsedModules(
      services.flatMap((service) => {
        const modules = groupByModule(endpointsOf(service), service.modules)
        return modules ? modules.map((group) => moduleKey(service.id, group.module)) : []
      }),
    )
    setCollapsedTags(
      services.flatMap((service) => {
        const modules = groupByModule(endpointsOf(service), service.modules)
        if (modules) {
          return modules.flatMap((group) =>
            group.tags.map((tag) => tagKey(service.id, group.module, tag.tag)),
          )
        }
        return groupByTag(endpointsOf(service)).map((group) => tagKey(service.id, '', group.tag))
      }),
    )
  }

  const expandAll = () => {
    setCollapsedServices([])
    setCollapsedModules([])
    setCollapsedTags([])
  }

  if (loading) {
    return (
      <div>
        <div className="catalog-toolbar">
          <p className="catalog-head" style={{ margin: 0 }}>
            Services
          </p>
        </div>
        <div className="skel" style={{ padding: 8 }}>
          <div className="skel-line" />
          <div className="skel-line" style={{ width: '80%' }} />
          <div className="skel-line" style={{ width: '64%' }} />
        </div>
      </div>
    )
  }

  return (
    <div>
      <div className="catalog-toolbar">
        <p className="catalog-head" style={{ margin: 0 }}>
          Services
        </p>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          {onRefreshSwagger ? (
            <button
              type="button"
              className="btn btn-ghost btn-compact tree-fold"
              title="Refresh swagger for all services"
              disabled={refreshingSwagger}
              onClick={onRefreshSwagger}
            >
              <IconRefresh size={14} />
              {refreshingSwagger ? '…' : 'Swagger'}
            </button>
          ) : null}
          {onOpenFree ? (
            <button type="button" className="btn btn-ghost btn-compact tree-fold" title="Open free request" onClick={onOpenFree}>
              <IconPlus size={14} />
              Free
            </button>
          ) : null}
          <button
            type="button"
            className="btn btn-ghost btn-compact tree-fold"
            title={allCollapsed ? 'Expand all services and endpoints' : 'Collapse all endpoints'}
            disabled={filtered.length === 0}
            onClick={() => (allCollapsed ? expandAll() : collapseAll())}
          >
            {allCollapsed ? 'Show all' : 'Hide all'}
          </button>
        </div>
      </div>
      <div className="catalog-search-tabs">
        {searchTabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`catalog-search-tab${tab.id === activeSearchTabId ? ' active' : ''}`}
            onClick={() => setActiveSearchTabId(tab.id)}
            title={tab.query.trim() || tab.label}
          >
            <span>{tab.label}</span>
            {searchTabs.length > 1 ? (
              <span
                className="catalog-search-tab-close"
                role="button"
                tabIndex={-1}
                title="Close search tab"
                onClick={(event) => {
                  event.stopPropagation()
                  closeSearchTab(tab.id)
                }}
              >
                <IconX size={12} />
              </span>
            ) : null}
          </button>
        ))}
        <button
          type="button"
          className="btn btn-ghost btn-compact catalog-search-add"
          title="New search tab"
          onClick={addSearchTab}
        >
          <IconPlus size={14} />
        </button>
      </div>
      <div className="search">
        <IconSearch size={16} />
        <input
          value={query}
          placeholder="All services · module, path, tag"
          onChange={(event) => setActiveQuery(event.target.value)}
        />
      </div>
      {filtered.length === 0 ? (
        <div className="empty" style={{ minHeight: 160, margin: '24px 8px' }}>
          <IconCloudOff size={28} />
          <h2 style={{ fontSize: 16 }}>Nothing found</h2>
          <p>Try another query or refresh YAML in Admin.</p>
        </div>
      ) : (
        <div className="service-list">
          {filtered.map(({ service, endpoints, favorite }, index) => {
            const active = service.id === selectedServiceId
            const open = !collapsedServices.includes(service.id)
            const localQuery = serviceQueries[service.id] ?? ''
            const scopedEndpoints = filterEndpoints(endpoints, localQuery)
            const modules = groupByModule(scopedEndpoints, service.modules)
            const count = service.endpointCount ?? endpoints.length
            const regionChip =
              (regionByServiceId[service.id] !== undefined
                ? regionByServiceId[service.id]
                : service.defaultRegion) || 'global'
            const renderTags = (tags: TaggedEndpoints[], moduleName: string, nested: boolean) =>
              tags.map((group) => {
                const key = tagKey(service.id, moduleName, group.tag)
                const tagOpen = !collapsedTags.includes(key)
                const mKey = moduleName ? moduleKey(service.id, moduleName) : null
                return (
                  <div key={key}>
                    <button
                      type="button"
                      className={`tree-tag${nested ? ' nested' : ''}`}
                      onClick={() => toggleTag(key)}
                    >
                      <span className={`tree-chevron${tagOpen ? ' open' : ''}`}>
                        <IconChevronRight size={12} />
                      </span>
                      {group.tag}
                    </button>
                    {tagOpen
                      ? group.endpoints.map((endpoint) => (
                          <button
                            key={`${key}-${endpoint.id}`}
                            type="button"
                            className={`endpoint-row${nested ? ' nested' : ''}${endpoint.id === selectedEndpointId ? ' active' : ''}`}
                            onClick={() => {
                              rememberDefaultRegion(service)
                              setCollapsedServices((current) => current.filter((id) => id !== service.id))
                              if (mKey) {
                                setCollapsedModules((current) => current.filter((item) => item !== mKey))
                              }
                              setCollapsedTags((current) => current.filter((item) => item !== key))
                              selectEndpoint(service.id, endpoint.id)
                            }}
                          >
                            <span className={methodClass(endpoint.method)}>{endpoint.method}</span>
                            <span className="endpoint-path">{endpoint.path}</span>
                          </button>
                        ))
                      : null}
                  </div>
                )
              })
            return (
              <div key={service.id} className="tree-block">
                <button
                  type="button"
                  className={`service-row${active && !selectedEndpointId ? ' active' : ''}`}
                  style={{ animationDelay: `${index * 45}ms` }}
                  onClick={() => {
                    rememberDefaultRegion(service)
                    selectService(service.id)
                  }}
                >
                  <span
                    className={`tree-chevron${open ? ' open' : ''}`}
                    onClick={(event) => {
                      event.stopPropagation()
                      toggleService(service.id)
                    }}
                  >
                    <IconChevronRight size={14} />
                  </span>
                  <span className="service-color" style={{ background: service.color ?? '#7a8f6a' }} />
                  <span className="service-meta">
                    <span className="service-name">{service.name}</span>
                    <span className="service-sub">
                      {count > 0 ? `${count} endpoints` : (service.description ?? service.authType)}
                    </span>
                  </span>
                  {service.isRegional ? <span className="region-chip">{regionChip}</span> : null}
                  <span
                    className={`star-btn${favorite ? ' on' : ''}`}
                    title={favorite ? 'Remove from favorites' : 'Add to favorites'}
                    onClick={(event) => {
                      event.stopPropagation()
                      toggleFavorite(service.id)
                    }}
                  >
                    <IconStar size={15} filled={favorite} />
                  </span>
                </button>
                {open ? (
                  <>
                    {endpoints.length > 8 ? (
                      <div className="search search-in-service">
                        <IconSearch size={14} />
                        <input
                          value={localQuery}
                          placeholder={`In ${service.name}…`}
                          onChange={(event) =>
                            setServiceQueries((current) => ({ ...current, [service.id]: event.target.value }))
                          }
                        />
                      </div>
                    ) : null}
                    {scopedEndpoints.length === 0 ? (
                      <p className="tree-empty">
                        {localQuery.trim() ? 'Nothing matches in this service.' : 'Refresh swagger in Admin first.'}
                      </p>
                    ) : modules ? (
                      modules.map((mod) => {
                        const mKey = moduleKey(service.id, mod.module)
                        const moduleOpen = !collapsedModules.includes(mKey)
                        return (
                          <div key={mKey}>
                            <button type="button" className="tree-module" onClick={() => toggleModule(mKey)}>
                              <span className={`tree-chevron${moduleOpen ? ' open' : ''}`}>
                                <IconChevronRight size={12} />
                              </span>
                              {mod.module}
                            </button>
                            {moduleOpen ? renderTags(mod.tags, mod.module, true) : null}
                          </div>
                        )
                      })
                    ) : (
                      renderTags(groupByTag(scopedEndpoints), '', false)
                    )}
                  </>
                ) : null}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
