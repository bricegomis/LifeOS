import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { ReceiptDto, StoreDto } from '../../core/api/api.models.ts'
import { frenchDate, pendingCount, receiptSubtitle, todayInputValue, uploadDay, visibleReceipts } from './receipt-view.ts'

const receipt = (over: Partial<ReceiptDto> = {}): ReceiptDto => ({
  id: 'r1', storeId: null, purchaseDate: null, status: 'pending', createdAt: '2026-11-09T10:00:00Z', ...over,
})
const stores = [{ id: 's1', name: 'Biocoop' }] as StoreDto[]

describe('receipt list presentation', () => {
  it('formats date-only values without shifting them through a time zone', () => {
    assert.equal(frenchDate('2026-11-08'), '08/11/2026')
    assert.equal(frenchDate(null), '')
    assert.equal(frenchDate('08/11/2026'), '')
  })

  it('shows the upload day in local time and nothing for an unreadable timestamp', () => {
    assert.equal(uploadDay(new Date(2026, 10, 9, 23, 30).toISOString()), '09/11/2026')
    assert.equal(uploadDay('pas une date'), '')
  })

  it('never invents a store or a purchase date', () => {
    assert.equal(receiptSubtitle(receipt(), stores), 'Magasin non renseigné')
    assert.equal(receiptSubtitle(receipt({ storeId: 's1' }), stores), 'Biocoop')
    assert.equal(receiptSubtitle(receipt({ storeId: 'gone' }), stores), 'Magasin non renseigné')
    assert.equal(receiptSubtitle(receipt({ storeId: 's1', purchaseDate: '2026-11-08' }), stores), 'Biocoop · achat du 08/11/2026')
  })

  it('filters the inbox on pending receipts and counts them', () => {
    const list = [receipt({ id: 'a' }), receipt({ id: 'b', status: 'processed' }), receipt({ id: 'c' })]
    assert.deepEqual(visibleReceipts(list, 'pending').map(item => item.id), ['a', 'c'])
    assert.equal(visibleReceipts(list, 'all').length, 3)
    assert.equal(pendingCount(list), 2)
  })

  it('builds the max value of the purchase date input from the local day', () => {
    assert.equal(todayInputValue(new Date(2026, 0, 5, 23, 59)), '2026-01-05')
  })
})
