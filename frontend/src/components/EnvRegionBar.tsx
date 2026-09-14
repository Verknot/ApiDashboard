import { environmentsOf, isProductionEnvironment, type CatalogService } from '../api/types'
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
  const region = service ? regionByServiceId[service.id] ?? service.defaultRegion ?? service.regions[0]?.code : undefined
  const module = service?.endpoints.find((item) => item.id === selectedEndpointId)?.module
  const baseUrl = resolveBaseUrl(service, environment, region, module)
  const envOptions = useMemo(() => environmentsOf(service), [service])

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
      </span>
    </div>
  )
}
