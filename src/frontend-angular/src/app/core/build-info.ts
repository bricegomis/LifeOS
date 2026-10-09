export interface BuildInfo {
  buildId: string
  versionMajor?: string | null
  buildNumber?: string | null
  runId?: string | null
  runAttempt?: string | null
  repository?: string | null
  serverUrl?: string | null
  workflow?: string | null
}

export function parseApiBuildInfo(value: unknown): BuildInfo {
  if (
    typeof value !== 'object' ||
    value === null ||
    !('component' in value) ||
    value.component !== 'api' ||
    !('buildId' in value) ||
    typeof value.buildId !== 'string' ||
    !value.buildId.trim()
  ) {
    throw new Error('Réponse de version API invalide')
  }
  const result: BuildInfo = { buildId: value.buildId }
  for (const field of [
    'versionMajor',
    'buildNumber',
    'runId',
    'runAttempt',
    'repository',
    'serverUrl',
    'workflow',
  ] as const) {
    if (field in value) {
      const entry: unknown = Reflect.get(value, field)
      if (entry !== null && typeof entry !== 'string') {
        throw new Error('Métadonnées de version API invalides')
      }
      if (
        entry &&
        ['versionMajor', 'buildNumber', 'runId', 'runAttempt'].includes(field) &&
        !/^[1-9]\d*$/.test(entry)
      ) {
        throw new Error('Identifiant de build API invalide')
      }
      result[field] = entry
    }
  }
  return result
}

export function formatBuildInfo(info: BuildInfo) {
  const major = info.versionMajor?.trim()
  const number = info.buildNumber?.trim()
  const local = ['local', 'dev'].includes(info.buildId)
  const label =
    major && /^[1-9]\d*$/.test(major) && number && /^[1-9]\d*$/.test(number)
      ? `${major}.${number}`
      : number && /^[1-9]\d*$/.test(number)
        ? `Build #${number}`
      : local
        ? info.buildId === 'dev'
          ? 'Local (développement)'
          : 'Local'
        : 'Build non identifié'
  return { label }
}
