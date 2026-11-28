import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  challengeItems, daySummary, doneItems, openSections, sportRoutineItems, supplementItems, taskItems, withMarks,
  type TodayChallenge, type TodaySportRoutine, type TodaySupplement, type TodayTodo, type TodayTaskRoutine,
} from './today-items.ts'

const today = '2026-11-26'
const todo = (over: Partial<TodayTodo>): TodayTodo => ({
  id: 't', title: 'Todo', dueOn: today, durationMinutes: null, state: 'open', doneOn: null, isLate: false, checklist: [], projectTitle: null, ...over,
})
const routine = (over: Partial<TodayTaskRoutine>): TodayTaskRoutine => ({
  id: 'r', title: 'Routine', state: 'active', nextDueOn: today, isLate: false, streak: 0, doneToday: false, ...over,
})

describe('today: tasks', () => {
  it('keeps todos due today or late and those done today, and leaves the rest out', () => {
    const items = taskItems({ today, routines: [], todos: [
      todo({ id: 'a', dueOn: '2026-11-24', isLate: true, durationMinutes: 30 }),
      todo({ id: 'b', checklist: [{ done: true }, { done: false }], projectTitle: 'Chambre' }),
      todo({ id: 'c', dueOn: '2026-11-27' }),
      todo({ id: 'd', dueOn: null }),
      todo({ id: 'e', state: 'done', doneOn: today, dueOn: '2026-11-20' }),
      todo({ id: 'f', state: 'done', doneOn: '2026-11-25' }),
    ] })
    assert.deepEqual(items.map(i => [i.id, i.section, i.done]), [['a', 'late', false], ['b', 'tasks', false], ['e', 'tasks', true]])
    assert.equal(items[0]!.sub, 'Tâche · en retard de 2 jours · 30 min')
    assert.equal(items[1]!.sub, 'Tâche · checklist 1/2 · Chambre')
  })

  it('keeps household routines due today, late or done today', () => {
    const items = taskItems({ today, todos: [], routines: [
      routine({ id: 'a', nextDueOn: '2026-11-25', streak: 3 }),
      routine({ id: 'b' }),
      routine({ id: 'c', doneToday: true, nextDueOn: '2026-12-03' }),
      routine({ id: 'd', nextDueOn: '2026-11-28' }),
      routine({ id: 'e', state: 'paused', nextDueOn: null }),
    ] })
    assert.deepEqual(items.map(i => [i.id, i.section, i.done]), [['a', 'late', false], ['b', 'tasks', false], ['c', 'tasks', true]])
    assert.equal(items[0]!.sub, 'Routine du foyer · prévue hier · série 3')
  })
})

describe('today: sport', () => {
  const report = (over: Partial<TodaySportRoutine>): TodaySportRoutine => ({
    routine: { id: 's', name: 'Gainage', lines: [1, 2, 3] }, plannedToday: true, completedToday: false, streak: 12, todaySessions: [], ...over,
  })

  it('shows routines planned today, with the pass in progress', () => {
    const items = sportRoutineItems([
      report({}),
      report({ routine: { id: 'p', name: 'Mobilité', lines: [1] }, streak: 0, todaySessions: [{ finished: false, exercisesDone: 2, exercisesTotal: 5 }] }),
      report({ routine: { id: 'n', name: 'Repos', lines: [] }, plannedToday: false }),
      report({ routine: { id: 'd', name: 'Faite', lines: [1] }, completedToday: true }),
    ])
    assert.deepEqual(items.map(i => [i.id, i.done, i.inProgress]), [['s', false, false], ['p', false, true], ['d', true, false]])
    assert.equal(items[0]!.sub, '3 exercices · série 12 j')
    assert.equal(items[1]!.sub, 'Passage en cours · 2/5 exercices')
    assert.equal(items[0]!.link, '/sports/routine/s')
  })

  it('shows challenges with today’s progress; reached ones are done', () => {
    const challenge = (over: Partial<TodayChallenge>): TodayChallenge => ({
      challenge: { id: 'c', name: 'Pompes', unit: 'reps', isActive: true }, todayTotal: 40, todayTarget: 100, reachedToday: false, streak: 12, todayEntries: [], ...over,
    })
    const items = challengeItems([challenge({}), challenge({ challenge: { id: 'm', name: 'Planche', unit: 'minutes', isActive: true }, todayTotal: 6, todayTarget: 5, reachedToday: true, streak: 0 })])
    assert.equal(items[0]!.sub, '40 / 100 rép. aujourd’hui · série 12 j')
    assert.equal(items[0]!.percent, 40)
    assert.deepEqual(items[0]!.steps, [10, 20, 25])
    assert.equal(items[1]!.done, true)
    assert.equal(items[1]!.percent, 100)
  })
})

describe('today: supplements and sections', () => {
  const supplement = (over: Partial<TodaySupplement>): TodaySupplement => ({
    id: 's', name: 'Zinc', dose: 1, unit: 'gelule', moment: 'soir', isStopped: false, status: { active: true }, takenToday: false, ...over,
  })

  it('keeps active supplements and groups the open ones by moment', () => {
    const items = supplementItems([
      supplement({ id: 'z' }),
      supplement({ id: 'o', name: 'Oméga-3', dose: 2, moment: 'matin' }),
      supplement({ id: 'd', name: 'D3', unit: 'goutte', moment: 'matin', takenToday: true }),
      supplement({ id: 'p', status: { active: false } }),
      supplement({ id: 'x', isStopped: true }),
    ])
    assert.deepEqual(items.map(i => i.id), ['z', 'o', 'd'])
    assert.equal(items[1]!.sub, '2 gélules')
    const sections = openSections(items)
    assert.deepEqual(sections.map(s => s.key), ['supplements'])
    assert.deepEqual(sections[0]!.groups.map(g => [g.label, g.items.map(i => i.id)]), [['Matin', ['o']], ['Soir', ['z']]])
    assert.deepEqual(doneItems(items).map(i => i.id), ['d'])
  })

  it('orders sections and summarises the day', () => {
    const items = [
      ...supplementItems([supplement({})]),
      ...taskItems({ today, todos: [todo({ id: 'l', dueOn: '2026-11-20' }), todo({ id: 'k', state: 'done', doneOn: today })], routines: [] }),
    ]
    assert.deepEqual(openSections(items).map(s => s.key), ['late', 'supplements'])
    assert.deepEqual(daySummary(items), { open: 2, done: 1, percent: 33, text: '2 à faire, 1 fait' })
    assert.equal(daySummary([]).text, 'rien de prévu')
  })

  it('shows a tick at once, before the server answers', () => {
    const items = supplementItems([
      { id: 'a', name: 'D3', dose: 1, unit: 'gelule', moment: 'matin', isStopped: false, status: { active: true }, takenToday: false },
      { id: 'b', name: 'Oméga 3', dose: 1, unit: 'gelule', moment: 'matin', isStopped: false, status: { active: true }, takenToday: true },
    ])
    assert.equal(withMarks(items, new Map()), items)
    const marked = withMarks(items, new Map([['supplement-a', true], ['supplement-b', false]]))
    assert.deepEqual(marked.map(item => item.done), [true, false])
    assert.deepEqual(doneItems(marked).map(item => item.id), ['a'])
    assert.equal(items[0]!.done, false)
  })
})
