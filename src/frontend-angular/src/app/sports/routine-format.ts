import type {
  ExerciseCompletionDto, ExerciseDto, ExerciseMeasure, ExerciseProgressDto, ExerciseRequest, RoutineDayDto, RoutineDto,
  RoutineLineDto, RoutineReportDto, RoutineRequest, RoutineSessionDto,
} from './sports.models.ts'

const SHORT_DAYS = ['lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.', 'dim.']
const LONG_DAYS = ['lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi', 'dimanche']
export const DAY_LETTERS = ['L', 'M', 'M', 'J', 'V', 'S', 'D']
export const DAY_NAMES = ['Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi', 'Dimanche']

/** A load in kg with a French comma: 12,5. */
export function kilos(value: number): string { return String(value).replace('.', ',') }
export function measureUnit(measure: ExerciseMeasure): string { return measure === 'hold' ? 's' : 'rép.' }
export function measureLabel(measure: ExerciseMeasure): string { return measure === 'hold' ? 'Tenue en secondes' : 'Répétitions' }
/** A load is optional (ADR 0015): none or 0 means bodyweight. */
export function hasLoad(loadKg: number | null | undefined): loadKg is number { return loadKg !== null && loadKg !== undefined && loadKg > 0 }
export function exerciseSummary(exercise: ExerciseDto): string {
  return measureLabel(exercise.measure) + (hasLoad(exercise.defaultLoadKg) ? ` · ${kilos(exercise.defaultLoadKg)} kg` : ' · poids du corps')
}

/** "2 × 45 s · 10 kg": the target of a routine line. */
export function targetText(line: RoutineLineDto): string {
  const sets = line.sets > 1 ? `${line.sets} × ` : ''
  const load = hasLoad(line.loadKg) ? ` · ${kilos(line.loadKg)} kg` : ''
  return `${sets}${line.target} ${measureUnit(line.measure)}${load}`
}
/** "3 × 60 s à 12 kg": what was done in a pass. */
export function doneText(line: RoutineLineDto, completion: ExerciseCompletionDto): string {
  const sets = completion.setsDone > 1 ? `${completion.setsDone} × ` : ''
  const load = hasLoad(completion.loadKg) ? ` à ${kilos(completion.loadKg)} kg` : ''
  return `${sets}${completion.value} ${measureUnit(line.measure)}${load}`
}

export function daysLabel(days: number[]): string {
  if (days.length === 7) return 'Tous les jours'
  return [...days].sort((a, b) => a - b).map(day => SHORT_DAYS[day - 1]).join(', ')
}
/** "Prochaine : demain" or "Prochaine : jeudi" for a routine that is not planned today. */
export function nextDayLabel(today: string, next: string | null): string {
  if (!next) return ''
  const gap = Math.round((Date.parse(`${next}T12:00:00Z`) - Date.parse(`${today}T12:00:00Z`)) / 86_400_000)
  if (gap === 1) return 'Prochaine : demain'
  return `Prochaine : ${LONG_DAYS[(new Date(`${next}T12:00:00Z`).getUTCDay() + 6) % 7]}`
}

