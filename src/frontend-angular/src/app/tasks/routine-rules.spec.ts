import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { recurrenceLabel, routineDraftOf, routineMeta, routineRequest } from './routine-rules.ts'
import type { RecurrenceDto, TaskRoutineDto } from './tasks.models.ts'

const today = '2026-11-22'
const fixed = (more: Partial<RecurrenceDto>): RecurrenceDto => ({ kind: 'fixed', unit: 'week', every: 1, days: [], monthDay: 1, delayDays: 7, ...more })

function routine(more: Partial<TaskRoutineDto> = {}): TaskRoutineDto {
  return {
    id: 'r', memberProfileId: 'p', title: 'Poubelles', notes: null, durationMinutes: 5, priority: 'normal', tags: [],
    recurrence: fixed({ days: [1, 4] }), startOn: today, state: 'active', nextDueOn: '2026-11-23', isLate: false, streak: 0, record: 0,
    lastDoneOn: null, doneToday: false, hasImage: false, updatedAt: '', ...more,
  }
}

describe('routine recurrence', () => {
  it('reads every kind of recurrence', () => {
    assert.equal(recurrenceLabel(fixed({ days: [1, 4] })), 'Chaque lundi et jeudi')
    assert.equal(recurrenceLabel(fixed({ every: 2, days: [6] })), 'Toutes les 2 semaines, le samedi')
    assert.equal(recurrenceLabel(fixed({ unit: 'day', every: 3 })), 'Tous les 3 jours')
    assert.equal(recurrenceLabel(fixed({ unit: 'month', monthDay: 1 })), 'Le 1er de chaque mois')
    assert.equal(recurrenceLabel(fixed({ unit: 'month', every: 3, monthDay: 25 })), 'Le 25, tous les 3 mois')
    assert.equal(recurrenceLabel({ ...fixed({}), kind: 'delay', delayDays: 7 }), '7 jours après la dernière fois')
  })

  it('shows the next due day, late or paused', () => {
    assert.equal(routineMeta(routine(), today, true), 'Prochaine : demain · Chaque lundi et jeudi · 5 min')
    assert.equal(routineMeta(routine({ nextDueOn: '2026-11-20', isLate: true }), today, true), 'En retard · ven. 20 nov. · Chaque lundi et jeudi · 5 min')
    assert.equal(routineMeta(routine({ state: 'paused', nextDueOn: null }), today, true), 'En pause · Chaque lundi et jeudi · 5 min')
  })
})

describe('routine form', () => {
  it('builds the request of each recurrence and checks it', () => {
    const draft = routineDraftOf(null, today)
    assert.equal(routineRequest(draft), 'Le titre est obligatoire.')
    assert.deepEqual(routineRequest({ ...draft, title: ' Poubelles ', days: [4, 1] }), {
      title: 'Poubelles', notes: null, durationMinutes: null, priority: 'normal', tags: [], startOn: today,
      recurrence: { kind: 'fixed', unit: 'week', every: 1, days: [1, 4], monthDay: null, delayDays: null },
    })
    assert.equal(routineRequest({ ...draft, title: 'A', days: [] }), 'Choisis au moins un jour de la semaine.')
    assert.equal(routineRequest({ ...draft, title: 'A', unit: 'month', monthDay: 32 }), 'Jour du mois : de 1 à 31.')
    assert.deepEqual((routineRequest({ ...draft, title: 'A', kind: 'delay', delayDays: '10' }) as { recurrence: unknown }).recurrence,
      { kind: 'delay', unit: null, every: null, days: null, monthDay: null, delayDays: 10 })
    assert.equal(routineRequest({ ...draft, title: 'A', kind: 'delay', delayDays: 0 }), 'Délai : de 1 à 365 jours.')
  })

  it('starts from the routine when editing', () => {
    const draft = routineDraftOf(routine({ recurrence: fixed({ unit: 'month', monthDay: 25 }) }), today)
    assert.deepEqual([draft.unit, draft.monthDay, draft.days], ['month', 25, [1]])
  })
})
