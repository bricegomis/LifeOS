import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { checkinStatus, dayStrip, entryMeta, filteredDays, level } from './symptom-rules.ts'
import type { SymptomEntryDto, SymptomJournalDto } from './health.models.ts'

const entry = (more: Partial<SymptomEntryDto>): SymptomEntryDto => ({ id: 'e', symptomTypeId: 't', symptomName: 'Fatigue', kind: 'physique', day: '2026-11-22', time: null, intensity: 5, note: null, ...more })
const journal: SymptomJournalDto = {
  today: '2026-11-22', from: '2026-10-24', types: [],
  days: [
    { day: '2026-11-22', calm: false, maxIntensity: 7, entries: [entry({ intensity: 7 }), entry({ kind: 'mental', intensity: 3 })] },
    { day: '2026-11-21', calm: true, maxIntensity: null, entries: [] },
  ],
}

describe('symptom journal', () => {
  it('draws the last days with the strongest intensity or a calm mark', () => {
    const strip = dayStrip(journal, 3)
    assert.deepEqual(strip.map(c => [c.weekday, c.text, c.tone]), [['ven', '', 'none'], ['sam', '✓', 'calm'], ['dim', '7', 'strong']])
    assert.equal(level(4), 'medium')
    assert.equal(checkinStatus(journal), '2 symptômes notés aujourd’hui.')
    assert.equal(checkinStatus({ ...journal, days: [journal.days[1]!] , today: '2026-11-21' }), 'Rien à signaler aujourd’hui.')
  })

  it('filters by kind and reads an entry', () => {
    assert.deepEqual(filteredDays(journal.days, 'mental').map(d => d.entries.length), [1])
    assert.equal(filteredDays(journal.days, '').length, 1)
    assert.equal(entryMeta(entry({ time: '14:10:00', note: 'Après le déjeuner.' })), '14 h 10 · Après le déjeuner.')
  })
})
