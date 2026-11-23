/** Geometry of the small line charts (VO2max, sleep and recovery), kept pure to be tested. */
export interface ChartPoint { label: string, value: number | null }

export interface LineGeometry {
  /** SVG polyline points in a 600 × 140 box; gaps (null values) split the line. */
  segments: string[]
  min: number
  max: number
  /** Optional band (e.g. the HRV usual range), as y and height in the box. */
  bandY: number
  bandHeight: number
  last: number | null
  ticks: string[]
}

export const CHART_WIDTH = 600
export const CHART_HEIGHT = 140

export function lineGeometry(points: ChartPoint[], band: [number, number] | null = null): LineGeometry {
  const values = points.map(point => point.value).filter((value): value is number => value !== null)
  if (!values.length) return { segments: [], min: 0, max: 0, bandY: 0, bandHeight: 0, last: null, ticks: [] }
  const all = band ? [...values, ...band] : values
  let low = Math.min(...all)
  let high = Math.max(...all)
  const pad = (high - low) * 0.15 || 1
  const min = low; const max = high
  low -= pad; high += pad
  const y = (value: number) => Math.round((CHART_HEIGHT - (value - low) / (high - low) * CHART_HEIGHT) * 10) / 10
  const step = CHART_WIDTH / Math.max(1, points.length - 1)
  const segments: string[] = []
  let current: string[] = []
  points.forEach((point, index) => {
    if (point.value === null) { if (current.length) segments.push(current.join(' ')); current = []; return }
    current.push(`${Math.round(index * step)},${y(point.value)}`)
  })
  if (current.length) segments.push(current.join(' '))
  const tickIndexes = points.length > 4
    ? [0, Math.round((points.length - 1) / 3), Math.round(2 * (points.length - 1) / 3), points.length - 1]
    : points.map((_, index) => index)
  return {
    segments, min, max,
    bandY: band ? y(band[1]) : 0, bandHeight: band ? y(band[0]) - y(band[1]) : 0,
    last: values[values.length - 1] ?? null,
    ticks: tickIndexes.map(index => points[index]!.label),
  }
}
