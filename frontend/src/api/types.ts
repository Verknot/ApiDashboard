export type EnvironmentName = 'dev' | 'stage' | 'prod'

export type Me = {
  id: number
  email: string
  displayName: string | null
  isFirstLogin: boolean
  roles: string[]
  ownedServiceIds: number[]
  canSend: boolean
  canGenerateDto: boolean
  isAdmin: boolean
}

export type Region = {
  code: string
  label: string
  sortOrder: number
}

export type ServiceUrl = {
  environment: EnvironmentName | string
  regionCode: string
  baseUrl: string
  module: string
}

export type JsonSchema = Record<string, unknown> | null

export type EndpointParameter = {
  name: string
  in: 'path' | 'query' | 'header' | 'cookie' | string
  required?: boolean
  type?: string | null
  format?: string | null
  description?: string | null
}

export type ServiceEndpoint = {
  id: number
  method: string
  path: string
  description: string | null
  operationId: string | null
  tags: string[]
  userTags: string[]
  module: string
  requestSchema: JsonSchema
  responseSchema: JsonSchema
  requestExample: unknown
  parameters: EndpointParameter[] | null
}

export type ServiceModule = {
  name: string
  authType: 'token' | 'certificate' | 'none' | string
  canFetchToken: boolean
}

export type CatalogService = {
  id: number
  name: string
  description: string | null
  color: string | null
  authType: 'token' | 'certificate' | 'none' | string
  proxy: boolean
  isRegional: boolean
  defaultRegion: string | null
  directSendSupported: boolean
  requiresClientCertificate: boolean
  splunkUrl: string | null
  regions: Region[]
  urls: ServiceUrl[]
  endpoints: ServiceEndpoint[]
  endpointCount: number
  modules: ServiceModule[]
  canFetchToken: boolean
}

export type ReloadConfigResponse = {
  upserted: number
  deactivated: number
  warnings: string[]
}

export type ConfigFile = {
  path: string
  writable: boolean
  content: string
}

export type SwaggerServiceRefresh = {
  service: string
  status: string
  endpoints: number
  error: string | null
  added: number
  removed: number
  changed: number
}

export type SwaggerRefreshResponse = {
  ok: number
  failed: number
  services: SwaggerServiceRefresh[]
}

export type SnapshotItem = {
  id: number
  fetchedAt: string
  module: string
}

export type OperationDiff = {
  method: string
  path: string
  kind: string
  summary: string | null
  detail: string
}

export type ContractDiff = {
  serviceId: number
  serviceName: string
  fromFetchedAt: string | null
  toFetchedAt: string | null
  fromSnapshotId: number | null
  toSnapshotId: number
  added: OperationDiff[]
  removed: OperationDiff[]
  changed: OperationDiff[]
}

export type HistoryItem = {
  id: number
  serviceId: number | null
  serviceName: string | null
  serviceColor: string | null
  endpointId: number | null
  environment: string | null
  regionCode: string
  url: string | null
  method: string | null
  requestHeaders: Record<string, unknown> | null
  responseHeaders: Record<string, unknown> | null
  requestBody: unknown
  responseStatus: number | null
  responseBody: string | null
  responseTruncated: boolean
  responseTimeMs: number | null
  createdAt: string
}

export type RequestTemplate = {
  id: number
  endpointId: number
  name: string
  templateBody: unknown
  createdAt: string
}

export type SaveHistoryPayload = {
  serviceId: number | null
  endpointId: number | null
  environment: string | null
  regionCode: string | null
  url: string | null
  method: string | null
  requestHeaders: string | null
  requestBody: string | null
  responseStatus: number | null
  responseBody: string | null
  responseTimeMs: number | null
  responseHeaders: string | null
}

export type ProxySendPayload = {
  serviceId?: number | null
  url: string
  method: string
  headers: Record<string, string>
  body: string | null
}

export type ProxySendResult = {
  status: number | null
  timeMs: number
  body: string
  error: string | null
  headers: Record<string, string>
}

export const ASSIGNABLE_ROLES = ['admin', 'tester', 'developer', 'viewer'] as const

export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number]

export type AdminUser = {
  id: number
  email: string
  displayName: string | null
  isActive: boolean
  isFirstLogin: boolean
  roles: string[]
  createdAt: string
}
