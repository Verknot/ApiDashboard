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

export function allEndpointTags(tags: string[] | undefined, userTags: string[] | undefined): string[] {
  const merged = [...(tags ?? []), ...(userTags ?? [])]
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0)
  if (merged.length === 0) {
    return ['untagged']
  }
  return [...new Set(merged)]
}
