import type { RouteRecordRaw } from 'vue-router'
import AppLayout from '@/layouts/AppLayout.vue'
import ArticlesView from '@/views/ArticlesView.vue'
import LibraryView from '@/views/LibraryView.vue'
import LoginView from '@/views/LoginView.vue'
import SettingsView from '@/views/SettingsView.vue'
import StoresView from '@/views/StoresView.vue'
import TodayView from '@/views/TodayView.vue'
import WeeklyPlannerView from '@/views/WeeklyPlannerView.vue'

export const routes: RouteRecordRaw[] = [
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
        path: 'stores',
        name: 'stores',
        component: StoresView,
      },
      {
        path: 'articles',
        name: 'articles',
        component: ArticlesView,
      },
      {
        path: 'settings',
        name: 'settings',
        component: SettingsView,
      },
    ],
  },
]
