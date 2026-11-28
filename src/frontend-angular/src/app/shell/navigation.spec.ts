import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { currentEntry, fallbackBackPath, isDetailPage, NAVIGATION_BLOCKS, openBlocksAfterNavigation, pageTitle, readMenuOpen, writeMenuOpen } from './navigation.ts'

describe('navigation', () => {
  it('groups every page in one block per domain', () => {
    assert.deepEqual(NAVIGATION_BLOCKS.map(block => block.title), ['Accueil', 'Alimentation', 'Sport', 'Tâches', 'Santé', 'Budget', 'Loisirs'])
    assert.deepEqual(NAVIGATION_BLOCKS[0]!.items.map(item => item.path), ['/today'])
    assert.deepEqual(NAVIGATION_BLOCKS[1]!.items.map(item => item.path), ['/recipes', '/foods', '/products', '/stores', '/tickets'])
    assert.deepEqual(NAVIGATION_BLOCKS[2]!.items.map(item => item.label),
      ['Aujourd’hui', 'Séances', 'Progression', 'VO2max', 'Routines', 'Exercices', 'Défis', 'Objectifs', 'Séances types', 'Garmin'])
    assert.equal(NAVIGATION_BLOCKS[2]!.items[0]!.exact, true)
    assert.deepEqual(NAVIGATION_BLOCKS[3]!.items.map(item => item.path), ['/tasks', '/tasks/all', '/tasks/routines', '/tasks/projects'])
  })

  it('finds the block and page of a URL for the open block and the phone title', () => {
    assert.equal(currentEntry('/sports/routines')?.item?.label, 'Routines')
    assert.equal(currentEntry('/sports')?.item?.label, 'Aujourd’hui')
    assert.equal(currentEntry('/recipes/12?tab=steps')?.block.id, 'food')
    assert.equal(currentEntry('/sports/routine/42')?.item, null)
    assert.equal(currentEntry('/sports/routine/42')?.block.id, 'sport')
    assert.equal(currentEntry('/elsewhere'), null)
    assert.equal(pageTitle('/sports/exercises'), 'Exercices')
    assert.equal(pageTitle('/sports/routine/42'), 'Sport')
    assert.equal(pageTitle('/settings'), 'Paramètres')
    assert.equal(pageTitle('/tasks/all'), 'Toutes les tâches')
    assert.equal(currentEntry('/tasks/todo/7')?.block.id, 'tasks')
    assert.equal(pageTitle('/tasks/todo/7'), 'Tâches')
    assert.equal(currentEntry('/tasks/routine/3')?.block.id, 'tasks')
    assert.equal(pageTitle('/health/supplements'), 'Compléments')
    assert.equal(currentEntry('/health/supplement/4')?.block.id, 'health')
    assert.equal(currentEntry('/budget/line/4')?.block.id, 'budget')
    assert.equal(pageTitle('/budget/rules'), 'Règles')
    assert.equal(currentEntry('/sports/session/9')?.block.id, 'sport')
    assert.equal(pageTitle('/today'), 'Aujourd’hui')
    assert.equal(pageTitle('/reading'), 'Lecture')
    assert.equal(currentEntry('/reading/book/3')?.block.id, 'leisure')
    assert.equal(pageTitle('/reading/series/2'), 'Séries de livres')
  })

  it('names the module homes by their module, not « Aujourd’hui »', () => {
    assert.equal(pageTitle('/sports'), 'Sport')
    assert.equal(pageTitle('/tasks?tag=maison'), 'Tâches')
    assert.equal(pageTitle('/health'), 'Santé')
    assert.equal(pageTitle('/budget'), 'Budget')
  })

  it('offers a way back on detail pages only', () => {
    assert.equal(isDetailPage('/tasks/todo/7'), true)
    assert.equal(isDetailPage('/sports/routine/42/run'), true)
    assert.equal(isDetailPage('/tasks/all'), false)
    assert.equal(isDetailPage('/today'), false)
    assert.equal(isDetailPage('/settings'), false)
    assert.equal(fallbackBackPath('/tasks/todo/7'), '/tasks')
    assert.equal(fallbackBackPath('/reading/book/3'), '/reading')
    assert.equal(fallbackBackPath('/elsewhere'), '/today')
  })

  it('keeps only the domain of the current page open after a navigation', () => {
    assert.deepEqual([...openBlocksAfterNavigation('/sports/routines')], ['sport'])
    assert.deepEqual([...openBlocksAfterNavigation('/recipes/3')], ['food'])
    assert.deepEqual([...openBlocksAfterNavigation('/settings')], [])
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
