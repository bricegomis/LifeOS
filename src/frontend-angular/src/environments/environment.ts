interface LifeOSRuntimeConfig {
  supabaseUrl?: string
  supabaseAnonKey?: string
  apiBaseUrl?: string
  buildId?: string
}

declare global {
  interface Window {
    LIFEOS_CONFIG?: LifeOSRuntimeConfig
  }
}

export const supabaseUrl = window.LIFEOS_CONFIG?.supabaseUrl?.trim() ?? ''
export const supabaseAnonKey = window.LIFEOS_CONFIG?.supabaseAnonKey?.trim() ?? ''
export const apiBaseUrl = window.LIFEOS_CONFIG?.apiBaseUrl?.trim().replace(/\/+$/, '') ?? ''
export const buildId = window.LIFEOS_CONFIG?.buildId?.trim() || 'local'
export const baseUrl = document.querySelector('base')?.getAttribute('href') ?? '/'
