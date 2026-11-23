import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { addMonths, cleanLabel, euro, lineDraftOf, lineRequest, monthLabel, monthlyOf, needsJudgement, parseAmount, ruleTextOf } from './budget-rules.ts'
import type { BudgetLineDto } from './budget.models.ts'

describe('budget rules', () => {
  it('formats euros and months in French', () => {
    assert.equal(euro(800), '800 €')
    assert.equal(euro(-37, true), '−37 €')
    assert.equal(euro(2929.36, true).replace(/\s/g, ' '), '+2 929,36 €')
    assert.equal(addMonths('2026-11', 2), '2027-01')
    assert.equal(addMonths('2026-01', -1), '2025-12')
    assert.equal(monthLabel('2026-11'), 'Novembre 2026')
  })

  it('cleans bank labels and proposes a rule text', () => {
    assert.equal(cleanLabel('CARTE 29/10/26 LE PASSIONNE CB*5135'), 'LE PASSIONNE')
    assert.equal(cleanLabel('PRLV SEPA CROSSFIT CFLS'), 'CROSSFIT CFLS')
    assert.equal(ruleTextOf('VIR INST DUPONT A.'), 'DUPONT A.')
    assert.ok(needsJudgement('PRLV SEPA PayPal Europe S.a.r.l. et Cie'))
    assert.ok(!needsJudgement('CARTE 23/10/26 LECLERC REZE CB*1'))
  })

  it('parses amounts typed by hand', () => {
    assert.equal(parseAmount('1 234,50'), 1234.5)
    assert.equal(parseAmount('-18.5'), -18.5)
    assert.equal(parseAmount(''), null)
    assert.ok(Number.isNaN(parseAmount('abc') as number))
  })

  it('builds a line request and only sends a changed amount', () => {
    const draft = lineDraftOf(null, '2026-11')
    assert.equal(lineRequest(draft, null), 'Indique un nom.')
    Object.assign(draft, { name: 'Courses', category: 'Alimentation', amount: '800' })
    const created = lineRequest(draft, null)
    assert.ok(typeof created !== 'string')
    assert.deepEqual([created.amount, created.period, created.fromMonth], [800, 'month', null])
    Object.assign(draft, { amount: '300', period: 'year' })
    assert.equal(monthlyOf(draft), 25)

    const line: BudgetLineDto = { id: '1', name: 'Courses', category: 'Alimentation', kind: 'envelope', reduce: true, fromSupplements: false, notes: null,
      stoppedFrom: null, active: true, amount: { fromMonth: '2000-01', amount: 800, period: 'month', months: 1 }, planned: 800, history: [] }
    const same = lineRequest(lineDraftOf(line, '2026-11'), line)
    assert.ok(typeof same !== 'string')
    assert.equal(same.amount, null)
    const changed = lineRequest({ ...lineDraftOf(line, '2026-11'), amount: '600' }, line)
    assert.ok(typeof changed !== 'string')
    assert.deepEqual([changed.amount, changed.fromMonth], [600, '2026-11'])
    assert.equal(lineRequest({ ...lineDraftOf(null, '2026-11'), name: 'Salaire', kind: 'income' }, null) !== 'Indique le montant prévu.', true)
  })
})
