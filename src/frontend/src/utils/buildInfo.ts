export interface BuildInfo {
  buildId: string
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
        ['buildNumber', 'runId', 'runAttempt'].includes(field) &&
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
  const number = info.buildNumber?.trim()
  const attempt = info.runAttempt?.trim()
  const local = ['local', 'dev'].includes(info.buildId)
  const label =
    number && /^[1-9]\d*$/.test(number)
      ? `Build #${number}${attempt && attempt !== '1' ? ` · tentative ${attempt}` : ''}`
      : local
        ? info.buildId === 'dev'
          ? 'Local (développement)'
          : 'Local'
        : 'Build non identifié'
  let runUrl: string | null = null
  if (
    info.serverUrl &&
    URL.canParse(info.serverUrl) &&
    info.repository &&
    info.runId &&
    /^[1-9]\d*$/.test(info.runId)
  ) {
    const server = new URL(info.serverUrl)
    if (
      server.protocol === 'https:' &&
      !server.username &&
      !server.password &&
      server.pathname === '/' &&
      !server.search &&
      !server.hash &&
      /^[\w-]+\/[\w-][\w.-]*$/.test(info.repository)
    ) {
      runUrl = `${server.origin}/${info.repository}/actions/runs/${info.runId}${attempt ? `/attempts/${attempt}` : ''}`
    }
  }
  const details = [
    info.workflow,
    info.runId ? `Run ${info.runId}` : null,
    attempt ? `Tentative ${attempt}` : null,
    !local ? `Commit ${info.buildId}` : null,
  ]
    .filter(Boolean)
    .join(' · ')
  return { label, runUrl, details }
}
