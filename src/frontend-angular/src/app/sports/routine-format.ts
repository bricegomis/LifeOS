import type {
  ExerciseCompletionDto, ExerciseDto, ExerciseMeasure, ExerciseProgressDto, ExerciseRequest, RoutineDayDto, RoutineDto, RoutineLineDto,
  RoutineReportDto, RoutineRequest,
} from './sports.models.ts'

const SHORT_DAYS = ['lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.', 'dim.']
const LONG_DAYS = ['lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi', 'dimanche']
export const DAY_LETTERS = ['L', 'M', 'M', 'J', 'V', 'S', 'D']
export const DAY_NAMES = ['Lundi', 'Mardi', 'Mercredi', 'Jeudi', 'Vendredi', 'Samedi', 'Dimanche']

/** A load in kg with a French comma: 12,5. */
export function kilos(value: number): string { return String(value).replace('.', ',') }
export function measureUnit(measure: ExerciseMeasure): string { return measure === 'hold' ? 's' : 'rép.' }
export function measureLabel(measure: ExerciseMeasure): string { return measure === 'hold' ? 'Tenue en secondes' : 'Répétitions' }
export function exerciseSummary(exercise: ExerciseDto): string {
  return measureLabel(exercise.measure) + (exercise.withLoad ? ' · avec charge' : '')
}

/** "2 × 45 s · 10 kg": the target of a routine line. */
export function targetText(line: RoutineLineDto): string {
  const sets = line.sets > 1 ? `${line.sets} × ` : ''
  const load = line.withLoad && line.loadKg !== null ? ` · ${kilos(line.loadKg)} kg` : ''
  return `${sets}${line.target} ${measureUnit(line.measure)}${load}`
}
/** "60 s à 12 kg": what was really done. */
export function doneText(line: RoutineLineDto, completion: ExerciseCompletionDto): string {
  const load = line.withLoad && completion.loadKg !== null ? ` à ${kilos(completion.loadKg)} kg` : ''
  return `${completion.value} ${measureUnit(line.measure)}${load}`
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
export function routineChip(report: RoutineReportDto): string {
  return report.completedToday ? 'Faite' : report.doneToday ? 'En cours' : 'À faire'
}
export function routinePercent(report: RoutineReportDto): number {
  return report.totalToday ? Math.round(report.doneToday * 100 / report.totalToday) : 0
}
export function routineAction(report: RoutineReportDto): string {
  return report.completedToday ? 'Revoir la routine' : report.doneToday ? 'Continuer' : 'Commencer'
}
export function routineJokerLine(report: RoutineReportDto): string {
  if (!report.jokersPerWeek) return 'Sans joker'
  const left = report.jokersLeftThisWeek
  return `Jokers cette semaine : ${left} sur ${report.jokersPerWeek} restant${left > 1 ? 's' : ''}`
}
/** Message shown when the last exercise of the day is ticked; empty otherwise. */
export function routineCelebration(before: RoutineReportDto, after: RoutineReportDto): string {
  if (before.completedToday || !after.completedToday) return ''
  return `${after.routine.name} : routine faite. Série de ${after.streak} jour${after.streak > 1 ? 's' : ''}.`
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
  return `${dayLabel}${count} (${ROUTINE_STATUS[day.status]})`
}

/** Steps of the +/− buttons: 5 seconds for a hold, 1 repetition, 0.5 kg. */
export function valueStep(measure: ExerciseMeasure): number { return measure === 'hold' ? 5 : 1 }
export function stepValue(value: number, measure: ExerciseMeasure, direction: 1 | -1): number {
  const step = valueStep(measure)
  return Math.max(1, Math.min(10_000, value + direction * step))
}
export function stepLoad(loadKg: number, direction: 1 | -1): number {
  return Math.max(0, Math.min(500, Math.round((loadKg + direction * 0.5) * 2) / 2))
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
  if (exercise.withLoad || progress.bestLoadKg !== null) {
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
  return { exerciseId: exercise.id, sets: 1, target: exercise.measure === 'hold' ? 30 : 10, loadKg: exercise.withLoad ? 0 : null }
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
    const sets = Number(line.sets), target = Number(line.target), load = Number(line.loadKg ?? 0)
    if (!Number.isInteger(sets) || sets < 1 || sets > 20) return `${exercise.name} : 1 à 20 séries.`
    if (!Number.isInteger(target) || target < 1 || target > 10_000) return `${exercise.name} : la cible va de 1 à 10 000.`
    if (exercise.withLoad && (load < 0 || load > 500 || !Number.isInteger(load * 2))) return `${exercise.name} : charge de 0 à 500 kg, par pas de 0,5.`
    lines.push({ exerciseId: exercise.id, sets, target, loadKg: exercise.withLoad ? load : null })
  }
  return { name, days, jokersPerWeek: draft.jokersPerWeek, lines }
}

export function emptyExerciseDraft(): ExerciseRequest { return { name: '', instructions: '', measure: 'hold', withLoad: false } }
export function exerciseRequest(draft: ExerciseRequest): ExerciseRequest | string {
  const name = draft.name.trim()
  if (!name) return 'Donnez un nom à l’exercice.'
  return { name, instructions: draft.instructions?.trim() || null, measure: draft.measure, withLoad: draft.withLoad }
}
