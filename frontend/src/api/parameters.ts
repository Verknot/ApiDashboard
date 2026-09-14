import type { EndpointParameter } from './types'
import { useMemo } from 'react'

function pathParamNames(path: string): string[] {
  const names: string[] = []
  const re = /\{([^}]+)\}/g
  let match: RegExpExecArray | null
  while ((match = re.exec(path))) {
    const name = match[1]?.trim()
    if (name && !names.includes(name)) {
      names.push(name)
    }
  }
  return names
}

export function mergeEndpointParameters(
  path: string,
  fromSpec: EndpointParameter[] | null | undefined,
): EndpointParameter[] {
  const byKey = new Map<string, EndpointParameter>()
  for (const item of fromSpec ?? []) {
    if (!item?.name) {
      continue
    }
    const key = `${(item.in || 'query').toLowerCase()}:${item.name}`
    byKey.set(key, {
      name: item.name,
      in: (item.in || 'query').toLowerCase(),
      required: Boolean(item.required),
      type: item.type ?? null,
      format: item.format ?? null,
      description: item.description ?? null,
    })
  }

  for (const name of pathParamNames(path)) {
    const key = `path:${name}`
    if (!byKey.has(key)) {
      byKey.set(key, { name, in: 'path', required: true, type: 'string' })
    }
  }

  const order = { path: 0, query: 1, header: 2, cookie: 3 }
  return [...byKey.values()].sort((a, b) => {
    const ia = order[a.in as keyof typeof order] ?? 9
    const ib = order[b.in as keyof typeof order] ?? 9
    if (ia !== ib) {
      return ia - ib
    }
    return a.name.localeCompare(b.name)
  })
}

export function applyParametersToUrl(
  baseUrl: string,
  pathTemplate: string,
  values: Record<string, string>,
  parameters: EndpointParameter[],
): string {
  let path = pathTemplate
  for (const param of parameters.filter((item) => item.in === 'path')) {
    const raw = values[param.name] ?? ''
    path = path.replaceAll(`{${param.name}}`, raw.trim() ? encodeURIComponent(raw.trim()) : `{${param.name}}`)
  }

  const root = baseUrl.replace(/\/$/, '')
  const query = new URLSearchParams()
  for (const param of parameters.filter((item) => item.in === 'query')) {
    const raw = values[param.name] ?? ''
    if (raw.trim()) {
      query.set(param.name, raw.trim())
    }
  }
  const q = query.toString()
  return `${root}${path.startsWith('/') ? path : `/${path}`}${q ? `?${q}` : ''}`
}

export function useEndpointParameters(path: string, fromSpec: EndpointParameter[] | null | undefined) {
  return useMemo(() => mergeEndpointParameters(path, fromSpec), [path, fromSpec])
}
