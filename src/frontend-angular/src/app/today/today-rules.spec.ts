import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { hrvText, sleepText, stepsText, type TodayNight } from './today-rules.ts'

const night: TodayNight = { sleepSeconds: 22380, sleepScore: 68, sleepQuality: 'FAIR', restingHeartRate: 49, hrvNightAverage: 43, hrvStatusLabel: 'Équilibré',
  hrvBaselineLow: 37, hrvBaselineHigh: 47, bodyBatteryWake: 62, stressAverage: 30, steps: 8775 }

describe('today at a glance', () => {
  it('says the night in words', () => {
    assert.deepEqual(sleepText(night), { value: '6 h 13', sub: 'score 68 · correct' })
    assert.deepEqual(sleepText(null), { value: '—', sub: 'pas de données' })
    assert.deepEqual(hrvText(night), { value: '43 ms', sub: 'Équilibré (37–47)' })
    assert.equal(stepsText(8775), '8 775')
    assert.equal(stepsText(null), '—')
  })
})
