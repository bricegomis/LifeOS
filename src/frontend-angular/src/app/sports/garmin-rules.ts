import type { GarminState, GarminStatusDto } from './sports.models.ts'

export interface GarminNotice { tone: 'ok' | 'warn' | 'off', icon: string, text: string, action: 'code' | 'reconnect' | 'settings' | null }

/** What the screens say about the Garmin connection (ADR 0019); null when there is nothing to say. */
export function garminNotice(status: Pick<GarminStatusDto, 'state'> | null): GarminNotice | null {
  const state: GarminState = status?.state ?? 'never'
  switch (state) {
    case 'mfa_requested': return { tone: 'warn', icon: 'mark_email_unread', text: 'Garmin a envoyé un code. Saisis-le pour reprendre la synchro.', action: 'code' }
    case 'code_refused': return { tone: 'off', icon: 'error', text: 'Le code a été refusé par Garmin. Saisis-en un autre.', action: 'code' }
    case 'code_expired': return { tone: 'warn', icon: 'timer_off', text: 'Le code n’a pas été saisi à temps. Relance la connexion pour en recevoir un nouveau.', action: 'reconnect' }
    case 'credentials_refused': return { tone: 'off', icon: 'key_off', text: 'Garmin refuse l’email ou le mot de passe de la pile LifeOS (GARMIN_EMAIL, GARMIN_PASSWORD).', action: 'settings' }
    case 'error': return { tone: 'off', icon: 'sync_problem', text: 'La synchro Garmin est en panne. Tes données restent dans Garmin et seront rattrapées.', action: 'settings' }
    default: return null
  }
}

export function waitsForCode(status: Pick<GarminStatusDto, 'state'> | null): boolean {
  return status?.state === 'mfa_requested' || status?.state === 'code_refused'
}

/** A Garmin code is six digits; spaces typed while copying it are ignored. Null when invalid. */
export function cleanCode(code: string): string | null {
  const digits = code.replace(/\s/g, '')
  return /^\d{6}$/.test(digits) ? digits : null
}

/** "aujourd’hui à 13 h 02", "hier à 7 h 00" or "le 21 nov. à 19 h 00", in Paris time. */
export function whenText(instant: string | null, now = new Date()): string {
  if (!instant) return 'jamais'
  const day = (date: Date) => new Intl.DateTimeFormat('fr-CA', { timeZone: 'Europe/Paris' }).format(date)
  const date = new Date(instant)
  const time = new Intl.DateTimeFormat('fr-FR', { timeZone: 'Europe/Paris', hour: 'numeric', minute: '2-digit' }).format(date).replace(':', ' h ')
  const yesterday = new Date(now.getTime() - 86_400_000)
  if (day(date) === day(now)) return `aujourd’hui à ${time}`
  if (day(date) === day(yesterday)) return `hier à ${time}`
  const label = new Intl.DateTimeFormat('fr-FR', { timeZone: 'Europe/Paris', day: 'numeric', month: 'short' }).format(date)
  return `le ${label} à ${time}`
}
