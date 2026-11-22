import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { appointmentDraftOf, appointmentRequest, appointmentWhen, documentTypeLabel, fileLabel, inDays, sizeLabel } from './document-rules.ts'

describe('documents and appointments', () => {
  it('labels documents and their files', () => {
    assert.equal(documentTypeLabel('compte_rendu'), 'Compte rendu')
    assert.equal(sizeLabel(2_400_000), '2,3 Mo')
    assert.equal(fileLabel({ id: 'f', contentType: 'application/pdf', size: 800, originalName: null }, 1), 'Document 2.pdf · 800 o')
  })

  it('labels appointment days', () => {
    assert.equal(appointmentWhen({ day: '2026-11-24', time: '09:30:00' }), 'Mar. 24 nov. 2026 · 9 h 30')
    assert.equal(appointmentWhen({ day: '2026-11-24', time: '14:00:00' }), 'Mar. 24 nov. 2026 · 14 h')
    assert.equal(inDays('2026-11-23', '2026-11-22'), 'Demain')
    assert.equal(inDays('2026-11-30', '2026-11-22'), 'Dans 8 jours')
  })

  it('checks an appointment before sending it', () => {
    const draft = { ...appointmentDraftOf(null, '2026-11-22'), who: ' Dentiste ', time: '08:15', bookingUrl: 'https://www.doctolib.fr/x' }
    assert.deepEqual(appointmentRequest(draft), {
      day: '2026-11-22', time: '08:15:00', who: 'Dentiste', reason: null, place: null, notes: null, bookingUrl: 'https://www.doctolib.fr/x', reportDocumentId: null,
    })
    assert.equal(appointmentRequest({ ...draft, bookingUrl: 'http://x' }), 'Le lien de prise de rendez-vous commence par https://.')
    assert.equal(appointmentRequest({ ...draft, who: '' }), 'Le médecin ou la spécialité est obligatoire.')
  })
})
