import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  daysLabel, doneText, draftFromRoutine, emptyRoutineDraft, exerciseCharts, moveLine, newLine, nextDayLabel, routineCelebration, routineChip,
  routineDayLevel, routineRequest, routineSummary, stepLoad, stepValue, targetText,
} from './routine-format.ts'
import type { ExerciseDto, RoutineDto, RoutineLineDto, RoutineReportDto } from './sports.models.ts'

const wallSit: ExerciseDto = { id: 'e1', name: 'Chaise contre le mur', instructions: 'Dos au mur.', measure: 'hold', withLoad: true, hasImage: false, isArchived: false, measureLocked: true }
const bridge: ExerciseDto = { id: 'e2', name: 'Pont fessier', instructions: null, measure: 'reps', withLoad: false, hasImage: false, isArchived: false, measureLocked: false }

function line(exercise: ExerciseDto, sets: number, target: number, loadKg: number | null): RoutineLineDto {
  return { exerciseId: exercise.id, exerciseName: exercise.name, instructions: exercise.instructions, measure: exercise.measure, withLoad: exercise.withLoad, hasImage: false, sets, target, loadKg }
}
const routine: RoutineDto = {
  id: 'r', name: 'Tendons', days: [2, 4, 6], jokersPerWeek: 1, lines: [line(wallSit, 2, 45, 10), line(bridge, 1, 12, null)],
  startedOn: '2026-11-01', stoppedOn: null, isActive: true,
}
function report(patch: Partial<RoutineReportDto>): RoutineReportDto {
  return {
    routine, today: '2026-11-17', plannedToday: true, doneToday: 1, totalToday: 2, completedToday: false, nextPlannedDay: '2026-11-19',
    streak: 4, record: 9, jokersPerWeek: 1, jokersLeftThisWeek: 1, todayCompletions: [], calendar: [], ...patch,
  }
}

