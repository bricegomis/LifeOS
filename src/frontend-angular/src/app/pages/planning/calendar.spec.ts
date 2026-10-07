import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { addDays, dayEvents, layoutEvents, monday, outsideDisplay, parseTime, timeLabel } from './calendar.ts'
import type { ManualDay, ManualSport } from '../../core/api/manual-planner.models.ts'

const session = (id: string, startMinute: number | null, endMinute: number | null): ManualSport => ({
  id, dayPlanId: 'day', sportTemplateId: 'model', name: 'Vélo',
  sport: 'bike', intensity: 'moderate', durationMinutes: 30, distanceKm: 10,
  calories: 200, startMinute, endMinute,
})
const day = (sports: ManualSport[]): ManualDay => ({
  id: 'day', date: '2026-10-05', meals: [], sports,
  nutrition: { calories: 0, protein: 0, carbs: 0, fat: 0, isComplete: true, warnings: [] },
})

describe('calendar civil dates', () => {
  it('navigates independent weeks across DST and year boundaries', () => {
    assert.equal(monday('2026-10-07'), '2026-10-05')
    assert.equal(monday('2026-10-11'), '2026-10-05')
    assert.equal(addDays('2026-10-19', 7), '2026-10-26')
    assert.equal(addDays('2026-12-28', 7), '2027-01-04')
  })
  it('keeps unset times unset and accepts precise snacks', () => {
    assert.equal(timeLabel(null), 'À positionner')
    assert.equal(parseTime('16:30'), 990)
    assert.equal(timeLabel(990), '16:30')
    assert.equal(parseTime('25:00'), null)
    assert.equal(parseTime('16:99'), null)
    assert.equal(parseTime(''), null)
  })
})

describe('multiple and overlapping events', () => {
  it('assigns separate lanes for overlap, including short cards, without modifying data', () => {
    const source = day([session('a', 990, 1000), session('b', 1000, 1030), session('c', 1100, 1130)])
    const snapshot = JSON.stringify(source)
    const events = layoutEvents(source)
    assert.equal(events.length, 3)
    assert.equal(events[0]?.lanes, 2)
    assert.equal(events[1]?.lanes, 2)
    assert.notEqual(events[0]?.lane, events[1]?.lane)
    assert.equal(events[2]?.lanes, 1)
    assert.equal(JSON.stringify(source), snapshot)
  })
  it('keeps historic and out-of-display events accessible in the agenda', () => {
    const source = day([session('late', 1320, 1380), session('old', null, null), session('in', 1020, 1050)])
    assert.equal(layoutEvents(source).length, 1)
    assert.equal(dayEvents(source).length, 3)
    assert.equal(dayEvents(source).filter(outsideDisplay).length, 1)
    assert.equal(dayEvents(source).at(-1)?.value.id, 'old')
  })
})
