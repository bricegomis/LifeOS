import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  addDays, dayLabel, draftOf, durationLabel, initial, postponeChoices, postponed, tagsOf, todaySections, todoMeta, todoRequest, withTag,
} from './task-format.ts'
import type { TodoDto } from './tasks.models.ts'

const today = '2026-11-22' // a Sunday

function todo(title: string, more: Partial<TodoDto> = {}): TodoDto {
  return {
    id: title, memberProfileId: 'p', title, notes: null, dueOn: null, durationMinutes: null, priority: 'normal', tags: [],
    state: 'open', doneOn: null, isLate: false, hasImage: false, checklist: [], updatedAt: '', ...more,
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
    const { sections, undated } = todaySections(todos, today)
    assert.deepEqual(sections.map(s => [s.title, s.todos.map(t => t.title)]), [
      ['En retard', ['Retard']],
      ['Aujourd’hui', ['Urgent', 'Basse', 'Fait']],
      ['mar. 24 nov.', ['Mardi']],
    ])
    assert.deepEqual(undated.map(t => t.title), ['Sans date'])
    const tagged = todaySections(todos, today, 'MAISON')
    assert.deepEqual(tagged.sections.map(s => s.todos.length), [0, 1])
    assert.equal(tagged.undated.length, 1)
    assert.deepEqual(tagsOf(todos), ['Maison'])
  })
})

describe('todo form', () => {
  it('requires a title and a whole duration in minutes', () => {
    assert.equal(todoRequest({ ...draftOf(null), title: '  ' }), 'Le titre est obligatoire.')
    assert.match(String(todoRequest({ ...draftOf(null), title: 'A', durationMinutes: 2.5 })), /Durée/)
    assert.deepEqual(todoRequest({ ...draftOf(null, today), title: ' A ', notes: ' ', durationMinutes: '15' }),
      { title: 'A', notes: null, dueOn: today, durationMinutes: 15, priority: 'normal', tags: [] })
  })

  it('adds a tag once and postpones without touching the rest', () => {
    assert.deepEqual(withTag(['Maison'], ' maison '), ['Maison'])
    assert.deepEqual(withTag(['Maison'], 'voiture'), ['Maison', 'voiture'])
    const t = todo('A', { dueOn: '2026-11-20', priority: 'high', tags: ['x'] })
    assert.deepEqual(postponed(t, null), { title: 'A', notes: null, dueOn: null, durationMinutes: null, priority: 'high', tags: ['x'] })
  })
})
