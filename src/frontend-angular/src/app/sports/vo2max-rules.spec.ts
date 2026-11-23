import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { monthlyVo2, vo2Summary } from './vo2max-rules.ts'
import type { Vo2MaxDto } from './sports.models.ts'

const m = (date: string, value: number, sport: 'running' | 'cycling' = 'running'): Vo2MaxDto => ({ date, sport, sportLabel: '', value })

describe('VO2max', () => {
  it('keeps the last value of each month and starts with the data', () => {
    const points = monthlyVo2([m('2025-12-31', 46.5), m('2026-10-02', 48.8), m('2026-10-28', 49.4), m('2026-11-09', 49.2), m('2026-11-01', 60, 'cycling')],
      'running', '2026-11-23', 24)
    assert.equal(points[0]!.label, 'déc.')
    assert.equal(points[0]!.value, 46.5)
    assert.equal(points.at(-2)!.value, 49.4)
    assert.equal(points.at(-1)!.value, 49.2)
    assert.equal(points[1]!.label, 'janv. 2026')
    assert.equal(points[1]!.value, null)
    assert.deepEqual(monthlyVo2([], 'running', '2026-11-23', 24), [])
  })

  it('sums up the last value, the change over a year and the best', () => {
    const summary = vo2Summary([m('2025-11-01', 46.5), m('2026-10-28', 49.4), m('2026-11-09', 49.2)], 'running', '2026-11-23')
    assert.deepEqual(summary, { last: 49.2, lastDay: 'le 9 nov. 2026', change: 2.7, best: 49.4 })
    assert.equal(vo2Summary([], 'cycling', '2026-11-23'), null)
  })
})
