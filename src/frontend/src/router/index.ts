import { createRouter, createWebHistory } from 'vue-router'
import LibraryView from '@/views/LibraryView.vue'
import SettingsView from '@/views/SettingsView.vue'
import TodayView from '@/views/TodayView.vue'
import WeeklyPlannerView from '@/views/WeeklyPlannerView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'today',
      component: TodayView,
    },
    {
      path: '/planner',
      name: 'weekly-planner',
      component: WeeklyPlannerView,
    },
    {
      path: '/library',
      name: 'library',
      component: LibraryView,
    },
    {
      path: '/settings',
      name: 'settings',
      component: SettingsView,
    },
  ],
})

export default router
