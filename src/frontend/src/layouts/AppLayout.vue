<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import { useAuthStore } from '@/stores/auth'

const navigationItems = [
  { to: '/', label: "Aujourd'hui", mobileLabel: 'Auj.', icon: 'pi pi-sun' },
  { to: '/planner', label: 'Planning', mobileLabel: 'Planning', icon: 'pi pi-calendar' },
  { to: '/library', label: 'Bibliothèque', mobileLabel: 'Biblio', icon: 'pi pi-book' },
  { to: '/settings', label: 'Réglages', mobileLabel: 'Réglages', icon: 'pi pi-cog' },
]

const authStore = useAuthStore()
const router = useRouter()

const userLabel = computed(() => authStore.user?.email ?? 'Utilisateur connecté')

async function handleSignOut(): Promise<void> {
  try {
    await authStore.signOut()
    await router.replace({ name: 'login' })
  } catch (error) {
    console.error(error)
  }
}
</script>

<template>
  <div class="lifeos-layout">
    <aside class="app-sidebar" aria-label="Navigation principale">
      <RouterLink class="app-brand" to="/">
        <span class="app-brand-mark" aria-hidden="true">L</span>
        <span>
          <strong>LifeOS</strong>
          <small>Weekly Planner</small>
        </span>
      </RouterLink>

      <nav class="desktop-nav">
        <RouterLink
          v-for="item in navigationItems"
          :key="item.to"
          :to="item.to"
          class="nav-link"
          exact-active-class="is-active"
        >
          <i :class="item.icon" aria-hidden="true"></i>
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>

      <div class="app-sidebar-footer">
        <small class="sidebar-user">{{ userLabel }}</small>
        <Button label="Déconnexion" severity="secondary" text size="small" @click="handleSignOut" />
      </div>
    </aside>

    <main class="app-main">
      <RouterView />
    </main>

    <nav class="mobile-nav" aria-label="Navigation mobile">
      <RouterLink
        v-for="item in navigationItems"
        :key="item.to"
        :to="item.to"
        class="mobile-nav-link"
        exact-active-class="is-active"
      >
        <i :class="item.icon" aria-hidden="true"></i>
        <span>{{ item.mobileLabel }}</span>
      </RouterLink>
    </nav>
  </div>
</template>

<style scoped>
.app-sidebar-footer {
  display: grid;
  gap: 0.6rem;
  margin-top: auto;
  padding-top: 1rem;
  border-top: 1px solid var(--surface-border);
}

.sidebar-user {
  color: var(--text-color-secondary);
  font-size: 0.85rem;
  line-height: 1.4;
  word-break: break-word;
}
</style>
