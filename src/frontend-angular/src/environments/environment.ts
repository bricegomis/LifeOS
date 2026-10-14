import type { BuildInfo } from '../app/core/build-info'
import { buildInfo as artifactBuildInfo } from './build-info.generated'

interface LifeOSRuntimeConfig {
  supabaseUrl?: string
  supabaseAnonKey?: string
  apiBaseUrl?: string
}

declare global {
  interface Window {
    LIFEOS_CONFIG?: LifeOSRuntimeConfig
  }
}

export const supabaseUrl = window.LIFEOS_CONFIG?.supabaseUrl?.trim() ?? ''
export const supabaseAnonKey = window.LIFEOS_CONFIG?.supabaseAnonKey?.trim() ?? ''
export const apiBaseUrl = window.LIFEOS_CONFIG?.apiBaseUrl?.trim().replace(/\/+$/, '') ?? ''
export const buildInfo: BuildInfo = artifactBuildInfo
export const baseUrl = document.querySelector('base')?.getAttribute('href') ?? '/'
