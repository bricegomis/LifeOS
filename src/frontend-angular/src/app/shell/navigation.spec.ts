import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { currentEntry, NAVIGATION_BLOCKS, pageTitle, readMenuOpen, writeMenuOpen } from './navigation.ts'

describe('navigation', () => {
  it('groups every page in one block per domain', () => {
    assert.deepEqual(NAVIGATION_BLOCKS.map(block => block.title), ['Alimentation', 'Sport', 'Tâches'])
    assert.deepEqual(NAVIGATION_BLOCKS[0]!.items.map(item => item.path), ['/recipes', '/foods', '/products', '/stores', '/tickets'])
    assert.deepEqual(NAVIGATION_BLOCKS[1]!.items.map(item => item.label),
      ['Aujourd’hui', 'Historique', 'Progression', 'Routines', 'Exercices', 'Défis', 'Objectifs', 'Séances types'])
    assert.equal(NAVIGATION_BLOCKS[1]!.items[0]!.exact, true)
    assert.deepEqual(NAVIGATION_BLOCKS[2]!.items.map(item => item.path), ['/tasks', '/tasks/all', '/tasks/routines', '/tasks/projects'])
  })

  it('finds the block and page of a URL for the open block and the phone title', () => {
    assert.equal(currentEntry('/sports/routines')?.item?.label, 'Routines')
    assert.equal(currentEntry('/sports')?.item?.label, 'Aujourd’hui')
    assert.equal(currentEntry('/recipes/12?tab=steps')?.block.id, 'food')
    assert.equal(currentEntry('/sports/routine/42')?.item, null)
    assert.equal(currentEntry('/sports/routine/42')?.block.id, 'sport')
    assert.equal(currentEntry('/elsewhere'), null)
    assert.equal(pageTitle('/sports/exercises'), 'Sport · Exercices')
    assert.equal(pageTitle('/sports/routine/42'), 'Sport')
    assert.equal(pageTitle('/settings'), 'Paramètres')
    assert.equal(pageTitle('/tasks/all'), 'Tâches · Toutes les tâches')
    assert.equal(currentEntry('/tasks/todo/7')?.block.id, 'tasks')
    assert.equal(pageTitle('/tasks/todo/7'), 'Tâches')
    assert.equal(currentEntry('/tasks/routine/3')?.block.id, 'tasks')
  })

  it('remembers the menu state and survives an unavailable storage', () => {
    const values = new Map<string, string>()
    const storage = { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => { values.set(key, value) } }
    assert.equal(readMenuOpen(storage), true)
    writeMenuOpen(false, storage)
    assert.equal(readMenuOpen(storage), false)
    const broken = { getItem: () => { throw new Error('blocked') }, setItem: () => { throw new Error('blocked') } }
    assert.equal(readMenuOpen(broken), true)
    assert.doesNotThrow(() => writeMenuOpen(false, broken))
  })
})
