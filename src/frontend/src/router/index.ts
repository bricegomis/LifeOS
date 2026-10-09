import { createRouter, createWebHashHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { routes } from './routes'

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes,
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