export function routineSummary(routine: RoutineDto): string {
  const count = routine.lines.length
  return `${daysLabel(routine.days)} · ${count} exercice${count > 1 ? 's' : ''} · ${routine.jokersPerWeek} joker${routine.jokersPerWeek > 1 ? 's' : ''}`
}
export function routineStreakLine(report: RoutineReportDto): string {
  return `Série : ${report.streak} jour${report.streak > 1 ? 's' : ''} · record ${report.record}`
}
/** The unfinished pass of today, if any: one at a time per routine. */
export function openPass(report: RoutineReportDto): RoutineSessionDto | null {
  return report.todaySessions.find(session => !session.finished) ?? null
}
/** "9 h 12": a time of the day, in Paris. */
export function clockTime(iso: string): string {
  const parts = new Intl.DateTimeFormat('fr-FR', { hour: 'numeric', minute: '2-digit', timeZone: 'Europe/Paris' }).formatToParts(new Date(iso))
  const part = (type: string) => parts.find(item => item.type === type)?.value ?? ''
  return `${Number(part('hour'))} h ${part('minute')}`
}
/** "2 passages : 9 h 12, 14 h 40" for the passes finished today. */
export function passesLine(report: RoutineReportDto): string {
  const finished = report.todaySessions.filter(session => session.finished)
  if (!finished.length) return 'Aucun passage terminé aujourd’hui'
  return `${finished.length} passage${finished.length > 1 ? 's' : ''} : ${finished.map(session => clockTime(session.startedAt)).join(', ')}`
}
export function routineChip(report: RoutineReportDto): string {
  return report.completedToday ? 'Faite' : openPass(report) ? 'En cours' : 'À faire'
}
/** Share of the open pass done; 100 once the day is done without an open pass. */
export function routinePercent(report: RoutineReportDto): number {
  const pass = openPass(report)
  if (pass) return pass.exercisesTotal ? Math.round(pass.exercisesDone * 100 / pass.exercisesTotal) : 0
  return report.completedToday ? 100 : 0
}
export function routineAction(report: RoutineReportDto): string {
  return openPass(report) ? 'Continuer le passage' : report.completedToday ? 'Nouveau passage' : 'Commencer'
}
export function routineJokerLine(report: RoutineReportDto): string {
  if (!report.jokersPerWeek) return 'Sans joker'
  const left = report.jokersLeftThisWeek
  return `Jokers cette semaine : ${left} sur ${report.jokersPerWeek} restant${left > 1 ? 's' : ''}`
}
/** Message shown when a tick finishes a pass; empty otherwise. */
export function routineCelebration(before: RoutineReportDto, after: RoutineReportDto): string {
  if (after.passesToday <= before.passesToday) return ''
  if (after.passesToday === 1) return `${after.routine.name} : routine faite. Série de ${after.streak} jour${after.streak > 1 ? 's' : ''}.`
  return `${after.routine.name} : passage terminé, ${after.passesToday} aujourd’hui.`
}

/** Sets ticked for an exercise in a pass. */
export function setsDone(session: RoutineSessionDto | null, exerciseId: string): number {
  return session?.completions.find(completion => completion.exerciseId === exerciseId)?.setsDone ?? 0
}
export interface SetButton { label: string, counter: string, percent: number, full: boolean, aria: string }
/** The "Série faite" button of an exercise: its counter fills the button until every set is done. */
export function setButton(line: RoutineLineDto, done: number): SetButton {
  const full = done >= line.sets
  return {
    label: full ? 'Exercice fait' : 'Série faite', counter: `${Math.min(done, line.sets)} / ${line.sets}`,
    percent: Math.round(Math.min(done, line.sets) * 100 / line.sets), full,
    aria: full ? `${line.exerciseName} : toutes les séries sont faites` : `Série faite pour ${line.exerciseName}, ${done} sur ${line.sets} pour l’instant`,
  }
}


/** Calendar cell level of a routine day, from the share of exercises ticked. */
export function routineDayLevel(day: RoutineDayDto): string {
  if (day.status === 'not_counted') return 'none'
  if (day.status === 'not_planned') return 'off'
  if (day.status === 'joker') return 'joker'
  if (day.percent >= 100) return 'full'
  if (day.percent >= 50) return 'high'
  if (day.percent > 0) return 'low'
  return 'zero'
}
const ROUTINE_STATUS: Record<string, string> = {
  done: 'faite', joker: 'joker', missed: 'manquée', in_progress: 'en cours', not_planned: 'non prévue', not_counted: 'hors routine',
}
export function routineDayTitle(day: RoutineDayDto, dayLabel: string): string {
  const count = day.planned ? ` : ${day.done} / ${day.planned}` : ''
  const passes = day.passes > 1 ? `, ${day.passes} passages` : ''
  return `${dayLabel}${count} (${ROUTINE_STATUS[day.status]}${passes})`
}

export interface ProgressBar { label: string, title: string, height: number, current: boolean }
export interface ProgressChart { title: string, now: string, summary: string, bars: ProgressBar[] }

/** One bar per week for the value, and for the load when the exercise has one. A week without the exercise has no bar. */
export function exerciseCharts(exercise: ExerciseDto, progress: ExerciseProgressDto, weekLabel: (monday: string) => string): ProgressChart[] {
  const unit = measureUnit(exercise.measure)
  const chart = (title: string, values: (number | null)[], format: (value: number) => string): ProgressChart => {
    const max = Math.max(1, ...values.map(value => value ?? 0))
    const last = values.at(-1) ?? null
    return {
      title, now: last === null ? 'Pas cette semaine' : `${format(last)} cette semaine`,
      summary: `${title} par semaine : ` + values.map((value, index) => `${weekLabel(progress.weeks[index]!.monday)} ${value === null ? 'rien' : format(value)}`).join(', '),
      bars: values.map((value, index) => ({
        label: value !== null && value !== values[index - 1] ? format(value).split(' ')[0]! : '',
        title: `Semaine du ${weekLabel(progress.weeks[index]!.monday)} : ${value === null ? 'pas fait' : format(value)}`,
        height: value === null ? 0 : Math.max(4, Math.round(value * 72 / max)),
        current: index === values.length - 1,
      })),
    }
  }
  const charts = [chart(exercise.measure === 'hold' ? 'Tenue' : 'Répétitions', progress.weeks.map(week => week.bestValue), value => `${value} ${unit}`)]
  if (hasLoad(exercise.defaultLoadKg) || progress.bestLoadKg !== null) {
    charts.push(chart('Charge', progress.weeks.map(week => week.bestLoadKg), value => `${kilos(value)} kg`))
  }
  return charts
}

