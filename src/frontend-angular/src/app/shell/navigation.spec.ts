import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { MOBILE_TABS, NAVIGATION_BLOCKS, readMenuOpen, writeMenuOpen } from './navigation.ts'

describe('navigation', () => {
  it('groups every food page in a single block and keeps reference data off the phone', () => {
    assert.deepEqual(NAVIGATION_BLOCKS.map(block => block.title), ['Alimentation', 'Sport'])
    assert.deepEqual(NAVIGATION_BLOCKS[0]!.items.map(item => item.path), ['/recipes', '/foods', '/products', '/stores', '/tickets'])
    assert.ok(!MOBILE_TABS.some(tab => ['/products', '/stores'].includes(tab.path)))
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
