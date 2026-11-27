import { measureUnit, setsDone, hasLoad, kilos } from './routine-format.ts'
import type { RoutineLineDto, RoutineSessionDto } from './sports.models.ts'

/**
 * Guided run of a routine (ADR 0015, « Mode guidé »): the exercises one after the other, as a circuit.
 * Round 1 does one set of every exercise in the routine's order, round 2 one more set of those with
 * at least two sets, and so on. Progress is read from the pass's ticks, so a run resumes where it stopped.
 */
export interface RunStep { line: RoutineLineDto, round: number }

/** Seconds of « Préparez-vous » before a hold starts. */
export const LEAD_SECONDS = 5
/** Beeps on the last seconds of a countdown. */
export const WARNING_SECONDS = 3

export function roundCount(lines: RoutineLineDto[]): number {
  return Math.max(0, ...lines.map(line => line.sets))
}

/** Every set of the routine in circuit order. */
export function runSteps(lines: RoutineLineDto[]): RunStep[] {
  const steps: RunStep[] = []
  for (let round = 1; round <= roundCount(lines); round++) {
    for (const line of lines) if (line.sets >= round) steps.push({ line, round })
  }
  return steps
}

/** A step is done once its exercise has at least that many sets ticked in the pass. */
export function stepDone(step: RunStep, pass: RoutineSessionDto | null): boolean {
  return setsDone(pass, step.line.exerciseId) >= step.round
}

/** Index of the first step left to do; steps.length when everything is done. */
export function currentStepIndex(steps: RunStep[], pass: RoutineSessionDto | null): number {
  const index = steps.findIndex(step => !stepDone(step, pass))
  return index === -1 ? steps.length : index
}

/** The step to do after the given one, skipping those already done. */
export function nextStep(steps: RunStep[], index: number, pass: RoutineSessionDto | null): RunStep | null {
  return steps.slice(index + 1).find(step => !stepDone(step, pass)) ?? null
}

/** "45 s · 10 kg" or "12 rép.": what one set asks for. */
export function setTarget(line: RoutineLineDto): string {
  const load = hasLoad(line.loadKg) ? ` · ${kilos(line.loadKg)} kg` : ''
  return `${line.target} ${measureUnit(line.measure)}${load}`
}

/** "Tour 2 / 3 · série 2 / 4"; just "Série 2 / 4" with one exercise; nothing for a single set. */
export function stepPosition(step: RunStep, rounds: number, exercises: number): string {
  const set = step.line.sets > 1 ? `série ${step.round} / ${step.line.sets}` : ''
  if (rounds <= 1 || exercises <= 1) return set ? set.charAt(0).toUpperCase() + set.slice(1) : ''
  return `Tour ${step.round} / ${rounds}` + (set ? ` · ${set}` : '')
}

/** "Ensuite : Pont fessier, 12 rép." or the end of the pass. */
export function nextLabel(next: RunStep | null, rounds: number, current: RunStep | null): string {
  if (!next) return 'Dernière série du passage'
  const round = current && next.round > current.round && rounds > 1 ? ` (tour ${next.round})` : ''
  return `Ensuite : ${next.line.exerciseName}, ${setTarget(next.line)}${round}`
}

/** "0:45", "1:30": whole seconds left, rounded up so 0:00 shows only at the end. */
export function countdownText(remainingMs: number): string {
  const seconds = Math.max(0, Math.ceil(remainingMs / 1000))
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

/** Share of the countdown already elapsed, 0 to 100. */
export function elapsedPercent(remainingMs: number, totalMs: number): number {
  if (totalMs <= 0) return 100
  return Math.round(Math.min(1, Math.max(0, 1 - remainingMs / totalMs)) * 100)
}

/** The whole second to beep for, when one of the last seconds has just been reached; null otherwise. */
export function warningSecond(previousMs: number, remainingMs: number): number | null {
  const before = Math.ceil(previousMs / 1000), now = Math.ceil(remainingMs / 1000)
  return now < before && now >= 1 && now <= WARNING_SECONDS ? now : null
}

/** "3 exercices · 4 tours · 11 séries" for the start screen. */
export function runSummary(lines: RoutineLineDto[]): string {
  const rounds = roundCount(lines), sets = runSteps(lines).length
  const plural = (count: number, word: string) => `${count} ${word}${count > 1 ? 's' : ''}`
  return [plural(lines.length, 'exercice'), rounds > 1 ? plural(rounds, 'tour') : '', plural(sets, 'série')].filter(Boolean).join(' · ')
}
