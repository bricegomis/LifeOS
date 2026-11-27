import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  clockTime, daysLabel, doneText, draftFromRoutine, emptyRoutineDraft, exerciseCharts, exerciseRequest, exerciseSummary, moveLine,
  newLine, nextDayLabel, openPass, optionalLoad, passesLine, routineAction, routineCelebration, routineChip, routineDayLevel, routinePercent,
  routineRequest, routineSummary, setButton, setsDone, targetText,
} from './routine-format.ts'
import type { ExerciseCompletionDto, ExerciseDto, RoutineDto, RoutineLineDto, RoutineReportDto, RoutineSessionDto } from './sports.models.ts'

const wallSit: ExerciseDto = { id: 'e1', name: 'Chaise contre le mur', instructions: 'Dos au mur.', measure: 'hold', defaultLoadKg: 10, hasImage: false, isArchived: false, measureLocked: true }
const bridge: ExerciseDto = { id: 'e2', name: 'Pont fessier', instructions: null, measure: 'reps', defaultLoadKg: null, hasImage: false, isArchived: false, measureLocked: false }

function line(exercise: ExerciseDto, sets: number, target: number, loadKg: number | null): RoutineLineDto {
  return { exerciseId: exercise.id, exerciseName: exercise.name, instructions: exercise.instructions, measure: exercise.measure, hasImage: false, sets, target, loadKg }
}
function tick(exerciseId: string, sets: number, patch: Partial<ExerciseCompletionDto> = {}): ExerciseCompletionDto {
  return { sessionId: 's', exerciseId, date: '2026-11-17', setsDone: sets, value: 45, loadKg: null, loggedAt: '', ...patch }
}
function pass(finished: boolean, startedAt: string, completions: ExerciseCompletionDto[] = []): RoutineSessionDto {
  return { id: startedAt, date: '2026-11-17', startedAt, finishedAt: null, finished, exercisesDone: finished ? 2 : 1, exercisesTotal: 2, comment: null, completions }
}
const routine: RoutineDto = {
  id: 'r', name: 'Tendons', days: [2, 4, 6], jokersPerWeek: 1, lines: [line(wallSit, 2, 45, 10), line(bridge, 1, 12, null)],
  startedOn: '2026-11-01', stoppedOn: null, isActive: true,
}
function report(patch: Partial<RoutineReportDto>): RoutineReportDto {
  return {
    routine, today: '2026-11-17', plannedToday: true, doneToday: 1, totalToday: 2, completedToday: false, nextPlannedDay: '2026-11-19',
    passesToday: 0, streak: 4, record: 9, jokersPerWeek: 1, jokersLeftThisWeek: 1, todaySessions: [], calendar: [], ...patch,
  }
}

