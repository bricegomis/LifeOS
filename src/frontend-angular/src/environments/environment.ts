import type { BuildInfo } from '../app/core/build-info'
import { buildInfo as artifactBuildInfo } from './build-info.generated'

interface LifeOSRuntimeConfig {
  apiBaseUrl?: string
}

declare global {
  interface Window {
    LIFEOS_CONFIG?: LifeOSRuntimeConfig
  }
}

export const apiBaseUrl = window.LIFEOS_CONFIG?.apiBaseUrl?.trim().replace(/\/+$/, '') ?? ''
export const buildInfo: BuildInfo = artifactBuildInfo
export const baseUrl = document.querySelector('base')?.getAttribute('href') ?? '/'
