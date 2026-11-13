import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  applyTemplate, calendarWeeks, celebration, challengeAmount, dayLevel, duration, emptySessionDraft, goalProgressText, pace,
  quickSteps, sessionRequest, shortDay, streakLine,
} from './sport-format.ts'
import type { ChallengeReportDto, GoalProgressDto, SportTemplateDto } from './sports.models.ts'

const sports = [{ code: 'running', label: 'Course', hasDistance: true }, { code: 'strength', label: 'Musculation', hasDistance: false }]

function report(patch: Partial<ChallengeReportDto>): ChallengeReportDto {
  return {
    challenge: { id: 'c', name: 'Tractions', unit: 'reps', dailyTarget: 30, jokersPerWeek: 1, startedOn: '2026-04-06', stoppedOn: null, isActive: true },
    today: '2026-11-13', todayTotal: 10, todayTarget: 30, reachedToday: false, streak: 9, record: 12, jokersPerWeek: 1,
    jokersLeftThisWeek: 0, total: 4_975, totalThisYear: 4_975, nextMilestone: 5_000, milestones: [1_000, 5_000, 10_000],
    todayEntries: [], calendar: [], ...patch,
  }
}

describe('sport formatting', () => {
  it('formats durations, days and paces in French', () => {
    assert.equal(duration(46), '46 min')
    assert.equal(duration(161), '2 h 41')
    assert.equal(duration(120), '2 h')
    assert.equal(shortDay('2026-11-13'), 'ven. 13')
    assert.equal(pace('running', 46, 8.2), 'Allure : 5 min 37 /km')
    assert.equal(pace('cycling', 30, 12), 'Vitesse : 24,0 km/h')
    assert.equal(pace('running', 46, null), '')
  })

  it('shows goal progress and keeps unknown calories visible', () => {
    const progress = (kind: 'sessions' | 'volume', unit: 'kcal' | 'hours' | null, achieved: number, partial = false): GoalProgressDto => ({
      goal: { id: 'g', kind, kindLabel: '', templateId: null, templateName: null, sport: null, unit, target: kind === 'volume' ? 1500 : 4, label: '', validFrom: '', validUntil: null },
      achieved, reached: false, partial,
    })
    assert.equal(goalProgressText(progress('sessions', null, 3)), '3 / 4')
    assert.equal(goalProgressText(progress('volume', 'kcal', 900, true)), '900 / 1 500 (partiel)')
  })

  it('celebrates a crossed milestone before the reached target', () => {
    const before = report({})
    assert.equal(celebration(before, report({ total: 5_000, todayTotal: 35, reachedToday: true, streak: 10 })), 'Palier atteint : 5 000 tractions au total !')
    assert.equal(celebration(before, report({ total: 4_995, todayTotal: 30, reachedToday: true, streak: 10 })), 'Tractions : défi du jour atteint. Série de 10 jours.')
    assert.equal(celebration(before, report({ total: 4_980, todayTotal: 15 })), '')
    assert.equal(streakLine(before), 'Série : 9 jours (aujourd’hui en cours) · record 12')
  })

  it('offers quick steps that fit the target and shows minutes as hours', () => {
    assert.deepEqual(quickSteps('reps', 100), [10, 20, 25])
    assert.deepEqual(quickSteps('reps', 30), [5, 10])
    assert.deepEqual(quickSteps('minutes', 20), [5, 10, 20])
    assert.equal(challengeAmount('minutes', 2_460), '41 h')
    assert.equal(challengeAmount('minutes', 90), '1 h 30')
  })

  it('lays the calendar out in Monday-first weeks', () => {
    const days = ['2026-11-12', '2026-11-13'].map(date => ({ date }))
    const weeks = calendarWeeks(days)
    assert.equal(weeks.length, 1)
    assert.deepEqual(weeks[0]!.map(day => day?.date ?? null), [null, null, null, '2026-11-12', '2026-11-13', null, null])
    assert.equal(dayLevel('joker', 0), 'joker')
    assert.equal(dayLevel('in_progress', 45), 'low')
    assert.equal(dayLevel('reached', 100), 'full')
  })

  it('copies a template and builds a valid request', () => {
    const template: SportTemplateDto = { id: 't', name: 'Footing', sport: 'running', sportLabel: 'Course', durationMinutes: 45, distanceKm: 8, calories: 500, isArchived: false }
    const draft = applyTemplate(emptySessionDraft('2026-11-13'), template)
    const request = sessionRequest(draft, sports)
    assert.equal(typeof request, 'object')
    assert.deepEqual(request, { date: '2026-11-13', sport: 'running', templateId: 't', durationMinutes: 45, distanceKm: 8, calories: 500, feeling: null, averageHeartRate: null, maxHeartRate: null, notes: '' })
    const strength = sessionRequest({ ...draft, sport: 'strength', calories: null }, sports)
    assert.ok(typeof strength === 'object' && strength.distanceKm === null && strength.calories === null)
    assert.equal(sessionRequest(emptySessionDraft('2026-11-13'), sports), 'La durée est obligatoire.')
  })
})
