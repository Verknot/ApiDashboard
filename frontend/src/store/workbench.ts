import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { CatalogService, EnvironmentName, HistoryItem, Me } from '../api/types'

type SessionState = {
  me: Me | null
  setMe: (me: Me | null) => void
}

export const useSession = create<SessionState>()((set) => ({
  me: null,
  setMe: (me) => set({ me }),
}))

export type WorkbenchTab = {
  id: string
  kind: 'endpoint' | 'free'
  serviceId: number | null
  endpointId: number | null
  method: string
  url: string
  headersText: string
  body: string
  token: string
  proxy: boolean
  clientCertPath: string
  clientCertPassword: string
}

type WorkbenchState = {
  environment: EnvironmentName
  selectedServiceId: number | null
  selectedEndpointId: number | null
  regionByServiceId: Record<number, string>
  tabs: WorkbenchTab[]
  activeTabId: string | null
  pendingReplay: HistoryItem | null
  tokenByScope: Record<string, string>
  setEnvironment: (environment: EnvironmentName) => void
  selectService: (id: number | null) => void
  selectEndpoint: (serviceId: number, endpointId: number) => void
  openTab: (serviceId: number, endpointId: number) => void
  openFreeTab: (
    seed?: Partial<
      Pick<
        WorkbenchTab,
        'method' | 'url' | 'headersText' | 'body' | 'token' | 'proxy' | 'clientCertPath' | 'clientCertPassword'
      >
    >,
  ) => string
  closeTab: (id: string) => void
  closeAllTabs: () => void
  setActiveTab: (id: string) => void
  setTabBody: (id: string, body: string) => void
  setTabToken: (id: string, token: string) => void
  patchFreeTab: (
    id: string,
    patch: Partial<
      Pick<
        WorkbenchTab,
        'method' | 'url' | 'headersText' | 'body' | 'token' | 'proxy' | 'clientCertPath' | 'clientCertPassword'
      >
    >,
  ) => void
  setRegion: (serviceId: number, region: string) => void
  rememberDefaultRegion: (service: CatalogService) => void
  setPendingReplay: (item: HistoryItem | null) => void
  setServiceToken: (
    serviceId: number,
    environment: EnvironmentName,
    region: string | undefined,
    token: string,
    module?: string,
  ) => void
}

export function tokenScopeKey(
  serviceId: number,
  environment: string,
  region?: string,
  module?: string,
): string {
  return `${serviceId}:${environment}:${region ?? ''}:${module ?? ''}`
}

export function resolveModuleAuth(service: CatalogService, module?: string | null) {
  const name = (module ?? '').trim()
  if (name) {
    const hit = service.modules.find((item) => item.name === name)
    if (hit) {
      return hit
    }
  }
  return {
    name: '',
    authType: service.authType,
    canFetchToken: service.canFetchToken,
  }
}

function endpointTabId(serviceId: number, endpointId: number): string {
  return `${serviceId}:${endpointId}`
}

function freeTabId(): string {
  return `free:${crypto.randomUUID()}`
}

const DEFAULT_FREE_HEADERS = '{\n  "Accept": "application/json"\n}'

function normalizeTab(raw: Partial<WorkbenchTab> & { id: string }): WorkbenchTab {
  const kind = raw.kind === 'free' || raw.id.startsWith('free:') ? 'free' : 'endpoint'
  return {
    id: raw.id,
    kind,
    serviceId: kind === 'free' ? null : (raw.serviceId ?? null),
    endpointId: kind === 'free' ? null : (raw.endpointId ?? null),
    method: (raw.method ?? 'GET').toUpperCase(),
    url: raw.url ?? '',
    headersText: raw.headersText ?? DEFAULT_FREE_HEADERS,
    body: raw.body ?? '',
    token: raw.token ?? '',
    proxy: raw.proxy ?? true,
    clientCertPath: raw.clientCertPath ?? '',
    clientCertPassword: raw.clientCertPassword ?? '',
  }
}

