import axios from 'axios'
import type {
  CatalogService,
  ConfigFile,
  ContractDiff,
  HistoryItem,
  Me,
  ReloadConfigResponse,
  RequestTemplate,
  SaveHistoryPayload,
  ServiceEndpoint,
  SnapshotItem,
  SwaggerRefreshResponse,
  ProxySendPayload,
  ProxySendResult,
  AdminUser,
  UserPin,
  SavePinPayload,
} from './types'

export const api = axios.create({
  baseURL: '/api',
  withCredentials: true,
  headers: { 'X-Requested-With': 'XMLHttpRequest' },
})

export function getApiMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string } | undefined
    if (data?.message) {
      return data.message
    }
  }
  return fallback
}

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      const url = String(error.config?.url ?? '')
      const isAuthProbe = url.includes('/auth/me') || url.includes('/auth/login')
      if (!isAuthProbe && window.location.pathname !== '/login') {
        window.location.assign('/login')
      }
    }
    return Promise.reject(error)
  },
)

export async function login(email: string, password: string): Promise<Me> {
  const { data } = await api.post<Me>('/auth/login', { email, password })
  return data
}

export async function logout(): Promise<void> {
  await api.post('/auth/logout')
}

export async function fetchMe(): Promise<Me> {
  const { data } = await api.get<Me>('/auth/me')
  return data
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await api.post('/auth/change-password', { currentPassword, newPassword })
}

export async function fetchServiceToken(
  serviceId: number,
  environment: string,
  regionCode?: string,
  module?: string,
): Promise<{ accessToken: string | null; redirectUrl: string | null }> {
  const { data } = await api.post<{ accessToken?: string | null; redirectUrl?: string | null }>(
    `/services/${serviceId}/token`,
    {
      environment,
      regionCode: regionCode ?? '',
      module: module ?? '',
    },
  )
  return {
    accessToken: data.accessToken ?? null,
    redirectUrl: data.redirectUrl ?? null,
  }
}

export async function fetchServices(): Promise<CatalogService[]> {
  const { data } = await api.get<CatalogService[]>('/services')
  return data.map((service) => ({
    ...service,
    endpoints: (service.endpoints ?? []).map((endpoint) => ({
      ...endpoint,
      tags: endpoint.tags ?? [],
      userTags: endpoint.userTags ?? [],
      module: endpoint.module ?? '',
      parameters: Array.isArray(endpoint.parameters) ? endpoint.parameters : null,
    })),
    modules: (service.modules ?? []).map((module) =>
      typeof module === 'string'
        ? { name: module, authType: service.authType, canFetchToken: Boolean(service.canFetchToken) }
        : {
            name: module.name,
            authType: module.authType ?? service.authType,
            canFetchToken: Boolean(module.canFetchToken),
          },
    ),
    urls: (service.urls ?? []).map((url) => ({ ...url, module: url.module ?? '' })),
    canFetchToken: Boolean(service.canFetchToken),
  }))
}

export async function reloadConfig(): Promise<ReloadConfigResponse> {
  const { data } = await api.post<ReloadConfigResponse>('/admin/config/reload')
  return data
}

export async function fetchConfig(): Promise<ConfigFile> {
  const { data } = await api.get<ConfigFile>('/admin/config')
  return data
}

export async function saveConfig(content: string): Promise<ReloadConfigResponse> {
  const { data } = await api.put<ReloadConfigResponse>('/admin/config', { content })
  return data
}

export async function refreshSwagger(): Promise<SwaggerRefreshResponse> {
  const { data } = await api.post<SwaggerRefreshResponse>('/services/swagger/refresh')
  return data
}

export async function fetchAdminUsers(): Promise<AdminUser[]> {
  const { data } = await api.get<AdminUser[]>('/admin/users')
  return data
}

export async function createAdminUser(payload: {
  email: string
  displayName?: string
  password: string
  role: string
}): Promise<AdminUser> {
  const { data } = await api.post<AdminUser>('/admin/users', payload)
  return data
}

export async function setAdminUserActive(id: number, isActive: boolean): Promise<AdminUser> {
  const { data } = await api.patch<AdminUser>(`/admin/users/${id}/active`, { isActive })
  return data
}

export async function setAdminUserRole(id: number, role: string): Promise<AdminUser> {
  const { data } = await api.patch<AdminUser>(`/admin/users/${id}/role`, { role })
  return data
}

