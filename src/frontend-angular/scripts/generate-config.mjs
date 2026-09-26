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

writeFileSync(
  resolve(publicDirectory, 'config.js'),
  `window.LIFEOS_CONFIG = ${JSON.stringify(config)};\n`,
  { mode: 0o644 },
)
