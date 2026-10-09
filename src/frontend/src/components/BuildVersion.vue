<script setup lang="ts">
import { computed } from 'vue'
import { formatBuildInfo, type BuildInfo } from '@/utils/buildInfo'

const props = defineProps<{ component: string; info?: BuildInfo | null; status?: string }>()
const version = computed(() => (props.info ? formatBuildInfo(props.info) : null))
</script>

<template>
  <div class="build-version">
    <span>{{ component }} </span>
    <template v-if="version">
      <a
        v-if="version.runUrl"
        :href="version.runUrl"
        target="_blank"
        rel="noopener noreferrer"
        :aria-label="`${component} ${version.label} — ouvrir le run GitHub Actions`"
        >{{ version.label }}</a
      >
      <span v-else>{{ version.label }}</span>
      <details v-if="version.details">
        <summary>Détails {{ component }}</summary>
        <span>{{ version.details }}</span>
      </details>
    </template>
    <span v-else>{{ status }}</span>
  </div>
</template>

<style scoped>
.build-version {
  overflow-wrap: anywhere;
}
a {
  color: inherit;
  text-decoration: underline;
}
details {
  margin-top: 0.2rem;
}
summary {
  cursor: pointer;
}
</style>
