import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  addDays, dayLabel, draftOf, projectProgress, projectTree, subTreeIds, type TaskItem, durationLabel, initial, postponeChoices, postponed, tagsOf, todaySections, todoMeta, todoRequest, withTag,
} from './task-format.ts'
import type { TaskRoutineDto, TodoDto } from './tasks.models.ts'

const today = '2026-11-22' // a Sunday

function todo(title: string, more: Partial<TodoDto> = {}): TodoDto {
  return {
    id: title, memberProfileId: 'p', title, notes: null, dueOn: null, durationMinutes: null, priority: 'normal', tags: [],
    state: 'open', doneOn: null, isLate: false, hasImage: false, checklist: [], updatedAt: '', projectId: null, projectTitle: null, ...more,
  }
}

function routine(title: string, more: Partial<TaskRoutineDto> = {}): TaskRoutineDto {
  return {
    id: title, memberProfileId: 'p', title, notes: null, durationMinutes: null, priority: 'normal', tags: [],
    recurrence: { kind: 'delay', unit: 'day', every: 1, days: [], monthDay: 1, delayDays: 7 }, startOn: today, state: 'active',
    nextDueOn: today, isLate: false, streak: 0, record: 0, lastDoneOn: null, doneToday: false, hasImage: false, updatedAt: '', ...more,
  }
}

describe('task days', () => {
  it('names nearby days and keeps calendar days', () => {
    assert.equal(dayLabel(today, today), 'Aujourd’hui')
    assert.equal(dayLabel('2026-11-23', today), 'Demain')
    assert.equal(dayLabel('2026-11-21', today), 'Hier')
    assert.equal(dayLabel('2026-11-25', today), 'mer. 25 nov.')
    assert.equal(addDays('2026-12-31', 1), '2027-01-01')
  })

  it('offers tomorrow, next Monday and a week later to postpone', () => {
    assert.deepEqual(postponeChoices(today).map(c => c.date), ['2026-11-23', '2026-11-23', '2026-11-29'])
    assert.equal(postponeChoices('2026-11-23')[1]!.date, '2026-11-30')
  })
})

describe('todo cards', () => {
  it('formats durations, initials and the second line', () => {
    assert.equal(durationLabel(45), '45 min')
    assert.equal(durationLabel(90), '1 h 30')
    assert.equal(durationLabel(null), '')
    assert.equal(initial('  écrire'), 'É')
    const late = todo('A', { dueOn: '2026-11-20', isLate: true, durationMinutes: 30, checklist: [{ id: '1', label: 'x', done: true }, { id: '2', label: 'y', done: false }] })
    assert.equal(todoMeta(late, today, true), 'En retard · ven. 20 nov. · 30 min · 1/2 sous-tâches')
    assert.equal(todoMeta(late, today, false), '30 min · 1/2 sous-tâches')
    assert.equal(todoMeta(todo('B', { state: 'done', doneOn: today }), today, true), 'Fait aujourd’hui')
    const inProject = todo('C', { durationMinutes: 15, projectId: 'p', projectTitle: 'Chambre de Léa' })
    assert.equal(todoMeta(inProject, today, false), '15 min · Chambre de Léa')
    assert.equal(todoMeta(inProject, today, false, false), '15 min')
    assert.deepEqual(projectProgress(3, 7), { label: '3 / 7 faits', percent: 43 })
    assert.deepEqual(projectProgress(0, 0), { label: '0 / 0 fait', percent: 0 })
  })
})

describe('today page', () => {
  it('splits late, today, the next seven days and the undated todos, high priority first', () => {
    const todos = [
      todo('Plus tard', { dueOn: '2026-11-30' }),
      todo('Retard', { dueOn: '2026-11-20', isLate: true }),
      todo('Basse', { dueOn: today, priority: 'low' }),
      todo('Urgent', { dueOn: today, priority: 'high' }),
      todo('Fait', { state: 'done', doneOn: today, dueOn: '2026-11-19' }),
      todo('Mardi', { dueOn: '2026-11-24', tags: ['Maison'] }),
      todo('Sans date', { tags: ['maison'] }),
    ]
    const routines = [
      routine('Draps', { nextDueOn: '2026-11-21', isLate: true }),
      routine('Poubelles', { nextDueOn: '2026-11-26', doneToday: true, tags: ['maison'] }),
      routine('En pause', { state: 'paused', nextDueOn: null }),
    ]
    const titles = (items: TaskItem[]) => items.map(i => i.kind === 'todo' ? i.todo.title : i.routine.title)
    const { sections, undated } = todaySections(todos, routines, today)
    assert.deepEqual(sections.map(s => [s.title, titles(s.items)]), [
      ['En retard', ['Retard', 'Draps']],
      ['Aujourd’hui', ['Urgent', 'Basse', 'Fait', 'Poubelles']],
      ['mar. 24 nov.', ['Mardi']],
      ['jeu. 26 nov.', ['Poubelles']],
    ])
    assert.deepEqual(titles(undated), ['Sans date'])
    const tagged = todaySections(todos, routines, today, 'MAISON')
    assert.deepEqual(tagged.sections.map(s => s.items.length), [1, 1, 1])
    assert.equal(tagged.undated.length, 1)
    assert.deepEqual(tagsOf(todos), ['Maison'])
  })
})

describe('todo form', () => {
  it('requires a title and a whole duration in minutes', () => {
    assert.equal(todoRequest({ ...draftOf(null), title: '  ' }), 'Le titre est obligatoire.')
    assert.match(String(todoRequest({ ...draftOf(null), title: 'A', durationMinutes: 2.5 })), /Durée/)
    assert.deepEqual(todoRequest({ ...draftOf(null, today), title: ' A ', notes: ' ', durationMinutes: '15' }),
      { title: 'A', notes: null, dueOn: today, durationMinutes: 15, priority: 'normal', tags: [], projectId: null })
  })

  it('adds a tag once and postpones without touching the rest', () => {
    assert.deepEqual(withTag(['Maison'], ' maison '), ['Maison'])
    assert.deepEqual(withTag(['Maison'], 'voiture'), ['Maison', 'voiture'])
    const t = todo('A', { dueOn: '2026-11-20', priority: 'high', tags: ['x'] })
    assert.deepEqual(postponed(t, null), { title: 'A', notes: null, dueOn: null, durationMinutes: null, priority: 'high', tags: ['x'], projectId: null })
  })
})

describe('sub-projects', () => {
  const projects = [
    { id: 'p', title: 'Potager', parentId: 't' },
    { id: 'e', title: 'Elec', parentId: 'c' },
    { id: 't', title: 'Terrain', parentId: null },
    { id: 'c', title: 'Chalet', parentId: 't' },
    { id: 'i', title: 'Impôts', parentId: null },
    { id: 'o', title: 'Orphelin', parentId: 'closed' },
  ]

  it('lists projects in tree order with their path', () => {
    assert.deepEqual(projectTree(projects).map(o => [o.label, o.depth]), [
      ['Impôts', 0], ['Orphelin', 0], ['Terrain', 0], ['Terrain › Chalet', 1], ['Terrain › Chalet › Elec', 2], ['Terrain › Potager', 1],
    ])
  })

  it('finds a project and all its sub-projects', () => {
    assert.deepEqual([...subTreeIds(projects, 't')].sort(), ['c', 'e', 'p', 't'])
    assert.deepEqual([...subTreeIds(projects, 'e')], ['e'])
  })
})
