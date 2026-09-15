import { IconMapPin } from '../icons'
import { usePins } from '../store/pins'

type Props = {
  parameters: import('../api/types').EndpointParameter[]
  values: Record<string, string>
  onChange: (name: string, value: string) => void
  serviceId?: number | null
}

export function EndpointParamsForm({ parameters, values, onChange, serviceId }: Props) {
  const openCreate = usePins((s) => s.openCreate)
  const pins = usePins((s) => s.pins)
  const requestApply = usePins((s) => s.requestApply)

  if (parameters.length === 0) {
    return null
  }

  return (
    <div className="params-panel">
      <p className="field-label">Parameters</p>
      <div className="params-table">
        <div className="params-head">
          <span>Name</span>
          <span>Value</span>
        </div>
        {parameters.map((param) => {
          const typeLabel = [param.type, param.format ? `$${param.format}` : null].filter(Boolean).join('')
          const current = values[param.name] ?? ''
          const matchingPins = pins.filter((pin) => {
            const alias = pin.alias.toLowerCase()
            const name = param.name.toLowerCase()
            return alias === name || name.includes(alias) || alias.includes(name)
          })
          return (
            <div key={`${param.in}:${param.name}`} className="params-row">
              <div className="params-meta">
                <span className="params-name">
                  {param.name}
                  {param.required ? <em>*</em> : null}
                </span>
                <span className="params-type">
                  {typeLabel || 'string'}
                  <span>({param.in})</span>
                </span>
                {param.description ? <span className="params-desc">{param.description}</span> : null}
              </div>
              <div className="params-value-row">
                <input
                  className="url-input"
                  value={current}
                  placeholder={param.name}
                  list={matchingPins.length > 0 ? `pin-${param.name}` : undefined}
                  onChange={(event) => onChange(param.name, event.target.value)}
                />
                {matchingPins.length > 0 ? (
                  <datalist id={`pin-${param.name}`}>
                    {matchingPins.map((pin) => (
                      <option key={pin.id} value={pin.value} label={`${pin.alias}${pin.comment ? ` — ${pin.comment}` : ''}`} />
                    ))}
                  </datalist>
                ) : null}
                <button
                  type="button"
                  className="btn btn-ghost btn-compact"
                  title={current.trim() ? 'Pin this value' : 'Pick from pinned'}
                  disabled={!current.trim() && matchingPins.length === 0}
                  onClick={() => {
                    if (current.trim()) {
                      openCreate({
                        value: current.trim(),
                        alias: param.name,
                        sourceKey: param.name,
                        serviceId: serviceId ?? null,
                      })
                      return
                    }
                    if (matchingPins[0]) {
                      requestApply(matchingPins[0])
                    }
                  }}
                >
                  <IconMapPin size={14} />
                </button>
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
