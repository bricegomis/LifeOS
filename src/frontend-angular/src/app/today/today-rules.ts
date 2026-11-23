/** The day at a glance (ADR 0019): the night, energy, sessions, steps and the week's goals, read from the published API. */
export interface TodayNight {
  sleepSeconds: number | null
  sleepScore: number | null
  sleepQuality: string | null
  restingHeartRate: number | null
  hrvNightAverage: number | null
  hrvStatusLabel: string | null
  hrvBaselineLow: number | null
  hrvBaselineHigh: number | null
  bodyBatteryWake: number | null
  stressAverage: number | null
  steps: number | null
}

const QUALITY: Record<string, string> = { POOR: 'faible', FAIR: 'correct', GOOD: 'bon', EXCELLENT: 'excellent' }

export function sleepText(night: TodayNight | null): { value: string, sub: string } {
  if (!night || night.sleepSeconds === null) return { value: '—', sub: 'pas de données' }
  const minutes = Math.round(night.sleepSeconds / 60)
  const quality = night.sleepQuality ? QUALITY[night.sleepQuality.toUpperCase()] ?? night.sleepQuality.toLowerCase() : ''
  const sub = [night.sleepScore !== null ? `score ${night.sleepScore}` : '', quality].filter(Boolean).join(' · ')
  return { value: `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')}`, sub }
}

export function hrvText(night: TodayNight | null): { value: string, sub: string } {
  if (!night || night.hrvNightAverage === null) return { value: '—', sub: '' }
  const range = night.hrvBaselineLow !== null && night.hrvBaselineHigh !== null ? ` (${night.hrvBaselineLow}–${night.hrvBaselineHigh})` : ''
  return { value: `${night.hrvNightAverage} ms`, sub: `${night.hrvStatusLabel ?? ''}${range}`.trim() }
}

export function stepsText(steps: number | null): string {
  return steps === null ? '—' : String(steps).replace(/\B(?=(\d{3})+(?!\d))/g, ' ')
}
