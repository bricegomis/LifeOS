import { measureUnit, setsDone, hasLoad, kilos, sideSuffix } from './routine-format.ts'
import type { RoutineLineDto, RoutineSessionDto } from './sports.models.ts'

/**
 * Guided run of a routine (ADR 0015, « Mode guidé »): the exercises one after the other, as a circuit.
 * Round 1 does one set of every exercise in the routine's order, round 2 one more set of those with
 * at least two sets, and so on. A per-side exercise splits each set into the left side then the right
 * side; the set is ticked once the right side is done. Progress is read from the pass's ticks, so a run
 * resumes where it stopped; a left side done but not yet ticked is only remembered by the open page.
 */
export type Side = 'left' | 'right'
export interface RunStep { line: RoutineLineDto, round: number, side: Side | null }

/** Seconds of « Préparez-vous » before a hold starts. */
export const LEAD_SECONDS = 5
/** Beeps on the last seconds of a countdown. */
export const WARNING_SECONDS = 3

export function roundCount(lines: RoutineLineDto[]): number {
  return Math.max(0, ...lines.map(line => line.sets))
}

const NO_SIDES: ReadonlySet<string> = new Set()

/** Every set of the routine in circuit order, a per-side set as its left then its right side. */
export function runSteps(lines: RoutineLineDto[]): RunStep[] {
  const steps: RunStep[] = []
  for (let round = 1; round <= roundCount(lines); round++) {
    for (const line of lines) {
      if (line.sets < round) continue
      if (line.perSide) steps.push({ line, round, side: 'left' }, { line, round, side: 'right' })
      else steps.push({ line, round, side: null })
    }
  }
  return steps
}

/** Key of a set whose left side is done but not yet ticked. */
export function setKey(step: RunStep): string { return `${step.line.exerciseId}:${step.round}` }

/**
 * A step is done once its exercise has at least that many sets ticked in the pass; a left side also
 * once it is in `leftDone`.
 */
export function stepDone(step: RunStep, pass: RoutineSessionDto | null, leftDone: ReadonlySet<string> = NO_SIDES): boolean {
  return setsDone(pass, step.line.exerciseId) >= step.round || (step.side === 'left' && leftDone.has(setKey(step)))
}

/** Index of the first step left to do; steps.length when everything is done. */
export function currentStepIndex(steps: RunStep[], pass: RoutineSessionDto | null, leftDone: ReadonlySet<string> = NO_SIDES): number {
  const index = steps.findIndex(step => !stepDone(step, pass, leftDone))
  return index === -1 ? steps.length : index
}

/** The step to do after the given one, skipping those already done. */
export function nextStep(steps: RunStep[], index: number, pass: RoutineSessionDto | null, leftDone: ReadonlySet<string> = NO_SIDES): RunStep | null {
  return steps.slice(index + 1).find(step => !stepDone(step, pass, leftDone)) ?? null
}

/** Sets of the run: a per-side set counts once. */
export function setCount(steps: RunStep[]): number { return steps.filter(step => step.side !== 'right').length }
/** 1-based number of the set the step belongs to. */
export function setNumber(steps: RunStep[], index: number): number { return setCount(steps.slice(0, index + 1)) }

export function sideLabel(side: Side | null): string { return side === 'left' ? 'Côté gauche' : side === 'right' ? 'Côté droit' : '' }

/** "45 s · 10 kg", "12 rép." or "30 s par côté": what one set asks for. */
export function setTarget(line: RoutineLineDto): string {
  const load = hasLoad(line.loadKg) ? ` · ${kilos(line.loadKg)} kg` : ''
  return `${line.target} ${measureUnit(line.measure)}${sideSuffix(line)}${load}`
}

/** "Tour 2 / 3 · série 2 / 4"; just "Série 2 / 4" with one exercise; nothing for a single set. */
export function stepPosition(step: RunStep, rounds: number, exercises: number): string {
  const set = step.line.sets > 1 ? `série ${step.round} / ${step.line.sets}` : ''
  if (rounds <= 1 || exercises <= 1) return set ? set.charAt(0).toUpperCase() + set.slice(1) : ''
  return `Tour ${step.round} / ${rounds}` + (set ? ` · ${set}` : '')
}

/** "Ensuite : Pont fessier, 12 rép.", "Ensuite : côté droit" or the end of the pass. */
export function nextLabel(next: RunStep | null, rounds: number, current: RunStep | null): string {
  if (!next) return 'Dernière série du passage'
  if (current?.side === 'left' && next.side === 'right' && setKey(next) === setKey(current)) return 'Ensuite : côté droit'
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
  const rounds = roundCount(lines), sets = setCount(runSteps(lines))
  const plural = (count: number, word: string) => `${count} ${word}${count > 1 ? 's' : ''}`
  return [plural(lines.length, 'exercice'), rounds > 1 ? plural(rounds, 'tour') : '', plural(sets, 'série')].filter(Boolean).join(' · ')
}
