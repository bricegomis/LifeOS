import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { dismissDecision, formSnapshot, hasChanges } from './dialog-guard.ts'

describe('creation dialog dismissal', () => {
  it('never closes while a request is pending', () => {
    assert.equal(dismissDecision({ busy: true, dirty: false }), 'ignore')
    assert.equal(dismissDecision({ busy: true, dirty: true }), 'ignore')
  })

  it('closes directly when nothing was typed', () => {
    assert.equal(dismissDecision({ busy: false, dirty: false }), 'close')
  })

  it('asks for confirmation before discarding a typed draft', () => {
    assert.equal(dismissDecision({ busy: false, dirty: true }), 'confirm')
  })
})

describe('creation dialog draft snapshots', () => {
  it('ignores key order', () => {
    assert.equal(formSnapshot({ name: 'Riz', unit: 'g' }), formSnapshot({ unit: 'g', name: 'Riz' }))
  })

  it('detects edits in nested lines and restores clean state', () => {
    const draft = { name: 'Bol', lines: [{ foodItemId: 'a', quantity: 1 }] }
    const initial = formSnapshot(draft)
    assert.equal(hasChanges(initial, draft), false)
    draft.lines[0]!.quantity = 2
    assert.equal(hasChanges(initial, draft), true)
    draft.lines[0]!.quantity = 1
    assert.equal(hasChanges(initial, draft), false)
    draft.lines.push({ foodItemId: '', quantity: 1 })
    assert.equal(hasChanges(initial, draft), true)
  })

  it('treats null and empty values as distinct edits', () => {
    assert.equal(hasChanges(formSnapshot({ calories: null }), { calories: 0 }), true)
  })
})
