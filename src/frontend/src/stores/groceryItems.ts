import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { GroceryItem, GroceryItemUnit, GroceryPriceEntry } from '@/types'

interface StoredGroceryItemsState {
  schemaVersion: 1
  items: GroceryItem[]
}

type GroceryItemPayload = {
  name: string
  unit: GroceryItemUnit
}

type GroceryPriceEntryPayload = {
  storeId: string
  price: number
  observedAt: string
}

const STORAGE_KEY = 'lifeos.groceryItems.v1'
const SCHEMA_VERSION = 1
const GROCERY_ITEM_UNITS: GroceryItemUnit[] = ['kilogram', 'liter', 'unit']

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isIsoDate(value: string): boolean {
  return !Number.isNaN(new Date(value).getTime())
}

function isGroceryItemUnit(value: unknown): value is GroceryItemUnit {
  return typeof value === 'string' && (GROCERY_ITEM_UNITS as string[]).includes(value)
}

function nowIso(): string {
  return new Date().toISOString()
}

function createItemId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return `item-${crypto.randomUUID()}`
  }

  return `item-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

function createPriceEntryId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return `price-${crypto.randomUUID()}`
  }

  return `price-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

function sanitizePriceEntry(value: unknown): GroceryPriceEntry | null {
  if (
    !isRecord(value)
    || typeof value.id !== 'string'
    || typeof value.storeId !== 'string'
    || !value.storeId.trim()
    || typeof value.price !== 'number'
    || !Number.isFinite(value.price)
    || value.price < 0
    || typeof value.observedAt !== 'string'
    || !isIsoDate(value.observedAt)
  ) {
    return null
  }

  const createdAt = typeof value.createdAt === 'string' && isIsoDate(value.createdAt)
    ? value.createdAt
    : nowIso()

  return {
    id: value.id,
    storeId: value.storeId,
    price: value.price,
    observedAt: value.observedAt,
    createdAt,
  }
}

function sanitizeItem(value: unknown): GroceryItem | null {
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
  const priceHistory = Array.isArray(value.priceHistory)
    ? value.priceHistory.map(sanitizePriceEntry).filter((entry): entry is GroceryPriceEntry => Boolean(entry))
    : []

  return {
    id: value.id,
    name: normalizedName,
    unit: isGroceryItemUnit(value.unit) ? value.unit : 'unit',
    priceHistory,
    createdAt,
    updatedAt,
  }
}

function loadItems(): GroceryItem[] {
  if (typeof window === 'undefined') {
    return []
  }

  try {
    const rawState = window.localStorage.getItem(STORAGE_KEY)

    if (!rawState) {
      return []
    }

    const parsedState = JSON.parse(rawState) as Partial<StoredGroceryItemsState>

    if (parsedState.schemaVersion !== SCHEMA_VERSION || !Array.isArray(parsedState.items)) {
      return []
    }

    return parsedState.items.map(sanitizeItem).filter((item): item is GroceryItem => Boolean(item))
  } catch {
    return []
  }
}

function persistItems(items: GroceryItem[]): void {
  if (typeof window === 'undefined') {
    return
  }

  const state: StoredGroceryItemsState = {
    schemaVersion: SCHEMA_VERSION,
    items,
  }

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export const useGroceryItemsStore = defineStore('groceryItems', () => {
  const items = ref<GroceryItem[]>(loadItems())

  function createItem(payload: GroceryItemPayload): boolean {
    const name = payload.name.trim()

    if (!name || !isGroceryItemUnit(payload.unit)) {
      return false
    }

    const timestamp = nowIso()

    items.value = [
      ...items.value,
      {
        id: createItemId(),
        name,
        unit: payload.unit,
        priceHistory: [],
        createdAt: timestamp,
        updatedAt: timestamp,
      },
    ]
    persistItems(items.value)

    return true
  }

  function updateItem(id: string, payload: GroceryItemPayload): boolean {
    const name = payload.name.trim()

    if (!name || !isGroceryItemUnit(payload.unit)) {
      return false
    }

    let updated = false

    items.value = items.value.map((item) => {
      if (item.id !== id) {
        return item
      }

      updated = true

      return {
        ...item,
        name,
        unit: payload.unit,
        updatedAt: nowIso(),
      }
    })

    if (updated) {
      persistItems(items.value)
    }

    return updated
  }

  function deleteItem(id: string): void {
    const nextItems = items.value.filter((item) => item.id !== id)

    if (nextItems.length === items.value.length) {
      return
    }

    items.value = nextItems
    persistItems(items.value)
  }

  function addPriceEntry(itemId: string, payload: GroceryPriceEntryPayload): boolean {
    const storeId = payload.storeId.trim()

    if (!storeId || !Number.isFinite(payload.price) || payload.price < 0 || !isIsoDate(payload.observedAt)) {
      return false
    }

    let added = false

    items.value = items.value.map((item) => {
      if (item.id !== itemId) {
        return item
      }

      added = true

      return {
        ...item,
        priceHistory: [
          ...item.priceHistory,
          {
            id: createPriceEntryId(),
            storeId,
            price: payload.price,
            observedAt: payload.observedAt,
            createdAt: nowIso(),
          },
        ],
        updatedAt: nowIso(),
      }
    })

    if (added) {
      persistItems(items.value)
    }

    return added
  }

  function deletePriceEntry(itemId: string, priceEntryId: string): void {
    let changed = false

    items.value = items.value.map((item) => {
      if (item.id !== itemId) {
        return item
      }

      const nextPriceHistory = item.priceHistory.filter((entry) => entry.id !== priceEntryId)

      if (nextPriceHistory.length === item.priceHistory.length) {
        return item
      }

      changed = true

      return {
        ...item,
        priceHistory: nextPriceHistory,
        updatedAt: nowIso(),
      }
    })

    if (changed) {
      persistItems(items.value)
    }
  }

  return {
    items,
    createItem,
    updateItem,
    deleteItem,
    addPriceEntry,
    deletePriceEntry,
  }
})
