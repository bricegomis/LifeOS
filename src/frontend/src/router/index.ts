import { createRouter, createWebHashHistory } from 'vue-router'
import AppLayout from '@/layouts/AppLayout.vue'
import { useAuthStore } from '@/stores/auth'
import LibraryView from '@/views/LibraryView.vue'
import LoginView from '@/views/LoginView.vue'
import SettingsView from '@/views/SettingsView.vue'
import TodayView from '@/views/TodayView.vue'
import WeeklyPlannerView from '@/views/WeeklyPlannerView.vue'

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: LoginView,
    },
    {
      path: '/',
      component: AppLayout,
      children: [
        {
          path: '',
          name: 'today',
          component: TodayView,
        },
        {
          path: 'planner',
          name: 'weekly-planner',
          component: WeeklyPlannerView,
        },
        {
          path: 'library',
          name: 'library',
          component: LibraryView,
        },
        {
          path: 'settings',
          name: 'settings',
          component: SettingsView,
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  const authStore = useAuthStore()

  await authStore.ensureReady()

  if (to.name === 'login') {
    if (authStore.authenticated) {
      const redirect = typeof to.query.redirect === 'string' && to.query.redirect.trim()
        ? to.query.redirect
        : '/'

      return redirect
    }

    return true
  }

  if (!authStore.authenticated) {
    return {
      name: 'login',
      query: {
        redirect: to.fullPath,
      },
    }
  }

  return true
})

export default router