export async function resetAdminUserPassword(id: number, password: string): Promise<AdminUser> {
  const { data } = await api.post<AdminUser>(`/admin/users/${id}/password`, { password })
  return data
}

export async function fetchSnapshots(serviceId: number): Promise<SnapshotItem[]> {
  const { data } = await api.get<SnapshotItem[]>(`/services/${serviceId}/snapshots`)
  return (data ?? []).map((item) => ({ ...item, module: item.module ?? '' }))
}

export async function fetchContractDiff(serviceId: number, fromId?: number, toId?: number): Promise<ContractDiff> {
  const { data } = await api.get<ContractDiff>(`/services/${serviceId}/diff`, {
    params: { fromId, toId },
  })
  return data
}

export async function saveUserTags(endpointId: number, tags: string[]): Promise<ServiceEndpoint> {
  const { data } = await api.put<ServiceEndpoint>(`/endpoints/${endpointId}/user-tags`, { tags })
  return data
}

export async function downloadDto(endpointId: number): Promise<void> {
  const response = await api.get<Blob>(`/endpoints/${endpointId}/dto`, { responseType: 'blob' })
  const disposition = String(response.headers['content-disposition'] ?? '')
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
  const name = match?.[1] ? decodeURIComponent(match[1]) : 'Dto.cs'
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}

export async function saveHistory(payload: SaveHistoryPayload): Promise<void> {
  await api.post('/history', payload)
}

export async function fetchHistory(params?: {
  serviceId?: number
  endpointId?: number
  status?: number
  q?: string
  take?: number
}): Promise<HistoryItem[]> {
  const { data } = await api.get<HistoryItem[]>('/history', { params })
  return data
}

export async function fetchTemplates(endpointId: number): Promise<RequestTemplate[]> {
  const { data } = await api.get<RequestTemplate[]>('/templates', { params: { endpointId } })
  return data
}

export async function saveTemplate(endpointId: number, name: string, templateBody: unknown): Promise<RequestTemplate> {
  const { data } = await api.post<RequestTemplate>('/templates', { endpointId, name, templateBody })
  return data
}

export async function deleteTemplate(id: number): Promise<void> {
  await api.delete(`/templates/${id}`)
}

export async function fetchPins(): Promise<UserPin[]> {
  const { data } = await api.get<UserPin[]>('/pins')
  return data
}

export async function savePin(payload: SavePinPayload): Promise<UserPin> {
  const { data } = await api.post<UserPin>('/pins', payload)
  return data
}

export async function updatePin(id: number, payload: SavePinPayload): Promise<UserPin> {
  const { data } = await api.put<UserPin>(`/pins/${id}`, payload)
  return data
}

export async function deletePin(id: number): Promise<void> {
  await api.delete(`/pins/${id}`)
}

export async function proxySend(payload: ProxySendPayload): Promise<ProxySendResult> {
  const { data } = await api.post<ProxySendResult>('/send', payload)
  return data
}

export async function directSend(payload: ProxySendPayload): Promise<ProxySendResult> {
  const method = payload.method.trim().toUpperCase()
  const hasBody = !['GET', 'HEAD'].includes(method)
  const started = performance.now()
  try {
    const response = await fetch(payload.url, {
      method,
      headers: payload.headers,
      body: hasBody ? (payload.body ?? undefined) : undefined,
      credentials: 'omit',
      cache: 'no-store',
      redirect: 'follow',
      signal: AbortSignal.timeout(60_000),
    })
    const body = await response.text()
    const headers: Record<string, string> = {}
    response.headers.forEach((value, key) => {
      headers[key] = value
    })
    return {
      status: response.status,
      timeMs: Math.round(performance.now() - started),
      body,
      error: null,
      headers,
    }
  } catch (error) {
    const timeMs = Math.round(performance.now() - started)
    const message = describeDirectSendError(error)
    return { status: null, timeMs, body: message, error: message, headers: {} }
  }
}

function describeDirectSendError(error: unknown): string {
  const raw = error instanceof Error ? error.message : 'Send failed'
  const name = error instanceof Error ? error.name : ''
  if (name === 'TimeoutError' || /aborted|timeout/i.test(raw)) {
    return 'Direct request timed out (60 s).'
  }
  if (/failed to fetch|networkerror|load failed/i.test(raw) || name === 'TypeError') {
    return 'The browser blocked the request (CORS or network). Enable proxy or install Allow CORS. Client certificate picker is browser mode only, HTTPS only.'
  }
  return raw
}
