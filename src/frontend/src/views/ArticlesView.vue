<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed, reactive, ref } from 'vue'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import { useGroceryItemsStore } from '@/stores/groceryItems'
import { useGroceryStoresStore } from '@/stores/groceryStores'
import type { GroceryItem, GroceryItemUnit } from '@/types'

interface UnitOption {
  label: string
  value: GroceryItemUnit
}

const unitOptions: UnitOption[] = [
  { label: 'Au kilo', value: 'kilogram' },
  { label: 'Au litre', value: 'liter' },
  { label: "À l'unité", value: 'unit' },
]

const unitLabels: Record<GroceryItemUnit, string> = {
  kilogram: 'kg',
  liter: 'L',
  unit: 'unité',
}

const dateFormatter = new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric' })

const searchQuery = ref('')
const itemName = ref('')
const itemUnit = ref<GroceryItemUnit>('unit')
const editingItemId = ref<string | null>(null)
const itemFormError = ref('')
const expandedItemId = ref<string | null>(null)

const priceForms = reactive<Record<string, { storeId: string; price: number | null; observedAt: string; error: string }>>({})

const groceryItemsStore = useGroceryItemsStore()
const { items } = storeToRefs(groceryItemsStore)
const groceryStoresStore = useGroceryStoresStore()
const { stores } = storeToRefs(groceryStoresStore)

const storeOptions = computed(() => stores.value.map((store) => ({ label: store.name, value: store.id })))

function storeName(storeId: string): string {
  return stores.value.find((store) => store.id === storeId)?.name ?? 'Magasin supprimé'
}

function formatPrice(price: number): string {
  return `${price.toFixed(2)} €`
}

const normalizedSearch = computed(() => searchQuery.value.trim().toLocaleLowerCase('fr-FR'))

const filteredItems = computed(() =>
  items.value.filter((item) => {
    const query = normalizedSearch.value

    return !query || item.name.toLocaleLowerCase('fr-FR').includes(query)
  }),
)

function sortedPriceHistory(item: GroceryItem) {
  return [...item.priceHistory].sort((a, b) => b.observedAt.localeCompare(a.observedAt))
}

function latestPrice(item: GroceryItem): string {
  const [mostRecentEntry] = sortedPriceHistory(item)

  if (!mostRecentEntry) {
    return 'Aucun prix enregistré'
  }

  return `${formatPrice(mostRecentEntry.price)} / ${unitLabels[item.unit]}`
}

function resetItemForm(): void {
  editingItemId.value = null
  itemName.value = ''
  itemUnit.value = 'unit'
  itemFormError.value = ''
}

function startItemEdition(item: GroceryItem): void {
  editingItemId.value = item.id
  itemName.value = item.name
  itemUnit.value = item.unit
  itemFormError.value = ''
}

function submitItemForm(): void {
  const payload = {
    name: itemName.value,
    unit: itemUnit.value,
  }
  const success = editingItemId.value
    ? groceryItemsStore.updateItem(editingItemId.value, payload)
    : groceryItemsStore.createItem(payload)

  if (!success) {
    itemFormError.value = 'Indiquez le nom de l’article pour l’enregistrer.'
    return
  }

  resetItemForm()
}

function removeItem(id: string): void {
  groceryItemsStore.deleteItem(id)

  if (editingItemId.value === id) {
    resetItemForm()
  }

  if (expandedItemId.value === id) {
    expandedItemId.value = null
  }

  delete priceForms[id]
}

function priceForm(itemId: string) {
  if (!priceForms[itemId]) {
    priceForms[itemId] = {
      storeId: '',
      price: null,
      observedAt: new Date().toISOString().slice(0, 10),
      error: '',
    }
  }

  return priceForms[itemId]
}

function toggleItemExpansion(itemId: string): void {
  expandedItemId.value = expandedItemId.value === itemId ? null : itemId
  priceForm(itemId)
}

function submitPriceForm(itemId: string): void {
  const form = priceForm(itemId)

  if (!form.storeId || form.price === null || !form.observedAt) {
    form.error = 'Renseignez le magasin, le prix et la date d’achat.'
    return
  }

  const success = groceryItemsStore.addPriceEntry(itemId, {
    storeId: form.storeId,
    price: form.price,
    observedAt: form.observedAt,
  })

  if (!success) {
    form.error = 'Impossible d’enregistrer ce prix.'
    return
  }

  form.storeId = ''
  form.price = null
  form.observedAt = new Date().toISOString().slice(0, 10)
  form.error = ''
}

function removePriceEntry(itemId: string, priceEntryId: string): void {
  groceryItemsStore.deletePriceEntry(itemId, priceEntryId)
}
</script>