describe('routine formatting', () => {
  it('writes targets and what was done with their unit and load', () => {
    assert.equal(targetText(routine.lines[0]!), '2 × 45 s · 10 kg')
    assert.equal(targetText(routine.lines[1]!), '12 rép.')
    assert.equal(doneText(routine.lines[0]!, tick('e1', 2, { value: 60, loadKg: 12.5 })), '2 × 60 s à 12,5 kg')
    assert.equal(doneText(routine.lines[1]!, tick('e2', 1, { value: 8 })), '8 rép.')
    assert.equal(exerciseSummary(wallSit), 'Tenue en secondes · 10 kg')
    assert.equal(exerciseSummary(bridge), 'Répétitions · poids du corps')
  })

  it('names the days of a routine and its next day', () => {
    assert.equal(daysLabel([1, 2, 3, 4, 5, 6, 7]), 'Tous les jours')
    assert.equal(daysLabel([6, 2, 4]), 'mar., jeu., sam.')
    assert.equal(routineSummary(routine), 'mar., jeu., sam. · 2 exercices · 1 joker')
    assert.equal(nextDayLabel('2026-11-16', '2026-11-17'), 'Prochaine : demain')
    assert.equal(nextDayLabel('2026-11-16', '2026-11-19'), 'Prochaine : jeudi')
    assert.equal(nextDayLabel('2026-11-16', null), '')
  })

  it('follows today’s passes: one open at a time, celebrated when one ends', () => {
    const open = pass(false, '2026-11-17T13:40:00Z', [tick('e1', 1)])
    const morning = pass(true, '2026-11-17T08:12:00Z')
    assert.equal(routineChip(report({ todaySessions: [open] })), 'En cours')
    assert.equal(routineChip(report({})), 'À faire')
    assert.equal(routineAction(report({})), 'Commencer')
    assert.equal(routineAction(report({ todaySessions: [open] })), 'Continuer le passage')
    const done = report({ completedToday: true, passesToday: 1, streak: 5, todaySessions: [morning] })
    assert.equal(routineChip(done), 'Faite')
    assert.equal(routineAction(done), 'Nouveau passage')
    assert.equal(routinePercent(report({ todaySessions: [open] })), 50)
    assert.equal(routinePercent(done), 100)
    assert.equal(openPass(report({ todaySessions: [morning, open] })), open)
    assert.equal(clockTime('2026-11-17T08:12:00Z'), '9 h 12')
    assert.equal(passesLine(report({ todaySessions: [morning, pass(true, '2026-11-17T13:40:00Z'), open] })), '2 passages : 9 h 12, 14 h 40')
    assert.equal(passesLine(report({})), 'Aucun passage terminé aujourd’hui')
    assert.equal(routineCelebration(report({}), done), 'Tendons : routine faite. Série de 5 jours.')
    assert.equal(routineCelebration(done, report({ completedToday: true, passesToday: 2 })), 'Tendons : passage terminé, 2 aujourd’hui.')
    assert.equal(routineCelebration(done, done), '')
  })

  it('counts sets on one button that fills until the exercise is done', () => {
    const open = pass(false, 'x', [tick('e1', 1)])
    assert.equal(setsDone(open, 'e1'), 1)
    assert.equal(setsDone(open, 'e2'), 0)
    assert.equal(setsDone(null, 'e1'), 0)
    assert.deepEqual(setButton(routine.lines[0]!, 1), {
      label: 'Série faite', counter: '1 / 2', percent: 50, full: false, aria: 'Série faite pour Chaise contre le mur, 1 sur 2 pour l’instant',
    })
    const full = setButton(routine.lines[0]!, 2)
    assert.equal(full.label, 'Exercice fait')
    assert.equal(full.full, true)
    assert.equal(full.percent, 100)
  })


  it('colours a calendar day by the share of exercises ticked, apart from days off', () => {
    const day = (status: RoutineReportDto['calendar'][number]['status'], percent: number) => routineDayLevel({ date: '', done: 0, planned: 2, percent, status, passes: 0 })
    assert.deepEqual([day('not_planned', 0), day('not_counted', 0), day('joker', 0), day('done', 100), day('missed', 50), day('missed', 25), day('in_progress', 0)],
      ['off', 'none', 'joker', 'full', 'high', 'low', 'zero'])
  })

  it('draws one bar per week and leaves a week without the exercise empty', () => {
    const charts = exerciseCharts(wallSit, {
      exerciseId: 'e1', bestValue: 60, bestLoadKg: 12.5, daysDone: 9, recent: [],
      weeks: [{ monday: '2026-11-02', bestValue: 45, bestLoadKg: 10 }, { monday: '2026-11-09', bestValue: null, bestLoadKg: null }, { monday: '2026-11-16', bestValue: 60, bestLoadKg: 12.5 }],
    }, monday => monday.slice(8))
    assert.deepEqual(charts.map(chart => chart.title), ['Tenue', 'Charge'])
    assert.deepEqual(charts[0]!.bars.map(bar => bar.height), [54, 0, 72])
    assert.deepEqual(charts[0]!.bars.map(bar => bar.label), ['45', '', '60'])
    assert.equal(charts[0]!.now, '60 s cette semaine')
    assert.equal(charts[1]!.now, '12,5 kg cette semaine')
    assert.equal(exerciseCharts(bridge, { exerciseId: 'e2', bestValue: null, bestLoadKg: null, daysDone: 0, recent: [], weeks: [{ monday: '2026-11-16', bestValue: null, bestLoadKg: null }] }, monday => monday).length, 1)
  })

  it('builds the routine request and names the first problem', () => {
    const draft = draftFromRoutine(routine)
    assert.deepEqual(draft.days, [false, true, false, true, false, true, false])
    assert.deepEqual(routineRequest(draft, [wallSit, bridge]), {
      name: 'Tendons', days: [2, 4, 6], jokersPerWeek: 1,
      lines: [{ exerciseId: 'e1', sets: 2, target: 45, loadKg: 10 }, { exerciseId: 'e2', sets: 1, target: 12, loadKg: 0 }],
    })
    assert.equal(routineRequest(emptyRoutineDraft(), []), 'Donnez un nom à la routine.')
    assert.equal(routineRequest({ ...draft, days: Array<boolean>(7).fill(false) }, [wallSit, bridge]), 'Choisissez au moins un jour.')
    assert.equal(routineRequest({ ...draft, lines: [] }, [wallSit, bridge]), 'Ajoutez au moins un exercice.')
    assert.equal(routineRequest({ ...draft, lines: [{ exerciseId: 'e1', sets: 2, target: 45, loadKg: 10.2 }] }, [wallSit]), 'Chaise contre le mur : charge de 0 à 500 kg, par pas de 0,5.')
    assert.equal(routineRequest({ ...draft, lines: [{ exerciseId: 'e2', sets: 0, target: 12, loadKg: null }] }, [bridge]), 'Pont fessier : 1 à 20 séries.')
  })

  it('adds an exercise with a starting target and reorders the lines', () => {
    assert.deepEqual(newLine(wallSit), { exerciseId: 'e1', sets: 1, target: 30, loadKg: 10 })
    assert.deepEqual(newLine(bridge), { exerciseId: 'e2', sets: 1, target: 10, loadKg: null })
    const lines = [newLine(wallSit), newLine(bridge)]
    assert.deepEqual(moveLine(lines, 0, 1).map(item => item.exerciseId), ['e2', 'e1'])
    assert.equal(moveLine(lines, 0, -1), lines)
  })

  it('reads an optional load: empty or 0 is bodyweight', () => {
    assert.equal(optionalLoad(''), null)
    assert.equal(optionalLoad(0), null)
    assert.equal(optionalLoad('12.5'), 12.5)
    assert.equal(optionalLoad(12.3), undefined)
    assert.deepEqual(exerciseRequest({ name: ' Gainage ', instructions: ' ', measure: 'hold', defaultLoadKg: 0 }),
      { name: 'Gainage', instructions: null, measure: 'hold', defaultLoadKg: null })
    assert.equal(exerciseRequest({ name: 'Gainage', instructions: null, measure: 'hold', defaultLoadKg: 501 }),
      'Charge : 0 à 500 kg, par pas de 0,5 ; laissez vide au poids du corps.')
  })
})
