import { useMemo, useState } from 'react'
import { allEndpointTags } from '../api/schema'
import type { CatalogService, ServiceEndpoint } from '../api/types'
import { IconChevronRight, IconCloudOff, IconPlus, IconSearch, IconStar } from '../icons'
import { useFavorites, useWorkbench } from '../store/workbench'

type Props = {
  services: CatalogService[]
  loading?: boolean
  onOpenFree?: () => void
}

type TaggedEndpoints = {
  tag: string
  endpoints: ServiceEndpoint[]
}

type ModuleGroup = {
  module: string
  tags: TaggedEndpoints[]
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

function groupByModule(endpoints: ServiceEndpoint[], preferredOrder: string[] = []): ModuleGroup[] | null {
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
  const rank = new Map(preferredOrder.map((name, index) => [name.toLowerCase(), index]))
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

  const matchingEndpoints = endpoints.filter(
    (endpoint) =>
      endpoint.path.toLowerCase().includes(q) ||
      endpoint.method.toLowerCase().includes(q) ||
      (endpoint.module ?? '').toLowerCase().includes(q) ||
      (endpoint.description ?? '').toLowerCase().includes(q) ||
      (endpoint.operationId ?? '').toLowerCase().includes(q) ||
      allEndpointTags(endpoint.tags, endpoint.userTags).some((tag) => tag.toLowerCase().includes(q)),
  )

  return { serviceHit, endpoints: serviceHit ? endpoints : matchingEndpoints }
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

export function ServiceTree({ services, loading, onOpenFree }: Props) {
  const [query, setQuery] = useState('')
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
  const searching = query.trim().length > 0

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
      <div className="search">
        <IconSearch size={16} />
        <input
          value={query}
          placeholder="Service, module, path, or tag"
          onChange={(event) => setQuery(event.target.value)}
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
            const region = regionByServiceId[service.id] ?? service.defaultRegion
            const open = searching || !collapsedServices.includes(service.id)
            const modules = groupByModule(endpoints, service.modules)
            const count = service.endpointCount ?? endpoints.length
            const renderTags = (tags: TaggedEndpoints[], moduleName: string, nested: boolean) =>
              tags.map((group) => {
                const key = tagKey(service.id, moduleName, group.tag)
                const tagOpen = searching || !collapsedTags.includes(key)
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
                  <span style={{ minWidth: 0, flex: 1 }}>
                    <span style={{ display: 'block', fontSize: 13, fontWeight: 600 }}>{service.name}</span>
                    <span style={{ display: 'block', fontSize: 12, color: 'var(--mute)' }}>
                      {count > 0 ? `${count} endpoints` : (service.description ?? service.authType)}
                    </span>
                  </span>
                  {service.isRegional ? <span className="region-chip">{region ?? 'geo'}</span> : null}
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
                  endpoints.length === 0 ? (
                    <p className="tree-empty">Refresh swagger in Admin first.</p>
                  ) : (
                    modules
                      ? modules.map((mod) => {
                          const mKey = moduleKey(service.id, mod.module)
                          const moduleOpen = searching || !collapsedModules.includes(mKey)
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
                      : renderTags(groupByTag(endpoints), '', false)
                  )
                ) : null}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