export interface RoutineLineDraft { exerciseId: string, sets: number | null, target: number | null, loadKg: number | null }
export interface RoutineDraft { name: string, days: boolean[], jokersPerWeek: number, lines: RoutineLineDraft[] }

export function emptyRoutineDraft(): RoutineDraft { return { name: '', days: Array<boolean>(7).fill(true), jokersPerWeek: 1, lines: [] } }
export function draftFromRoutine(routine: RoutineDto): RoutineDraft {
  return {
    name: routine.name, days: DAY_LETTERS.map((_, index) => routine.days.includes(index + 1)), jokersPerWeek: routine.jokersPerWeek,
    lines: routine.lines.map(({ exerciseId, sets, target, loadKg }) => ({ exerciseId, sets, target, loadKg })),
  }
}
export function newLine(exercise: ExerciseDto): RoutineLineDraft {
  return { exerciseId: exercise.id, sets: 1, target: exercise.measure === 'hold' ? 30 : 10, loadKg: hasLoad(exercise.defaultLoadKg) ? exercise.defaultLoadKg : null }
}
export function moveLine(lines: RoutineLineDraft[], index: number, direction: 1 | -1): RoutineLineDraft[] {
  const target = index + direction
  if (target < 0 || target >= lines.length) return lines
  const moved = [...lines]
  ;[moved[index], moved[target]] = [moved[target]!, moved[index]!]
  return moved
}

/** The request to send, or the French message of the first problem. */
export function routineRequest(draft: RoutineDraft, exercises: ExerciseDto[]): RoutineRequest | string {
  const name = draft.name.trim()
  if (!name) return 'Donnez un nom à la routine.'
  const days = draft.days.flatMap((on, index) => on ? [index + 1] : [])
  if (!days.length) return 'Choisissez au moins un jour.'
  if (!draft.lines.length) return 'Ajoutez au moins un exercice.'
  const lines: RoutineRequest['lines'] = []
  for (const line of draft.lines) {
    const exercise = exercises.find(item => item.id === line.exerciseId)
    if (!exercise) return 'Un exercice de la routine est introuvable.'
    const sets = Number(line.sets), target = Number(line.target)
    if (!Number.isInteger(sets) || sets < 1 || sets > 20) return `${exercise.name} : 1 à 20 séries.`
    if (!Number.isInteger(target) || target < 1 || target > 10_000) return `${exercise.name} : la cible va de 1 à 10 000.`
    const load = optionalLoad(line.loadKg)
    if (load === undefined) return `${exercise.name} : charge de 0 à 500 kg, par pas de 0,5.`
    // 0 is sent as such: bodyweight, rather than the exercise's usual load.
    lines.push({ exerciseId: exercise.id, sets, target, loadKg: load ?? 0 })
  }
  return { name, days, jokersPerWeek: draft.jokersPerWeek, lines }
}

/** An optional load from a form field: null when empty or 0 (bodyweight), undefined when invalid. */
export function optionalLoad(value: number | string | null | undefined): number | null | undefined {
  if (value === null || value === undefined || value === '') return null
  const load = Number(value)
  if (!Number.isFinite(load) || load < 0 || load > 500 || !Number.isInteger(load * 2)) return undefined
  return load === 0 ? null : load
}

export function emptyExerciseDraft(): ExerciseRequest { return { name: '', instructions: '', measure: 'hold', defaultLoadKg: null } }
export function exerciseRequest(draft: ExerciseRequest): ExerciseRequest | string {
  const name = draft.name.trim()
  if (!name) return 'Donnez un nom à l’exercice.'
  const load = optionalLoad(draft.defaultLoadKg)
  if (load === undefined) return 'Charge : 0 à 500 kg, par pas de 0,5 ; laissez vide au poids du corps.'
  return { name, instructions: draft.instructions?.trim() || null, measure: draft.measure, defaultLoadKg: load }
}
