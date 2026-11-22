import type { AppointmentDto, AppointmentRequest, HealthDocumentFileDto, HealthDocumentType } from './health.models'
import { dateLabel } from './marker-rules.ts'

export const DOCUMENT_TYPES: { code: HealthDocumentType, label: string }[] = [
  { code: 'ordonnance', label: 'Ordonnance' }, { code: 'compte_rendu', label: 'Compte rendu' }, { code: 'resultat', label: 'Résultat d’analyse' },
  { code: 'bilan', label: 'Bilan' }, { code: 'autre', label: 'Autre' },
]

export function documentTypeLabel(code: HealthDocumentType): string { return DOCUMENT_TYPES.find(t => t.code === code)?.label ?? code }

export function sizeLabel(bytes: number): string {
  if (bytes < 1024) return `${bytes} o`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} Ko`
  return `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} Mo`
}

export function fileLabel(f: HealthDocumentFileDto, index: number): string {
  return `${f.originalName ?? (f.contentType === 'application/pdf' ? `Document ${index + 1}.pdf` : `Photo ${index + 1}`)} · ${sizeLabel(f.size)}`
}

/** "Lun. 24 nov. 2026 · 9 h 30", without the time when unknown. */
export function appointmentWhen(a: Pick<AppointmentDto, 'day' | 'time'>): string {
  const days = ['Dim.', 'Lun.', 'Mar.', 'Mer.', 'Jeu.', 'Ven.', 'Sam.']
  const weekday = days[new Date(`${a.day}T00:00:00Z`).getUTCDay()]
  return `${weekday} ${dateLabel(a.day)}${a.time ? ` · ${timeLabel(a.time)}` : ''}`
}

export function timeLabel(time: string): string {
  const [h, m] = time.split(':')
  return `${Number(h)} h ${m === '00' ? '' : m}`.trim()
}

/** Days from `today` to `day`: "Aujourd’hui", "Demain", "Dans 5 jours". */
export function inDays(day: string, today: string): string {
  const diff = Math.round((Date.parse(`${day}T00:00:00Z`) - Date.parse(`${today}T00:00:00Z`)) / 86_400_000)
  if (diff === 0) return 'Aujourd’hui'
  if (diff === 1) return 'Demain'
  return diff > 1 ? `Dans ${diff} jours` : `Il y a ${-diff} jours`
}

export interface AppointmentDraft { day: string, time: string, who: string, reason: string, place: string, notes: string, bookingUrl: string, reportDocumentId: string }

export function appointmentDraftOf(a: AppointmentDto | null, today: string): AppointmentDraft {
  return {
    day: a?.day ?? today, time: a?.time?.slice(0, 5) ?? '', who: a?.who ?? '', reason: a?.reason ?? '', place: a?.place ?? '', notes: a?.notes ?? '',
    bookingUrl: a?.bookingUrl ?? '', reportDocumentId: a?.reportDocumentId ?? '',
  }
}

/** The request, or the message to show. */
export function appointmentRequest(d: AppointmentDraft): AppointmentRequest | string {
  if (!d.who.trim()) return 'Le médecin ou la spécialité est obligatoire.'
  if (!d.day) return 'Le jour est obligatoire.'
  const url = d.bookingUrl.trim()
  if (url && !/^https:\/\/\S+$/i.test(url)) return 'Le lien de prise de rendez-vous commence par https://.'
  const text = (value: string) => value.trim() || null
  return {
    day: d.day, time: d.time ? `${d.time}:00` : null, who: d.who.trim(), reason: text(d.reason), place: text(d.place), notes: text(d.notes),
    bookingUrl: url || null, reportDocumentId: d.reportDocumentId || null,
  }
}