describe('routine formatting', () => {
  it('writes targets and what was done with their unit and load', () => {
    assert.equal(targetText(routine.lines[0]!), '2 × 45 s · 10 kg')
    assert.equal(targetText(routine.lines[1]!), '12 rép.')
    assert.equal(doneText(routine.lines[0]!, { exerciseId: 'e1', date: '2026-11-17', value: 60, loadKg: 12.5, loggedAt: '' }), '60 s à 12,5 kg')
    assert.equal(doneText(routine.lines[1]!, { exerciseId: 'e2', date: '2026-11-17', value: 8, loadKg: null, loggedAt: '' }), '8 rép.')
  })

  it('names the days of a routine and its next day', () => {
    assert.equal(daysLabel([1, 2, 3, 4, 5, 6, 7]), 'Tous les jours')
    assert.equal(daysLabel([6, 2, 4]), 'mar., jeu., sam.')
    assert.equal(routineSummary(routine), 'mar., jeu., sam. · 2 exercices · 1 joker')
    assert.equal(nextDayLabel('2026-11-16', '2026-11-17'), 'Prochaine : demain')
    assert.equal(nextDayLabel('2026-11-16', '2026-11-19'), 'Prochaine : jeudi')
    assert.equal(nextDayLabel('2026-11-16', null), '')
  })

  it('celebrates only the tick that finishes the routine', () => {
    assert.equal(routineChip(report({})), 'En cours')
    assert.equal(routineChip(report({ doneToday: 0 })), 'À faire')
    const done = report({ doneToday: 2, completedToday: true, streak: 5 })
    assert.equal(routineChip(done), 'Faite')
    assert.equal(routineCelebration(report({}), done), 'Tendons : routine faite. Série de 5 jours.')
    assert.equal(routineCelebration(done, done), '')
    assert.equal(routineCelebration(report({ doneToday: 0 }), report({})), '')
  })

  it('colours a calendar day by the share of exercises ticked, apart from days off', () => {
    const day = (status: RoutineReportDto['calendar'][number]['status'], percent: number) => routineDayLevel({ date: '', done: 0, planned: 2, percent, status })
    assert.deepEqual([day('not_planned', 0), day('not_counted', 0), day('joker', 0), day('done', 100), day('missed', 50), day('missed', 25), day('in_progress', 0)],
      ['off', 'none', 'joker', 'full', 'high', 'low', 'zero'])
  })

  it('steps a hold by 5 seconds, repetitions by 1 and a load by half a kilo', () => {
    assert.equal(stepValue(45, 'hold', 1), 50)
    assert.equal(stepValue(3, 'hold', -1), 1)
    assert.equal(stepValue(12, 'reps', -1), 11)
    assert.equal(stepLoad(10, 1), 10.5)
    assert.equal(stepLoad(0, -1), 0)
  })

  it('draws one bar per week and leaves a week without the exercise empty', () => {
    const charts = exerciseCharts(wallSit, {
      exerciseId: 'e1', bestValue: 60, bestLoadKg: 12.5, daysDone: 9,
      weeks: [{ monday: '2026-11-02', bestValue: 45, bestLoadKg: 10 }, { monday: '2026-11-09', bestValue: null, bestLoadKg: null }, { monday: '2026-11-16', bestValue: 60, bestLoadKg: 12.5 }],
    }, monday => monday.slice(8))
    assert.deepEqual(charts.map(chart => chart.title), ['Tenue', 'Charge'])
    assert.deepEqual(charts[0]!.bars.map(bar => bar.height), [54, 0, 72])
    assert.deepEqual(charts[0]!.bars.map(bar => bar.label), ['45', '', '60'])
    assert.equal(charts[0]!.now, '60 s cette semaine')
    assert.equal(charts[1]!.now, '12,5 kg cette semaine')
    assert.equal(exerciseCharts(bridge, { exerciseId: 'e2', bestValue: null, bestLoadKg: null, daysDone: 0, weeks: [{ monday: '2026-11-16', bestValue: null, bestLoadKg: null }] }, monday => monday).length, 1)
  })

  it('builds the routine request and names the first problem', () => {
    const draft = draftFromRoutine(routine)
    assert.deepEqual(draft.days, [false, true, false, true, false, true, false])
    assert.deepEqual(routineRequest(draft, [wallSit, bridge]), {
      name: 'Tendons', days: [2, 4, 6], jokersPerWeek: 1,
      lines: [{ exerciseId: 'e1', sets: 2, target: 45, loadKg: 10 }, { exerciseId: 'e2', sets: 1, target: 12, loadKg: null }],
    })
    assert.equal(routineRequest(emptyRoutineDraft(), []), 'Donnez un nom à la routine.')
    assert.equal(routineRequest({ ...draft, days: Array<boolean>(7).fill(false) }, [wallSit, bridge]), 'Choisissez au moins un jour.')
    assert.equal(routineRequest({ ...draft, lines: [] }, [wallSit, bridge]), 'Ajoutez au moins un exercice.')
    assert.equal(routineRequest({ ...draft, lines: [{ exerciseId: 'e1', sets: 2, target: 45, loadKg: 10.2 }] }, [wallSit]), 'Chaise contre le mur : charge de 0 à 500 kg, par pas de 0,5.')
    assert.equal(routineRequest({ ...draft, lines: [{ exerciseId: 'e2', sets: 0, target: 12, loadKg: null }] }, [bridge]), 'Pont fessier : 1 à 20 séries.')
  })

  it('adds an exercise with a starting target and reorders the lines', () => {
    assert.deepEqual(newLine(wallSit), { exerciseId: 'e1', sets: 1, target: 30, loadKg: 0 })
    assert.deepEqual(newLine(bridge), { exerciseId: 'e2', sets: 1, target: 10, loadKg: null })
    const lines = [newLine(wallSit), newLine(bridge)]
    assert.deepEqual(moveLine(lines, 0, 1).map(item => item.exerciseId), ['e2', 'e1'])
    assert.equal(moveLine(lines, 0, -1), lines)
  })
})
