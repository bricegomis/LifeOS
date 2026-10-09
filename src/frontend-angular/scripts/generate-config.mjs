import { existsSync, mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

for (const file of ['.env', '.env.local']) {
  const path = resolve(file)

  if (existsSync(path)) {
    process.loadEnvFile(path)
  }
}

const publicDirectory = resolve('public')
mkdirSync(publicDirectory, { recursive: true })

const config = {
  supabaseUrl: process.env.VITE_SUPABASE_URL?.trim() ?? '',
  supabaseAnonKey: process.env.VITE_SUPABASE_ANON_KEY?.trim() ?? '',
  apiBaseUrl: process.env.VITE_LIFEOS_API_URL?.trim().replace(/\/+$/, '') ?? '',
}

const buildInfo = {
  buildId: process.env.LIFEOS_BUILD_ID?.trim() || 'local',
  buildNumber: process.env.LIFEOS_BUILD_NUMBER?.trim() || null,
  runId: process.env.LIFEOS_RUN_ID?.trim() || null,
  runAttempt: process.env.LIFEOS_RUN_ATTEMPT?.trim() || null,
  repository: process.env.LIFEOS_BUILD_REPOSITORY?.trim() || null,
  serverUrl: process.env.LIFEOS_BUILD_SERVER_URL?.trim() || null,
  workflow: process.env.LIFEOS_BUILD_WORKFLOW?.trim() || null,
}

// Build provenance belongs to the artifact, not to the runtime deployment config.
const environmentDirectory = resolve('src/environments')
mkdirSync(environmentDirectory, { recursive: true })
writeFileSync(
  resolve(environmentDirectory, 'build-info.generated.ts'),
  `export const buildInfo = ${JSON.stringify(buildInfo)};\n`,
  { mode: 0o644 },
)

writeFileSync(
  resolve(publicDirectory, 'config.js'),
  `window.LIFEOS_CONFIG = ${JSON.stringify(config)};\n`,
  { mode: 0o644 },
)
