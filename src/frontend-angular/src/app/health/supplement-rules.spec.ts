import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { doseLabel, periodLabel, statusLabel, supplementDraftOf, supplementRequest, todayGroups, unitPrice } from './supplement-rules.ts'
import type { SupplementDto } from './health.models.ts'

const today = '2026-11-22'

function supplement(more: Partial<SupplementDto> = {}): SupplementDto {
  return {
    id: 's', name: 'Magnésium', notes: null, dose: 2, unit: 'gelule', moment: 'soir', packageContent: 180, packagePrice: 29.9, shop: null, links: [],
    period: { kind: 'year', startOn: '2026-05-01', cureLength: 0, cureUnit: 'month', pauseLength: 0, pauseUnit: 'month' }, isStopped: false, hasImage: false,
    updatedAt: '', monthlyCost: 9.97, status: { active: true, dayInCure: null, cureDays: null, daysLeft: null, nextCureOn: null, ended: false },
    takenToday: false, streak: 0, recentDays: [], ...more,
  }
}

describe('supplement labels', () => {
  it('reads dose, period, status and unit price', () => {
    assert.equal(doseLabel(supplement()), '2 gélules par jour')
    assert.equal(doseLabel(supplement({ dose: 5, unit: 'g' })), '5 g par jour')
    assert.equal(periodLabel(supplement()), 'À l’année depuis le 1 mai')
    const cure = supplement({ period: { kind: 'cure', startOn: today, cureLength: 3, cureUnit: 'month', pauseLength: 2, pauseUnit: 'week' },
      status: { active: true, dayInCure: 12, cureDays: 90, daysLeft: 79, nextCureOn: null, ended: false } })
    assert.equal(periodLabel(cure), 'Cure de 3 mois, pause de 2 semaines, puis on recommence')
    assert.equal(statusLabel(cure), 'Cure : jour 12 sur 90')
    assert.equal(statusLabel({ ...cure, status: { active: false, dayInCure: null, cureDays: 90, daysLeft: null, nextCureOn: '2026-12-05', ended: false } }), 'Pause jusqu’au 5 déc.')
    assert.equal(statusLabel(supplement({ isStopped: true })), 'Arrêté')
    assert.equal(unitPrice(supplement()), '16,61 € les 100 gélules')
  })

  it('groups today’s supplements by moment', () => {
    const groups = todayGroups([supplement({ id: 'a', moment: 'matin' }), supplement({ id: 'b' }), supplement({ id: 'c', status: { ...supplement().status, active: false } })])
    assert.deepEqual(groups.map(g => [g.label, g.items.map(i => i.id)]), [['Matin', ['a']], ['Soir', ['b']]])
  })
})

describe('supplement form', () => {
  it('checks the draft and builds the request', () => {
    const draft = supplementDraftOf(null, today)
    assert.equal(supplementRequest(draft), 'Le nom est obligatoire.')
    assert.match(String(supplementRequest({ ...draft, name: 'A' })), /Contenu/)
    assert.match(String(supplementRequest({ ...draft, name: 'A', packageContent: 60, links: [{ label: '', url: 'http://x.fr' }] })), /https/)
    assert.deepEqual(supplementRequest({ ...draft, name: ' Zinc ', packageContent: '60', packagePrice: '12,5', kind: 'cure', cureLength: '1', pauseLength: '2',
      pauseUnit: 'month', links: [{ label: '', url: ' https://x.fr/zinc ' }, { label: '', url: '' }] }), {
      name: 'Zinc', notes: null, dose: 1, unit: 'gelule', moment: 'matin', packageContent: 60, packagePrice: 12.5, shop: null,
      links: [{ label: null, url: 'https://x.fr/zinc' }],
      period: { kind: 'cure', startOn: today, cureLength: 1, cureUnit: 'month', pauseLength: 2, pauseUnit: 'month' },
    })
  })
})
