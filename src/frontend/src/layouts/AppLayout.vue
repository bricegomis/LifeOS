<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import { useAuthStore } from '@/stores/auth'
import BuildVersion from '@/components/BuildVersion.vue'
import { parseApiBuildInfo, type BuildInfo } from '@/utils/buildInfo'

const navigationItems = [
  { to: '/', label: "Aujourd'hui", mobileLabel: 'Auj.', icon: 'pi pi-sun' },
  { to: '/planner', label: 'Planning', mobileLabel: 'Planning', icon: 'pi pi-calendar' },
  { to: '/library', label: 'Bibliothèque', mobileLabel: 'Biblio', icon: 'pi pi-book' },
  { to: '/stores', label: 'Magasins', mobileLabel: 'Magasins', icon: 'pi pi-shop' },
  { to: '/articles', label: 'Articles', mobileLabel: 'Articles', icon: 'pi pi-box' },
  { to: '/settings', label: 'Réglages', mobileLabel: 'Réglages', icon: 'pi pi-cog' },
]

const authStore = useAuthStore()
const router = useRouter()

const userLabel = computed(() => authStore.user?.email ?? 'Utilisateur connecté')
const buildInfo: BuildInfo = {
  buildId: import.meta.env.VITE_LIFEOS_BUILD_ID?.trim() || (import.meta.env.DEV ? 'dev' : 'local'),
  buildNumber: import.meta.env.VITE_LIFEOS_BUILD_NUMBER,
  runId: import.meta.env.VITE_LIFEOS_RUN_ID,
  runAttempt: import.meta.env.VITE_LIFEOS_RUN_ATTEMPT,
  repository: import.meta.env.VITE_LIFEOS_BUILD_REPOSITORY,
  serverUrl: import.meta.env.VITE_LIFEOS_BUILD_SERVER_URL,
  workflow: import.meta.env.VITE_LIFEOS_BUILD_WORKFLOW,
}
const apiBaseUrl = import.meta.env.VITE_LIFEOS_API_URL?.trim().replace(/\/+$/, '') ?? ''
const apiBuildInfo = ref<BuildInfo | null>(null)
const apiBuildStatus = ref(apiBaseUrl ? 'chargement…' : 'non configurée')

onMounted(async () => {
  if (!apiBaseUrl) return

  try {
    const response = await fetch(`${apiBaseUrl}/api/version`, { signal: AbortSignal.timeout(5000) })
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    const version: unknown = await response.json()
    apiBuildInfo.value = parseApiBuildInfo(version)
  } catch {
    apiBuildStatus.value = 'indisponible'
  }
})

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
        <div class="sidebar-build" role="group" aria-label="Versions déployées">
          <BuildVersion component="UI" :info="buildInfo" />
          <BuildVersion component="API" :info="apiBuildInfo" :status="apiBuildStatus" />
        </div>
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

.sidebar-build {
  display: grid;
  gap: 0.2rem;
  color: var(--text-color-secondary);
  font-size: 0.72rem;
  line-height: 1.3;
}
</style>
