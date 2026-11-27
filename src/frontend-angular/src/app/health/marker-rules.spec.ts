import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { dueLabel, filterMarkers, frequencyLabel, isOffTarget, markerDraftOf, markerRequest, measureTypeRequest, rangeBar, rangeLabel } from './marker-rules.ts'
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
    assert.equal(rangeBar(marker({ targetMin: null, targetMax: null })).targetWidth, 0) // no target, no target band
  })

  it('tells values out of the target from values without a target', () => {
    assert.deepEqual((['in_target', 'normal', 'below_lab', 'above_lab', 'in_lab', null] as const).map(isOffTarget), [false, true, true, true, false, false])
  })

  it('checks a body measure kind with an optional target', () => {
    assert.deepEqual(measureTypeRequest(' Taille ', 'cm', '', ' '), { name: 'Taille', unit: 'cm', targetMin: null, targetMax: null, source: null })
    assert.deepEqual(measureTypeRequest('Poids', 'kg', '70', '74,5'), { name: 'Poids', unit: 'kg', targetMin: 70, targetMax: 74.5, source: null })
    assert.equal((measureTypeRequest('FC', 'bpm', '45', '55', 'garmin_resting_heart_rate') as { source: string }).source, 'garmin_resting_heart_rate')
    assert.equal(measureTypeRequest('Poids', 'kg', '75', '74'), 'Cible : le minimum dépasse le maximum.')
    assert.equal(measureTypeRequest('Poids', '', '', ''), 'Nom et unité sont obligatoires.')
  })

  it('checks a marker form', () => {
    const draft = markerDraftOf(null)
    assert.equal(markerRequest({ ...draft, name: 'TSH' }), 'L’unité est obligatoire.')
    assert.equal(markerRequest({ ...draft, name: 'TSH', unit: 'mUI/L', labMin: '4', labMax: '0,4' }), 'Plage du labo : le minimum dépasse le maximum.')
    assert.deepEqual(markerRequest({ ...draft, name: ' TSH ', unit: 'mUI/L', labMin: '0,4', labMax: '4', targetMin: '1', targetMax: '2', whenLow: ' Hyperthyroïdie possible. ', whenHigh: '  ' }),
      { name: 'TSH', unit: 'mUI/L', labMin: 0.4, labMax: 4, targetMin: 1, targetMax: 2, frequencyMonths: 12, panels: [],
        description: null, whenLow: 'Hyperthyroïdie possible.', whenHigh: null })
  })

  it('filters markers by position, follow-up, panel and name', () => {
    const list = [
      marker({ id: 'f', name: 'Ferritine', panels: ['Fer'] }),
      marker({ id: 'v', name: 'Vitamine D', status: 'normal', due: true, late: true }),
      marker({ id: 'c', name: 'CRP', status: 'above_lab' }),
      marker({ id: 't', name: 'TSH', status: 'in_lab', targetMin: null, targetMax: null }),
      marker({ id: 'b', name: 'B12', values: [], status: null, nextDueOn: null, due: true }),
    ]
    const ids = (more: Partial<Parameters<typeof filterMarkers>[1]>) =>
      filterMarkers(list, { query: '', position: 'all', followUp: 'all', panel: '', ...more }).map(m => m.id)
    assert.deepEqual(ids({}), ['b', 'c', 'f', 't', 'v']) // by name
    assert.deepEqual(ids({ position: 'in_target' }), ['f'])
    assert.deepEqual(ids({ position: 'off_target' }), ['c', 'v'])
    assert.deepEqual(ids({ position: 'in_lab_range' }), ['f', 't', 'v'])
    assert.deepEqual(ids({ position: 'off_lab_range' }), ['c'])
    assert.deepEqual(ids({ position: 'no_target' }), ['t'])
    assert.deepEqual(ids({ followUp: 'due' }), ['b', 'v'])
    assert.deepEqual(ids({ followUp: 'late' }), ['v'])
    assert.deepEqual(ids({ followUp: 'never' }), ['b'])
    assert.deepEqual(ids({ followUp: 'up_to_date' }), ['c', 'f', 't'])
    assert.deepEqual(ids({ panel: 'Fer' }), ['f'])
    assert.deepEqual(ids({ query: 'vitamine' }), ['v'])
    assert.deepEqual(ids({ query: 'fer' }), ['f']) // panel name too
  })
})