export const useWorkbench = create<WorkbenchState>()(
  persist(
    (set, get) => ({
      environment: 'dev',
      selectedServiceId: null,
      selectedEndpointId: null,
      regionByServiceId: {},
      tabs: [],
      activeTabId: null,
      pendingReplay: null,
      tokenByScope: {},
      setEnvironment: (environment) => set({ environment }),
      selectService: (id) => set({ selectedServiceId: id, selectedEndpointId: null }),
      selectEndpoint: (serviceId, endpointId) => {
        const id = endpointTabId(serviceId, endpointId)
        const existing = get().tabs.find((tab) => tab.id === id)
        const tabs = existing
          ? get().tabs
          : [
              ...get().tabs,
              normalizeTab({
                id,
                kind: 'endpoint',
                serviceId,
                endpointId,
                method: 'GET',
                body: '',
                token: '',
                proxy: true,
              }),
            ]
        set({
          selectedServiceId: serviceId,
          selectedEndpointId: endpointId,
          tabs,
          activeTabId: id,
        })
      },
      openTab: (serviceId, endpointId) => get().selectEndpoint(serviceId, endpointId),
      openFreeTab: (seed) => {
        const id = freeTabId()
        const tab = normalizeTab({
          id,
          kind: 'free',
          method: seed?.method ?? 'GET',
          url: seed?.url ?? '',
          headersText: seed?.headersText ?? DEFAULT_FREE_HEADERS,
          body: seed?.body ?? '',
          token: seed?.token ?? '',
          proxy: seed?.proxy ?? true,
        })
        set({
          tabs: [...get().tabs, tab],
          activeTabId: id,
          selectedServiceId: null,
          selectedEndpointId: null,
        })
        return id
      },
      closeTab: (id) => {
        const tabs = get().tabs.filter((tab) => tab.id !== id)
        const active = get().activeTabId === id ? (tabs.at(-1)?.id ?? null) : get().activeTabId
        const current = tabs.find((tab) => tab.id === active)
        set({
          tabs,
          activeTabId: active,
          selectedServiceId: current?.serviceId ?? null,
          selectedEndpointId: current?.endpointId ?? null,
        })
      },
      closeAllTabs: () =>
        set({
          tabs: [],
          activeTabId: null,
          selectedServiceId: null,
          selectedEndpointId: null,
        }),
      setActiveTab: (id) => {
        const tab = get().tabs.find((item) => item.id === id)
        if (!tab) {
          return
        }
        set({
          activeTabId: id,
          selectedServiceId: tab.serviceId,
          selectedEndpointId: tab.endpointId,
        })
      },
      setTabBody: (id, body) =>
        set({
          tabs: get().tabs.map((tab) => (tab.id === id ? { ...tab, body } : tab)),
        }),
      setTabToken: (id, token) =>
        set({
          tabs: get().tabs.map((tab) => (tab.id === id ? { ...tab, token } : tab)),
        }),
      patchFreeTab: (id, patch) =>
        set({
          tabs: get().tabs.map((tab) => (tab.id === id && tab.kind === 'free' ? { ...tab, ...patch } : tab)),
        }),
      setServiceToken: (serviceId, environment, region, token, module) => {
        const key = tokenScopeKey(serviceId, environment, region, module)
        set({
          tokenByScope: { ...get().tokenByScope, [key]: token },
        })
      },
      setRegion: (serviceId, region) =>
        set({
          regionByServiceId: { ...get().regionByServiceId, [serviceId]: region },
        }),
      rememberDefaultRegion: (service) => {
        if (!service.isRegional || get().regionByServiceId[service.id]) {
          return
        }
        const fallback = service.defaultRegion ?? service.regions[0]?.code
        if (fallback) {
          set({
            regionByServiceId: { ...get().regionByServiceId, [service.id]: fallback },
          })
        }
      },
      setPendingReplay: (item) => set({ pendingReplay: item }),
    }),
    {
      name: 'api-workbench-session',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({
        environment: state.environment,
        selectedServiceId: state.selectedServiceId,
        selectedEndpointId: state.selectedEndpointId,
        regionByServiceId: state.regionByServiceId,
        tabs: state.tabs,
        activeTabId: state.activeTabId,
        tokenByScope: state.tokenByScope,
      }),
      merge: (persisted, current) => {
        const raw = (persisted ?? {}) as Partial<WorkbenchState>
        const tabs = Array.isArray(raw.tabs) ? raw.tabs.map((tab) => normalizeTab(tab as WorkbenchTab)) : current.tabs
        return {
          ...current,
          ...raw,
          tabs,
          pendingReplay: null,
        }
      },
    },
  ),
)

type FavoritesState = {
  favoriteIds: number[]
  toggleFavorite: (serviceId: number) => void
  isFavorite: (serviceId: number) => boolean
}

export const useFavorites = create<FavoritesState>()(
  persist(
    (set, get) => ({
      favoriteIds: [],
      toggleFavorite: (serviceId) => {
        const current = get().favoriteIds
        set({
          favoriteIds: current.includes(serviceId)
            ? current.filter((id) => id !== serviceId)
            : [...current, serviceId],
        })
      },
      isFavorite: (serviceId) => get().favoriteIds.includes(serviceId),
    }),
    {
      name: 'api-workbench-favorites',
      storage: createJSONStorage(() => localStorage),
    },
  ),
)

export function sendModeHint(sendViaProxy: boolean, authType?: string, url?: string): string {
  if (sendViaProxy) {
    return authType === 'certificate'
      ? 'proxy: client certificate is taken from C:\\pult-certs (cert_path in YAML).'
      : 'proxy: the request goes through the workbench. CORS on the target API is not required.'
  }

  const https = (url ?? '').toLowerCase().startsWith('https://')
  if (authType === 'certificate' && !https) {
    return 'browser: certificate picker is HTTPS only. For HTTP keep proxy and the PFX from the folder.'
  }
  if (authType === 'certificate') {
    return 'browser: Windows will open a certificate picker. CORS or Allow CORS is required.'
  }
  return 'browser: direct fetch. Without CORS on the service, install Allow CORS or you get Failed to fetch.'
}

export function resolveBaseUrl(
  service: CatalogService | undefined,
  environment: EnvironmentName,
  regionCode: string | undefined,
  module?: string | null,
): string | null {
  if (!service) {
    return null
  }
  const wantedRegion = service.isRegional ? (regionCode ?? '') : ''
  const wantedModule = (module ?? '').trim()
  const match = service.urls.find(
    (url) =>
      url.environment === environment &&
      (url.regionCode ?? '') === wantedRegion &&
      (url.module ?? '') === wantedModule,
  )
  if (match) {
    return match.baseUrl
  }
  return (
    service.urls.find(
      (url) =>
        url.environment === environment &&
        (url.regionCode ?? '') === wantedRegion &&
        !(url.module ?? '').trim(),
    )?.baseUrl ?? null
  )
}
