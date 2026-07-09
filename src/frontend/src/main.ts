import { createApp } from 'vue'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'

import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/auth'
import 'primeicons/primeicons.css'
import './styles.css'

const app = createApp(App)
const pinia = createPinia()

app.use(PrimeVue, {
  ripple: true,
  theme: {
    preset: Aura,
    options: {
      darkModeSelector: false,
    },
  },
})
app.use(pinia)

const authStore = useAuthStore(pinia)
void authStore.initialize()

app.use(router)

app.mount('#app')
