import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { GroceryStore } from '@/types'

interface StoredGroceryStoresState {
  schemaVersion: 1
  stores: GroceryStore[]
}

type GroceryStorePayload = {
  name: string
  address?: string
}

const STORAGE_KEY = 'lifeos.groceryStores.v1'
const SCHEMA_VERSION = 1

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isIsoDate(value: string): boolean {
  return !Number.isNaN(new Date(value).getTime())
}

function nowIso(): string {
  return new Date().toISOString()
}

function createStoreId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return `store-${crypto.randomUUID()}`
  }

  return `store-${Date.now()}`
}

function sanitizeStore(value: unknown): GroceryStore | null {
  if (!isRecord(value) || typeof value.id !== 'string' || typeof value.name !== 'string') {
    return null
  }

  const normalizedName = value.name.trim()

  if (!normalizedName) {
    return null
  }

  const createdAt = typeof value.createdAt === 'string' && isIsoDate(value.createdAt)
    ? value.createdAt
    : nowIso()
  const updatedAt = typeof value.updatedAt === 'string' && isIsoDate(value.updatedAt)
    ? value.updatedAt
    : createdAt

  return {
    id: value.id,
    name: normalizedName,
    address: typeof value.address === 'string' ? value.address.trim() : '',
    createdAt,
    updatedAt,
  }
}

function loadStores(): GroceryStore[] {
  if (typeof window === 'undefined') {
    return []
  }

  try {
    const rawState = window.localStorage.getItem(STORAGE_KEY)

    if (!rawState) {
      return []
    }

    const parsedState = JSON.parse(rawState) as Partial<StoredGroceryStoresState>

    if (parsedState.schemaVersion !== SCHEMA_VERSION || !Array.isArray(parsedState.stores)) {
      return []
    }

    return parsedState.stores.map(sanitizeStore).filter((store): store is GroceryStore => Boolean(store))
  } catch {
    return []
  }
}

function persistStores(stores: GroceryStore[]): void {
  if (typeof window === 'undefined') {
    return
  }

  const state: StoredGroceryStoresState = {
    schemaVersion: SCHEMA_VERSION,
    stores,
  }

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export const useGroceryStoresStore = defineStore('groceryStores', () => {
  const stores = ref<GroceryStore[]>(loadStores())

  function createStore(payload: GroceryStorePayload): boolean {
    const name = payload.name.trim()

    if (!name) {
      return false
    }

    const timestamp = nowIso()

    stores.value = [
      ...stores.value,
      {
        id: createStoreId(),
        name,
        address: payload.address?.trim() ?? '',
        createdAt: timestamp,
        updatedAt: timestamp,
      },
    ]
    persistStores(stores.value)

    return true
  }

  function updateStore(id: string, payload: GroceryStorePayload): boolean {
    const name = payload.name.trim()

    if (!name) {
      return false
    }

    let updated = false

    stores.value = stores.value.map((store) => {
      if (store.id !== id) {
        return store
      }

      updated = true

      return {
        ...store,
        name,
        address: payload.address?.trim() ?? '',
        updatedAt: nowIso(),
      }
    })

    if (updated) {
      persistStores(stores.value)
    }

    return updated
  }

  function deleteStore(id: string): void {
    const nextStores = stores.value.filter((store) => store.id !== id)

    if (nextStores.length === stores.value.length) {
      return
    }

    stores.value = nextStores
    persistStores(stores.value)
  }

  return {
    stores,
    createStore,
    updateStore,
    deleteStore,
  }
})