<template>
  <section class="page-stack stores-page">
    <header class="page-hero stores-hero">
      <div>
        <h1>Articles</h1>
        <p>Suivez vos articles de courses et l’historique de leurs prix par magasin.</p>
      </div>
      <span class="stores-count">{{ items.length }} {{ items.length > 1 ? 'articles' : 'article' }}</span>
    </header>

    <section class="stores-workspace" aria-label="Gestion des articles">
      <form class="stores-form" @submit.prevent="submitItemForm">
        <div>
          <h2>{{ editingItemId ? 'Modifier l’article' : 'Ajouter un article' }}</h2>
          <p>Le nom et l’unité de référence sont requis.</p>
        </div>

        <label class="stores-field">
          <span>Nom de l’article</span>
          <InputText v-model="itemName" placeholder="Ex. Riz basmati" />
        </label>

        <label class="stores-field">
          <span>Unité de référence</span>
          <Select v-model="itemUnit" :options="unitOptions" option-label="label" option-value="value" />
        </label>

        <p v-if="itemFormError" class="stores-form-error" role="alert">{{ itemFormError }}</p>

        <div class="stores-form-actions">
          <Button :label="editingItemId ? 'Enregistrer les modifications' : 'Ajouter l’article'" type="submit" />
          <Button
            v-if="editingItemId"
            label="Annuler"
            severity="secondary"
            outlined
            type="button"
            @click="resetItemForm"
          />
        </div>
      </form>

      <section class="stores-directory" aria-label="Liste des articles">
        <div class="stores-directory-heading">
          <div>
            <h2>Vos articles</h2>
            <p>Recherchez un article et suivez l’historique de ses prix.</p>
          </div>
          <label class="stores-search">
            <i class="pi pi-search" aria-hidden="true"></i>
            <span class="sr-only">Rechercher un article</span>
            <InputText v-model="searchQuery" placeholder="Rechercher" />
          </label>
        </div>

        <div v-if="filteredItems.length" class="stores-list">
          <article v-for="item in filteredItems" :key="item.id" class="store-row article-row">
            <div class="article-row-main">
              <div class="store-row-icon" aria-hidden="true"><i class="pi pi-box"></i></div>
              <div class="store-row-content">
                <h3>{{ item.name }}</h3>
                <p>{{ latestPrice(item) }} · prix {{ unitLabels[item.unit] }}</p>
              </div>
              <div class="store-row-actions">
                <Button
                  :label="expandedItemId === item.id ? 'Fermer l’historique' : 'Historique des prix'"
                  severity="secondary"
                  outlined
                  size="small"
                  @click="toggleItemExpansion(item.id)"
                />
                <Button
                  label="Modifier"
                  severity="secondary"
                  outlined
                  size="small"
                  @click="startItemEdition(item)"
                />
                <Button label="Supprimer" severity="danger" text size="small" @click="removeItem(item.id)" />
              </div>
            </div>

            <div v-if="expandedItemId === item.id" class="article-price-panel">
              <form class="article-price-form" @submit.prevent="submitPriceForm(item.id)">
                <label class="stores-field">
                  <span>Magasin</span>
                  <Select
                    v-model="priceForm(item.id).storeId"
                    :options="storeOptions"
                    option-label="label"
                    option-value="value"
                    placeholder="Choisir un magasin"
                  />
                </label>

                <label class="stores-field">
                  <span>Prix</span>
                  <InputNumber
                    v-model="priceForm(item.id).price"
                    mode="currency"
                    currency="EUR"
                    locale="fr-FR"
                    :min="0"
                  />
                </label>

                <label class="stores-field">
                  <span>Date d’achat</span>
                  <input class="text-input" type="date" v-model="priceForm(item.id).observedAt" />
                </label>

                <Button label="Ajouter le prix" type="submit" size="small" />
              </form>

              <p v-if="priceForm(item.id).error" class="stores-form-error" role="alert">
                {{ priceForm(item.id).error }}
              </p>

              <div v-if="sortedPriceHistory(item).length" class="article-price-list">
                <article v-for="entry in sortedPriceHistory(item)" :key="entry.id" class="article-price-entry">
                  <div>
                    <strong>{{ formatPrice(entry.price) }}</strong>
                    <span>{{ storeName(entry.storeId) }} · {{ dateFormatter.format(new Date(`${entry.observedAt}T00:00:00`)) }}</span>
                  </div>
                  <Button
                    icon="pi pi-trash"
                    aria-label="Supprimer ce prix"
                    severity="danger"
                    text
                    rounded
                    size="small"
                    @click="removePriceEntry(item.id, entry.id)"
                  />
                </article>
              </div>

              <p v-else class="stores-empty-inline">Aucun prix enregistré pour cet article.</p>
            </div>
          </article>
        </div>

        <div v-else class="stores-empty">
          <i class="pi pi-box" aria-hidden="true"></i>
          <div>
            <h3>{{ searchQuery ? 'Aucun article ne correspond à votre recherche' : 'Aucun article enregistré' }}</h3>
            <p>{{ searchQuery ? 'Essayez un autre nom.' : 'Ajoutez votre premier article avec le formulaire.' }}</p>
          </div>
        </div>
      </section>
    </section>
  </section>
</template>
