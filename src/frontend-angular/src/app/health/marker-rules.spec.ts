import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { dueLabel, frequencyLabel, markerDraftOf, markerRequest, rangeBar, rangeLabel } from './marker-rules.ts'
import type { MarkerDto } from './health.models.ts'

function marker(more: Partial<MarkerDto> = {}): MarkerDto {
  return { id: 'm', name: 'Ferritine', unit: 'µg/L', labMin: 30, labMax: 400, targetMin: 80, targetMax: 150, frequencyMonths: 6, panels: [], isArchived: false,
    values: [{ analysisId: 'a', day: '2026-10-13', value: 88, status: 'in_target' }], status: 'in_target', trend: null, nextDueOn: '2027-04-13', due: false, late: false, ...more }
}

describe('markers', () => {
  it('reads ranges, frequencies and due dates', () => {
    assert.equal(rangeLabel(0.4, 4, 'mUI/L'), '0,4 – 4 mUI/L')
    assert.equal(rangeLabel(null, 5, 'mg/L'), '< 5 mg/L')
    assert.equal(frequencyLabel(0), 'Une seule fois')
    assert.equal(frequencyLabel(24), 'Tous les 2 ans')
    assert.equal(dueLabel(marker()), 'À refaire vers le 13 avr. 2027')
    assert.equal(dueLabel(marker({ values: [] })), 'Jamais mesuré')
  })

  it('places the lab range, the target and the last value on one scale', () => {
    const bar = rangeBar(marker())
    assert.ok(bar.labLeft < bar.targetLeft && bar.targetLeft < bar.value! && bar.value! < bar.targetLeft + bar.targetWidth)
  })

  it('checks a marker form', () => {
    const draft = markerDraftOf(null)
    assert.equal(markerRequest({ ...draft, name: 'TSH' }), 'L’unité est obligatoire.')
    assert.equal(markerRequest({ ...draft, name: 'TSH', unit: 'mUI/L', labMin: '4', labMax: '0,4' }), 'Plage du labo : le minimum dépasse le maximum.')
    assert.deepEqual(markerRequest({ ...draft, name: ' TSH ', unit: 'mUI/L', labMin: '0,4', labMax: '4', targetMin: '1', targetMax: '2', whenLow: ' Hyperthyroïdie possible. ', whenHigh: '  ' }),
      { name: 'TSH', unit: 'mUI/L', labMin: 0.4, labMax: 4, targetMin: 1, targetMax: 2, frequencyMonths: 12, panels: [],
        description: null, whenLow: 'Hyperthyroïdie possible.', whenHigh: null })
  })
})
