import { createRouter, createWebHistory } from 'vue-router'
import WeeklyPlannerView from '@/views/WeeklyPlannerView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'weekly-planner',
      component: WeeklyPlannerView,
    },
  ],
})

export default router
