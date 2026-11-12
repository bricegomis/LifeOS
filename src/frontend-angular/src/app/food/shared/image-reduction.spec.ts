import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { jpegName, LIBRARY_IMAGE, RECEIPT_PHOTO, reducedSize } from './image-reduction.ts'

describe('image reduction', () => {
  it('brings the longest side down to the limit and keeps the proportions', () => {
    // A 12 Mpx iPhone photo, landscape and portrait.
    assert.deepEqual(reducedSize(4032, 3024, RECEIPT_PHOTO.maxSide), { width: 2560, height: 1920 })
    assert.deepEqual(reducedSize(3024, 4032, LIBRARY_IMAGE.maxSide), { width: 1200, height: 1600 })
    // A long, narrow receipt keeps its width proportional.
    assert.deepEqual(reducedSize(1000, 5120, RECEIPT_PHOTO.maxSide), { width: 500, height: 2560 })
  })

  it('leaves images already within the limit untouched', () => {
    assert.equal(reducedSize(2560, 1440, RECEIPT_PHOTO.maxSide), null)
    assert.equal(reducedSize(800, 600, LIBRARY_IMAGE.maxSide), null)
    assert.equal(reducedSize(0, 0, LIBRARY_IMAGE.maxSide), null)
  })

  it('names the reduced file as a JPEG', () => {
    assert.equal(jpegName('IMG_0042.HEIC'), 'IMG_0042.jpg')
    assert.equal(jpegName('ticket.png'), 'ticket.jpg')
    assert.equal(jpegName('photo'), 'photo.jpg')
    assert.equal(jpegName('.png'), 'photo.jpg')
  })
})
