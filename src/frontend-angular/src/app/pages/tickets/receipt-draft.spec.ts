import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { packageProposal, receiptDateInput } from './receipt-draft.ts'

describe('receipt drafts', () => {
  it('proposes explicit package measures without inventing weights', () => {
    assert.deepEqual(packageProposal('Emmental 29% 400G'), { quantity: 400, unit: 'g' })
    assert.deepEqual(packageProposal('Sardines huile olive 13'), { quantity: null, unit: null })
    assert.deepEqual(packageProposal('Bouteille 1,5 L'), { quantity: 1.5, unit: 'L' })
  })
  it('shows civil dates as day/month/year', () => {
    assert.equal(receiptDateInput('2026-04-11'), '11/04/2026')
    assert.equal(receiptDateInput(null), '')
  })
})
