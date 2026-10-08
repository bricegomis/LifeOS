import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { purchaseDraftHasChanges } from './purchase-draft.ts'

describe('purchase article draft', () => {
  it('treats a changed unit as an unsaved edit only for an unlinked food', () => {
    assert.equal(purchaseDraftHasChanges(null, '', 'unit'), false)
    assert.equal(purchaseDraftHasChanges(null, '', 'kilogram'), true)
    assert.equal(purchaseDraftHasChanges('article-1', 'article-1', 'unit'), false)
  })

  it('detects changes to the selected article', () => {
    assert.equal(purchaseDraftHasChanges('article-1', 'article-2', 'unit'), true)
  })
})
