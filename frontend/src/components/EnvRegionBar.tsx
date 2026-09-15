import { environmentsOf, hasGlobalRegion, isProductionEnvironment, regionLabel, type CatalogService } from '../api/types'
import { resolveBaseUrl, useWorkbench } from '../store/workbench'
import { useEffect, useMemo } from 'react'

type Props = {
  service: CatalogService | undefined
}

export function EnvRegionBar({ service }: Props) {
  const environment = useWorkbench((s) => s.environment)
  const setEnvironment = useWorkbench((s) => s.setEnvironment)
  const regionByServiceId = useWorkbench((s) => s.regionByServiceId)
  const setRegion = useWorkbench((s) => s.setRegion)

  const selectedEndpointId = useWorkbench((s) => s.selectedEndpointId)
  const storedRegion = service ? regionByServiceId[service.id] : undefined
  const region =
    service == null
      ? undefined
      : storedRegion !== undefined
        ? storedRegion
        : (service.defaultRegion ?? (hasGlobalRegion(service) ? '' : service.regions[0]?.code))
  const module = service?.endpoints.find((item) => item.id === selectedEndpointId)?.module
  const baseUrl = resolveBaseUrl(service, environment, region, module)
  const envOptions = useMemo(() => environmentsOf(service), [service])
  const showGlobal = service ? hasGlobalRegion(service) : false

  useEffect(() => {
    if (envOptions.length === 0) {
      return
    }
    if (!envOptions.includes(environment)) {
      setEnvironment(envOptions[0])
    }
  }, [environment, envOptions, setEnvironment])

  return (
    <div className="pane-bar">
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
        <div className="pills">
          {envOptions.map((item) => (
            <button
              key={item}
              type="button"
              className={`pill${environment === item ? ' on' : ''}${isProductionEnvironment(item) ? ' danger' : ''}`}
              onClick={() => setEnvironment(item)}
            >
              {item}
            </button>
          ))}
        </div>
        {service?.isRegional ? (
          <div className="pills">
            {showGlobal ? (
              <button
                type="button"
                className={`pill${(region ?? '') === '' ? ' on' : ''}`}
                title="Non-regional host"
                onClick={() => setRegion(service.id, '')}
              >
                global
              </button>
            ) : null}
            {service.regions.map((item) => (
              <button
                key={item.code}
                type="button"
                className={`pill${region === item.code ? ' on' : ''}`}
                onClick={() => setRegion(service.id, item.code)}
              >
                {item.code}
              </button>
            ))}
          </div>
        ) : (
          <span className="meta">no regions</span>
        )}
        {service ? (
          <span className="meta" title={service.proxy ? 'proxy: true in services.yaml' : 'proxy: false in services.yaml'}>
            {service.proxy ? 'proxy' : 'browser'}
          </span>
        ) : null}
      </div>
      <span className="url-chip" title={baseUrl ?? undefined}>
        {baseUrl ?? 'select a service'}
        {service?.isRegional ? (
          <span className="meta" style={{ marginLeft: 8 }}>
            {regionLabel(region)}
          </span>
        ) : null}
      </span>
    </div>
  )
}
