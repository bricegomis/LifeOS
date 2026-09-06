<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import { useGroceryStoresStore } from '@/stores/groceryStores'
import type { GroceryStore } from '@/types'

const searchQuery = ref('')
const storeName = ref('')
const storeAddress = ref('')
const editingStoreId = ref<string | null>(null)
const storeFormError = ref('')

const groceryStoresStore = useGroceryStoresStore()
const { stores } = storeToRefs(groceryStoresStore)

const normalizedSearch = computed(() => searchQuery.value.trim().toLocaleLowerCase('fr-FR'))

const filteredStores = computed(() =>
  stores.value.filter((store) => {
    const query = normalizedSearch.value

    return !query
      || store.name.toLocaleLowerCase('fr-FR').includes(query)
      || store.address.toLocaleLowerCase('fr-FR').includes(query)
  }),
)

function resetStoreForm(): void {
  editingStoreId.value = null
  storeName.value = ''
  storeAddress.value = ''
  storeFormError.value = ''
}

function startStoreEdition(store: GroceryStore): void {
  editingStoreId.value = store.id
  storeName.value = store.name
  storeAddress.value = store.address
  storeFormError.value = ''
}

function submitStoreForm(): void {
  const payload = {
    name: storeName.value,
    address: storeAddress.value,
  }
  const success = editingStoreId.value
    ? groceryStoresStore.updateStore(editingStoreId.value, payload)
    : groceryStoresStore.createStore(payload)

  if (!success) {
    storeFormError.value = 'Indiquez le nom du magasin pour l’enregistrer.'
    return
  }

  resetStoreForm()
}

function removeStore(id: string): void {
  groceryStoresStore.deleteStore(id)

  if (editingStoreId.value === id) {
    resetStoreForm()
  }
}
</script>

<template>
  <section class="page-stack stores-page">
    <header class="page-hero stores-hero">
      <div>
        <h1>Magasins</h1>
        <p>Gardez les enseignes et adresses utiles pour vos courses alimentaires.</p>
      </div>
      <span class="stores-count">{{ stores.length }} {{ stores.length > 1 ? 'magasins' : 'magasin' }}</span>
    </header>

    <section class="stores-workspace" aria-label="Gestion des magasins">
      <form class="stores-form" @submit.prevent="submitStoreForm">
        <div>
          <h2>{{ editingStoreId ? 'Modifier le magasin' : 'Ajouter un magasin' }}</h2>
          <p>Le nom est requis. L’adresse reste facultative.</p>
        </div>

        <label class="stores-field">
          <span>Nom du magasin</span>
          <InputText v-model="storeName" placeholder="Ex. Biocoop République" />
        </label>

        <label class="stores-field">
          <span>Adresse</span>
          <InputText v-model="storeAddress" placeholder="Ex. 12 rue de la Paix, Paris" />
        </label>

        <p v-if="storeFormError" class="stores-form-error" role="alert">{{ storeFormError }}</p>

        <div class="stores-form-actions">
          <Button :label="editingStoreId ? 'Enregistrer les modifications' : 'Ajouter le magasin'" type="submit" />
          <Button
            v-if="editingStoreId"
            label="Annuler"
            severity="secondary"
            outlined
            type="button"
            @click="resetStoreForm"
          />
        </div>
      </form>

      <section class="stores-directory" aria-label="Liste des magasins">
        <div class="stores-directory-heading">
          <div>
            <h2>Vos magasins</h2>
            <p>Recherchez, modifiez ou retirez une adresse enregistrée.</p>
          </div>
          <label class="stores-search">
            <i class="pi pi-search" aria-hidden="true"></i>
            <span class="sr-only">Rechercher un magasin</span>
            <InputText v-model="searchQuery" placeholder="Rechercher" />
          </label>
        </div>

        <div v-if="filteredStores.length" class="stores-list">
          <article v-for="store in filteredStores" :key="store.id" class="store-row">
            <div class="store-row-icon" aria-hidden="true"><i class="pi pi-shop"></i></div>
            <div class="store-row-content">
              <h3>{{ store.name }}</h3>
              <p>{{ store.address || 'Adresse non renseignée' }}</p>
            </div>
            <div class="store-row-actions">
              <Button
                label="Modifier"
                severity="secondary"
                outlined
                size="small"
                @click="startStoreEdition(store)"
              />
              <Button label="Supprimer" severity="danger" text size="small" @click="removeStore(store.id)" />
            </div>
          </article>
        </div>

        <div v-else class="stores-empty">
          <i class="pi pi-shop" aria-hidden="true"></i>
          <div>
            <h3>{{ searchQuery ? 'Aucun magasin ne correspond à votre recherche' : 'Aucun magasin enregistré' }}</h3>
            <p>{{ searchQuery ? 'Essayez un autre nom ou une autre adresse.' : 'Ajoutez votre première enseigne avec le formulaire.' }}</p>
          </div>
        </div>
      </section>
    </section>
  </section>
</template>
