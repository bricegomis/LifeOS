import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { formatDate, formatEuro, formatQuantity, matches, packageLabel, today } from './format.ts'

describe('food formats', () => {
  it('writes French amounts, quantities and dates', () => {
    assert.equal(formatEuro(14.875), '14,88 €')
    assert.equal(formatQuantity(6, 'pièce'), '6 pièces')
    assert.equal(formatQuantity(1, 'pièce'), '1 pièce')
    assert.equal(formatQuantity(0.25, 'kg'), '0,25 kg')
    assert.equal(formatDate('2026-11-02'), '02/11/2026')
    assert.equal(formatDate('2026-11-02T10:00:00Z'), '02/11/2026')
  })

  it('names loose packages by the kilo or litre', () => {
    assert.equal(packageLabel(1, 'kg'), 'Au kilo')
    assert.equal(packageLabel(1, 'L'), 'Au litre')
    assert.equal(packageLabel(400, 'g'), '400 g')
  })

  it('searches without case or accents', () => {
    assert.ok(matches('Œuf', 'œu'))
    assert.ok(matches('Lentilles corail', 'LENTILLE'))
    assert.ok(matches('Crème fraîche', 'creme'))
    assert.ok(!matches('Riz', 'pâtes'))
  })

  it('dates today in the local time zone', () => {
    assert.match(today(), /^\d{4}-\d{2}-\d{2}$/)
  })
})
