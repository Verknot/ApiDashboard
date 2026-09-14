type Props = {
  parameters: import('../api/types').EndpointParameter[]
  values: Record<string, string>
  onChange: (name: string, value: string) => void
}

export function EndpointParamsForm({ parameters, values, onChange }: Props) {
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
              <input
                className="url-input"
                value={values[param.name] ?? ''}
                placeholder={param.name}
                onChange={(event) => onChange(param.name, event.target.value)}
              />
            </div>
          )
        })}
      </div>
    </div>
  )
}
