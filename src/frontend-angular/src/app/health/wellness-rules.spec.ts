import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { average, buckets, hoursMinutes, hrvBand, series, sleepColumns } from './wellness-rules.ts'
import type { WellnessDto } from './health.models.ts'

function day(date: string, patch: Partial<WellnessDto> = {}): WellnessDto {
  return {
    date, sleepStart: null, sleepEnd: null, sleepSeconds: 22380, deepSleepSeconds: 5220, lightSleepSeconds: 14820, remSleepSeconds: 2340,
    awakeSeconds: 3360, sleepScore: 68, sleepQuality: 'FAIR', restingHeartRate: 49, hrvNightAverage: 43, hrvWeeklyAverage: 42,
    hrvBaselineLow: 37, hrvBaselineHigh: 47, hrvStatus: 'balanced', hrvStatusLabel: 'Équilibré', respiration: 12.5, spO2: null,
    bodyBatteryWake: 62, bodyBatteryHigh: 80, bodyBatteryLow: 20, stressAverage: 30, steps: 4210, updatedAt: '', ...patch,
  }
}

describe('sleep and recovery', () => {
  it('cuts a period into nights, weeks or months', () => {
    assert.equal(buckets('4w', '2026-11-23').length, 28)
    assert.equal(buckets('4w', '2026-11-23').at(-1)!.label, '23/11')
    assert.equal(buckets('3m', '2026-11-23').length, 13)
    const months = buckets('1y', '2026-11-23')
    assert.equal(months.length, 12)
    assert.deepEqual([months[0]!.label, months[0]!.from, months.at(-1)!.to], ['déc.', '2025-12-01', '2026-11-30'])
  })

  it('averages known values only and leaves a night without the watch empty', () => {
    const days = [day('2026-11-22', { restingHeartRate: 51 }), day('2026-11-23', { restingHeartRate: null })]
    assert.equal(average(days, 'restingHeartRate'), 51)
    const points = series(days, '4w', '2026-11-23', 'hrvNightAverage')
    assert.equal(points.at(-1)!.value, 43)
    assert.equal(points.at(-3)!.value, null)
    const columns = sleepColumns(days, '4w', '2026-11-23')
    assert.equal(columns.at(-3)!.empty, true)
    assert.ok(columns.at(-1)!.deep > 0 && columns.at(-1)!.light > columns.at(-1)!.deep)
  })

  it('formats durations and keeps the latest HRV range', () => {
    assert.equal(hoursMinutes(22380), '6 h 13')
    assert.equal(hoursMinutes(null), '—')
    assert.deepEqual(hrvBand([day('2026-11-22', { hrvBaselineLow: 35, hrvBaselineHigh: 45 }), day('2026-11-23')]), [37, 47])
    assert.equal(hrvBand([]), null)
  })
})
