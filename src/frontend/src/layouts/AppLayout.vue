<script setup lang="ts">
const navigationItems = [
  { to: '/', label: "Aujourd'hui", mobileLabel: 'Auj.', icon: 'pi pi-sun' },
  { to: '/planner', label: 'Planning', mobileLabel: 'Planning', icon: 'pi pi-calendar' },
  { to: '/library', label: 'Bibliothèque', mobileLabel: 'Biblio', icon: 'pi pi-book' },
  { to: '/settings', label: 'Réglages', mobileLabel: 'Réglages', icon: 'pi pi-cog' },
]
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
