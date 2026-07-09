<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const sending = ref(false)
const notice = ref('')

const redirectPath = computed(() => {
  const value = route.query.redirect

  return typeof value === 'string' && value.trim() ? value : '/'
})

const readyLabel = computed(() => {
  if (authStore.errorMessage) {
    return authStore.errorMessage
  }

  if (!authStore.configured) {
    return 'Supabase n’est pas configuré dans cet environnement.'
  }

  if (authStore.loading) {
    return 'Vérification de la session...'
  }

  return 'Connexion par lien magique par email.'
})

async function submit(): Promise<void> {
  if (!email.value.trim()) {
    notice.value = 'Entre une adresse email valide.'
    return
  }

  sending.value = true
  notice.value = ''

  try {
    const callbackPath = `/login?redirect=${encodeURIComponent(redirectPath.value)}`

    await authStore.sendMagicLink(email.value.trim(), callbackPath)
    notice.value = 'Un lien de connexion a été envoyé.'
  } catch (error) {
    notice.value = error instanceof Error ? error.message : 'Impossible d’envoyer le lien de connexion.'
  } finally {
    sending.value = false
  }
}

onMounted(async () => {
  await authStore.ensureReady()

  if (authStore.authenticated) {
    await router.replace(redirectPath.value)
  }
})
</script>

<template>
  <main class="login-page">
    <section class="login-panel">
      <div class="login-copy">
        <p class="eyebrow">LifeOS</p>
        <h1>Connexion</h1>
        <p>Accède à tes paramètres et à tes plannings depuis plusieurs appareils avec un lien magique Supabase.</p>
      </div>

      <form class="login-form" @submit.prevent="submit">
        <label class="form-field">
          <span>Email</span>
          <InputText v-model="email" type="email" autocomplete="email" placeholder="toi@exemple.com" />
        </label>

        <Button :label="sending ? 'Envoi...' : 'Recevoir le lien'" type="submit" :loading="sending" />

        <p class="login-hint">{{ readyLabel }}</p>
        <p v-if="notice" class="login-feedback">{{ notice }}</p>
      </form>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 2rem;
  background:
    radial-gradient(circle at top left, rgba(67, 102, 163, 0.16), transparent 34%),
    radial-gradient(circle at bottom right, rgba(122, 160, 108, 0.18), transparent 30%),
    var(--surface-ground);
}

.login-panel {
  width: min(100%, 34rem);
  display: grid;
  gap: 1.25rem;
  padding: 1.6rem;
  border: 1px solid var(--surface-border);
  border-radius: 22px;
  background: var(--surface-card);
  box-shadow: 0 22px 55px rgba(15, 23, 42, 0.1);
}

.login-copy {
  display: grid;
  gap: 0.5rem;
}

.login-copy h1 {
  margin: 0;
  font-size: clamp(2rem, 4vw, 2.8rem);
  line-height: 1.05;
}

.login-copy p {
  margin: 0;
  color: var(--text-color-secondary);
  line-height: 1.6;
}

.login-form {
  display: grid;
  gap: 0.95rem;
}

.login-hint {
  margin: 0;
  color: var(--text-color-secondary);
  font-size: 0.92rem;
}

.login-feedback {
  margin: 0;
  color: var(--lifeos-accent);
  font-weight: 700;
}
</style>
