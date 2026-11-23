import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { lineGeometry } from './chart-geometry.ts'

describe('line chart geometry', () => {
  it('draws the values in the box and splits the line at gaps', () => {
    const chart = lineGeometry([{ label: 'a', value: 46.5 }, { label: 'b', value: 49.2 }, { label: 'c', value: null }, { label: 'd', value: 48 }])
    assert.equal(chart.segments.length, 2)
    assert.equal(chart.min, 46.5)
    assert.equal(chart.max, 49.2)
    assert.equal(chart.last, 48)
    assert.deepEqual(chart.ticks, ['a', 'b', 'c', 'd'])
    assert.ok(chart.segments[0]!.startsWith('0,'))
  })

  it('keeps a band inside the box and handles empty data', () => {
    const chart = lineGeometry([{ label: 'a', value: 43 }, { label: 'b', value: 40 }], [37, 47])
    assert.ok(chart.bandY > 0 && chart.bandHeight > 0 && chart.bandY + chart.bandHeight < 140)
    assert.deepEqual(lineGeometry([{ label: 'a', value: null }]).segments, [])
  })
})
