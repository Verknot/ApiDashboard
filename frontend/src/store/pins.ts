import { create } from 'zustand'
import { deletePin, fetchPins, getApiMessage, savePin, updatePin } from '../api/client'
import type { SavePinPayload, UserPin } from '../api/types'

export type PinDraft = {
  id?: number
  value: string
  alias: string
  comment: string
  sourceKey?: string | null
  serviceId?: number | null
}

type PinsState = {
  pins: UserPin[]
  loading: boolean
  draft: PinDraft | null
  applyTicket: { alias: string; value: string; nonce: number } | null
  load: () => Promise<void>
  openCreate: (partial: Partial<PinDraft> & { value: string }) => void
  openEdit: (pin: UserPin) => void
  closeDraft: () => void
  submitDraft: () => Promise<void>
  remove: (id: number) => Promise<void>
  requestApply: (pin: UserPin) => void
  consumeApply: () => { alias: string; value: string } | null
}

function suggestAlias(value: string, sourceKey?: string | null): string {
  const key = (sourceKey ?? '').trim()
  if (key) {
    return key.replace(/^\$\.?/, '').split('.').pop() || key
  }
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim())) {
    return 'id'
  }
  return 'value'
}

export const usePins = create<PinsState>((set, get) => ({
  pins: [],
  loading: false,
  draft: null,
  applyTicket: null,

  load: async () => {
    set({ loading: true })
    try {
      const pins = await fetchPins()
      set({ pins })
    } finally {
      set({ loading: false })
    }
  },

  openCreate: (partial) => {
    set({
      draft: {
        value: partial.value,
        alias: partial.alias?.trim() || suggestAlias(partial.value, partial.sourceKey),
        comment: partial.comment ?? '',
        sourceKey: partial.sourceKey ?? null,
        serviceId: partial.serviceId ?? null,
      },
    })
  },

  openEdit: (pin) => {
    set({
      draft: {
        id: pin.id,
        value: pin.value,
        alias: pin.alias,
        comment: pin.comment ?? '',
        sourceKey: pin.sourceKey,
        serviceId: pin.serviceId,
      },
    })
  },

  closeDraft: () => set({ draft: null }),

  submitDraft: async () => {
    const draft = get().draft
    if (!draft) {
      return
    }
    const payload: SavePinPayload = {
      alias: draft.alias.trim(),
      value: draft.value.trim(),
      comment: draft.comment.trim() || null,
      sourceKey: draft.sourceKey ?? null,
      serviceId: draft.serviceId ?? null,
    }
    const saved = draft.id ? await updatePin(draft.id, payload) : await savePin(payload)
    set((state) => {
      const without = state.pins.filter((item) => item.id !== saved.id && item.alias !== saved.alias)
      return {
        pins: [saved, ...without].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)),
        draft: null,
      }
    })
  },

  remove: async (id) => {
    await deletePin(id)
    set((state) => ({ pins: state.pins.filter((item) => item.id !== id) }))
  },

  requestApply: (pin) => {
    set({
      applyTicket: { alias: pin.alias, value: pin.value, nonce: Date.now() },
    })
  },

  consumeApply: () => {
    const ticket = get().applyTicket
    if (!ticket) {
      return null
    }
    set({ applyTicket: null })
    return { alias: ticket.alias, value: ticket.value }
  },
}))

export { getApiMessage }

/** Match pin alias to endpoint param names. */
export function matchPinToParams(
  alias: string,
  paramNames: string[],
): string | null {
  const needle = alias.trim().toLowerCase()
  if (!needle || paramNames.length === 0) {
    return null
  }
  const exact = paramNames.find((name) => name.toLowerCase() === needle)
  if (exact) {
    return exact
  }
  const soft = paramNames.find((name) => {
    const n = name.toLowerCase()
    return n.includes(needle) || needle.includes(n)
  })
  return soft ?? null
}
