import type { JsonSchema, ServiceEndpoint } from './types'

type SchemaNode = {
  type?: string | string[]
  properties?: Record<string, JsonSchema>
  items?: JsonSchema
  example?: unknown
  default?: unknown
  allOf?: JsonSchema[]
  $ref?: string
  format?: string
  enum?: unknown[]
}

function asNode(schema: JsonSchema): SchemaNode | null {
  if (!schema || typeof schema !== 'object' || Array.isArray(schema)) {
    return null
  }
  return schema as SchemaNode
}

export function exampleFromSchema(schema: JsonSchema, name = ''): unknown {
  const node = asNode(schema)
  if (!node) {
    return {}
  }

  if (node.example !== undefined) {
    return node.example
  }
  if (node.default !== undefined) {
    return node.default
  }
  if (node.enum && node.enum.length > 0) {
    return node.enum[0]
  }

  if (node.allOf && node.allOf.length > 0) {
    const merged: Record<string, unknown> = {}
    for (const part of node.allOf) {
      const value = exampleFromSchema(part, name)
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        Object.assign(merged, value)
      }
    }
    if (Object.keys(merged).length > 0) {
      return merged
    }
  }

  const type = Array.isArray(node.type) ? node.type[0] : node.type

  if (type === 'object' || node.properties) {
    const result: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(node.properties ?? {})) {
      result[key] = exampleFromSchema(value, key)
    }
    return result
  }

  if (type === 'array') {
    return [exampleFromSchema(node.items ?? null, name)]
  }

  if (type === 'integer' || type === 'number' || node.format === 'double' || node.format === 'int32') {
    return 0
  }
  if (type === 'boolean') {
    return false
  }
  if (type === 'string') {
    if (node.format === 'date-time') {
      return new Date().toISOString()
    }
    if (node.format === 'uuid') {
      return '00000000-0000-0000-0000-000000000000'
    }
    return name || ''
  }

  if (name) {
    return ''
  }
  return {}
}

export function isBlankRequestBody(value: string | null | undefined): boolean {
  if (value == null) {
    return true
  }
  const trimmed = value.trim()
  if (!trimmed) {
    return true
  }
  try {
    const parsed: unknown = JSON.parse(trimmed)
    if (parsed == null) {
      return true
    }
    if (typeof parsed === 'object' && !Array.isArray(parsed) && Object.keys(parsed as object).length === 0) {
      return true
    }
  } catch {
    return false
  }
  return false
}

function hasUsefulExample(value: unknown): boolean {
  if (value == null || value === '') {
    return false
  }
  if (typeof value === 'object' && !Array.isArray(value) && Object.keys(value as object).length === 0) {
    return false
  }
  return true
}

export function seedRequestBody(endpoint: Pick<ServiceEndpoint, 'method' | 'requestSchema' | 'requestExample'>): string {
  const method = endpoint.method.toUpperCase()
  if (method === 'GET' || method === 'HEAD') {
    return ''
  }

  if (hasUsefulExample(endpoint.requestExample)) {
    return prettyJson(endpoint.requestExample)
  }

  const schema = unwrapSchema(endpoint.requestSchema)
  const example = schema ? exampleFromSchema(schema) : {}
  return prettyJson(example)
}

function unwrapSchema(schema: JsonSchema | string | undefined | null): JsonSchema {
  if (!schema) {
    return null
  }
  if (typeof schema === 'string') {
    try {
      return JSON.parse(schema) as JsonSchema
    } catch {
      return null
    }
  }
  return schema
}

export function prettyJson(value: unknown): string {
  try {
    return JSON.stringify(value ?? {}, null, 2)
  } catch {
    return '{}'
  }
}

export type SchemaEnumField = {
  path: string
  type: string | null
  values: string[]
}

function enumValues(node: SchemaNode): string[] {
  if (!node.enum || node.enum.length === 0) {
    return []
  }
  return node.enum.map((item) => String(item)).filter((item) => item.length > 0)
}

function mergePropertyMaps(schema: JsonSchema): Record<string, JsonSchema> {
  const node = asNode(schema)
  if (!node) {
    return {}
  }
  const map: Record<string, JsonSchema> = { ...(node.properties ?? {}) }
  for (const part of node.allOf ?? []) {
    Object.assign(map, mergePropertyMaps(part))
  }
  return map
}

/** Top-level (and one nested object level) properties that declare an OpenAPI enum. */
export function collectEnumFields(schema: JsonSchema | string | null | undefined): SchemaEnumField[] {
  const root = unwrapSchema(schema)
  if (!root) {
    return []
  }

  const fields: SchemaEnumField[] = []
  const seen = new Set<string>()

  const visit = (props: Record<string, JsonSchema>, prefix: string, depth: number) => {
    for (const [key, value] of Object.entries(props)) {
      const path = prefix ? `${prefix}.${key}` : key
      const node = asNode(value)
      if (!node) {
        continue
      }

      const values = enumValues(node)
      if (values.length > 0 && !seen.has(path)) {
        seen.add(path)
        const type = Array.isArray(node.type) ? node.type[0] : node.type
        fields.push({ path, type: type ?? 'enum', values })
      }

      if (depth < 1) {
        const nested = mergePropertyMaps(value)
        if (Object.keys(nested).length > 0) {
          visit(nested, path, depth + 1)
        }
      }
    }
  }

  visit(mergePropertyMaps(root), '', 0)
  return fields
}

export function patchJsonPath(source: string, path: string, nextValue: unknown): string {
  let root: unknown
  try {
    root = JSON.parse(source.trim() || '{}')
  } catch {
    root = {}
  }
  if (!root || typeof root !== 'object' || Array.isArray(root)) {
    root = {}
  }

  const parts = path.split('.').filter(Boolean)
  if (parts.length === 0) {
    return prettyJson(root)
  }

  let cursor = root as Record<string, unknown>
  for (let i = 0; i < parts.length - 1; i++) {
    const key = parts[i]
    const child = cursor[key]
    if (!child || typeof child !== 'object' || Array.isArray(child)) {
      cursor[key] = {}
    }
    cursor = cursor[key] as Record<string, unknown>
  }
  cursor[parts[parts.length - 1]] = nextValue
  return prettyJson(root)
}

export function readJsonPath(source: string, path: string): unknown {
  try {
    let cursor: unknown = JSON.parse(source.trim() || '{}')
    for (const part of path.split('.').filter(Boolean)) {
      if (!cursor || typeof cursor !== 'object' || Array.isArray(cursor)) {
        return undefined
      }
      cursor = (cursor as Record<string, unknown>)[part]
    }
    return cursor
  } catch {
    return undefined
  }
}

export function allEndpointTags(tags: string[] | undefined, userTags: string[] | undefined): string[] {
  const merged = [...(tags ?? []), ...(userTags ?? [])]
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0)
  if (merged.length === 0) {
    return ['untagged']
  }
  return [...new Set(merged)]
}
