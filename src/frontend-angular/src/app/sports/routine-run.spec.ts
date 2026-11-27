import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  countdownText, currentStepIndex, elapsedPercent, nextLabel, nextStep, roundCount, runSteps, runSummary, setTarget, stepPosition, warningSecond,
} from './routine-run.ts'
import type { ExerciseCompletionDto, RoutineLineDto, RoutineSessionDto } from './sports.models.ts'

function line(exerciseId: string, name: string, sets: number, target: number, measure: 'hold' | 'reps', loadKg: number | null = null): RoutineLineDto {
  return { exerciseId, exerciseName: name, instructions: null, measure, hasImage: false, sets, target, loadKg }
}
const wallSit = line('e1', 'Chaise', 3, 45, 'hold', 10)
const bridge = line('e2', 'Pont fessier', 1, 12, 'reps')
const plank = line('e3', 'Gainage', 2, 30, 'hold')
const lines = [wallSit, bridge, plank]

function pass(ticks: Record<string, number>): RoutineSessionDto {
  const completions: ExerciseCompletionDto[] = Object.entries(ticks).map(([exerciseId, setsDone]) => ({
    sessionId: 's', exerciseId, date: '2026-11-27', setsDone, value: 0, loadKg: null, loggedAt: '',
  }))
  return { id: 's', date: '2026-11-27', startedAt: '', finishedAt: null, finished: false, exercisesDone: 0, exercisesTotal: 3, comment: null, completions }
}

describe('guided routine run', () => {
  it('orders the sets as a circuit, one round per set', () => {
    assert.equal(roundCount(lines), 3)
    assert.deepEqual(runSteps(lines).map(step => `${step.line.exerciseId}:${step.round}`), ['e1:1', 'e2:1', 'e3:1', 'e1:2', 'e3:2', 'e1:3'])
    assert.equal(runSummary(lines), '3 exercices · 3 tours · 6 séries')
    assert.equal(runSummary([bridge]), '1 exercice · 1 série')
  })

  it('resumes at the first set not ticked, whatever was ticked elsewhere', () => {
    const steps = runSteps(lines)
    assert.equal(currentStepIndex(steps, null), 0)
    assert.equal(currentStepIndex(steps, pass({ e1: 1, e2: 1 })), 2)
    // Two sets of the wall sit already ticked on the routine page: round 2 goes on with the plank.
    const ahead = pass({ e1: 2, e2: 1, e3: 1 })
    assert.equal(currentStepIndex(steps, ahead), 4)
    assert.equal(nextStep(steps, 4, ahead)?.line.exerciseId, 'e1')
    assert.equal(currentStepIndex(steps, pass({ e1: 3, e2: 1, e3: 2 })), steps.length)
    assert.equal(nextStep(steps, 5, pass({ e1: 2, e2: 1, e3: 2 })), null)
  })

  it('describes the position, the target and what comes next', () => {
    const steps = runSteps(lines)
    assert.equal(setTarget(wallSit), '45 s · 10 kg')
    assert.equal(setTarget(bridge), '12 rép.')
    assert.equal(stepPosition(steps[3]!, 3, 3), 'Tour 2 / 3 · série 2 / 3')
    assert.equal(stepPosition(steps[1]!, 3, 3), 'Tour 1 / 3')
    assert.equal(stepPosition(runSteps([bridge])[0]!, 1, 1), '')
    assert.equal(stepPosition(runSteps([plank])[1]!, 2, 1), 'Série 2 / 2')
    assert.equal(nextLabel(steps[1]!, 3, steps[0]!), 'Ensuite : Pont fessier, 12 rép.')
    assert.equal(nextLabel(steps[3]!, 3, steps[2]!), 'Ensuite : Chaise, 45 s · 10 kg (tour 2)')
    assert.equal(nextLabel(null, 3, steps[5]!), 'Dernière série du passage')
  })

  it('counts down in whole seconds and beeps on the last three', () => {
    assert.equal(countdownText(45_000), '0:45')
    assert.equal(countdownText(44_100), '0:45')
    assert.equal(countdownText(90_000), '1:30')
    assert.equal(countdownText(0), '0:00')
    assert.equal(countdownText(-200), '0:00')
    assert.equal(elapsedPercent(30_000, 60_000), 50)
    assert.equal(elapsedPercent(-1, 60_000), 100)
    assert.equal(warningSecond(3_100, 2_900), 3)
    assert.equal(warningSecond(2_900, 2_700), null)
    assert.equal(warningSecond(4_100, 3_900), null)
    assert.equal(warningSecond(1_100, 900), 1)
    assert.equal(warningSecond(100, 0), null)
  })
})
