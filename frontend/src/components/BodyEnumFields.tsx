import { collectEnumFields, patchJsonPath, readJsonPath } from '../api/schema'
import type { JsonSchema } from '../api/types'

type Props = {
  schema: JsonSchema
  body: string
  onChange: (nextBody: string) => void
}

export function BodyEnumFields({ schema, body, onChange }: Props) {
  const fields = collectEnumFields(schema)
  if (fields.length === 0) {
    return null
  }

  return (
    <details className="response-headers body-enum-fields">
      <summary>
        Enum fields
        <span className="meta" style={{ marginLeft: 8, textTransform: 'none', letterSpacing: 0 }}>
          {fields.length}
        </span>
      </summary>
      <div className="response-headers-body body-enum-list">
        {fields.map((field) => {
          const current = readJsonPath(body, field.path)
          const currentText = current == null ? '' : String(current)
          return (
            <div key={field.path} className="body-enum-row">
              <div className="body-enum-meta">
                <span className="body-enum-name">{field.path}</span>
                <span className="body-enum-type">{field.type || 'enum'}</span>
              </div>
              <select
                className="url-input"
                value={field.values.includes(currentText) ? currentText : ''}
                onChange={(event) => {
                  const next = event.target.value
                  if (!next) {
                    return
                  }
                  onChange(patchJsonPath(body, field.path, next))
                }}
              >
                <option value="">
                  {currentText && !field.values.includes(currentText) ? `${currentText} (custom)` : 'Select…'}
                </option>
                {field.values.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </div>
          )
        })}
      </div>
    </details>
  )
}
