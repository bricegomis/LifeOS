import { inject, Injectable } from '@angular/core'
import { from, switchMap, type Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import { LIBRARY_IMAGE, reduceImage } from '@/app/shared/image-reduction'
import type {
  ChallengeReportDto, DailyChallengeDto, DailyChallengeRequest, ExerciseDto, ExerciseProgressDto, ExerciseRequest,
  CreatedImportTokenDto, GarminStatusDto, ImportTokenDto, RoutineReportDto, RoutineRequest, SportCatalog, SportSessionDto, Vo2MaxDto, SportSessionRequest,
  SportTemplateDto, SportTemplateRequest, WeekReportDto, WeeklyGoalDto, WeeklyGoalRequest,
} from './sports.models'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the Sport screens. Without a profile, the API uses the household's main profile. */
@Injectable({ providedIn: 'root' })
export class SportsApiService {
  private readonly api = inject(LifeosApiService)

  catalog(): Observable<SportCatalog> { return this.api.get<SportCatalog>('/sport/catalog') }

  templates(): Observable<SportTemplateDto[]> { return this.api.get<SportTemplateDto[]>('/sport-templates') }
  createTemplate(request: SportTemplateRequest): Observable<SportTemplateDto> { return this.api.post<SportTemplateDto>('/sport-templates', request) }
  updateTemplate(templateId: string, request: SportTemplateRequest): Observable<SportTemplateDto> {
    return this.api.put<SportTemplateDto>(`/sport-templates/${id(templateId)}`, request)
  }
  archiveTemplate(templateId: string): Observable<void> { return this.api.delete(`/sport-templates/${id(templateId)}`) }

  session(sessionId: string): Observable<SportSessionDto> { return this.api.get<SportSessionDto>(`/sport/sessions/${id(sessionId)}`) }
  sessions(from: string, to: string): Observable<SportSessionDto[]> { return this.api.get<SportSessionDto[]>('/sport/sessions', { from, to }) }
  vo2max(from: string, to: string): Observable<Vo2MaxDto[]> { return this.api.get<Vo2MaxDto[]>('/sport/vo2max', { from, to }) }

  garmin(): Observable<GarminStatusDto> { return this.api.get<GarminStatusDto>('/garmin') }
  requestGarminSync(): Observable<GarminStatusDto> { return this.api.post<GarminStatusDto>('/garmin/sync', {}) }
  reconnectGarmin(): Observable<GarminStatusDto> { return this.api.post<GarminStatusDto>('/garmin/reconnect', {}) }
  sendGarminCode(code: string): Observable<GarminStatusDto> { return this.api.post<GarminStatusDto>('/garmin/code', { code }) }
  importTokens(): Observable<ImportTokenDto[]> { return this.api.get<ImportTokenDto[]>('/import-tokens') }
  createImportToken(): Observable<CreatedImportTokenDto> { return this.api.post<CreatedImportTokenDto>('/import-tokens', { name: 'garmin-sync' }) }
  revokeImportToken(tokenId: string): Observable<void> { return this.api.delete(`/import-tokens/${id(tokenId)}`) }

  createSession(request: SportSessionRequest): Observable<SportSessionDto> { return this.api.post<SportSessionDto>('/sport/sessions', request) }
  updateSession(sessionId: string, request: SportSessionRequest): Observable<SportSessionDto> {
    return this.api.put<SportSessionDto>(`/sport/sessions/${id(sessionId)}`, request)
  }
  deleteSession(sessionId: string): Observable<void> { return this.api.delete(`/sport/sessions/${id(sessionId)}`) }

  /** Week reports, oldest first, ending with the current week. */
  weeks(count: number): Observable<WeekReportDto[]> { return this.api.get<WeekReportDto[]>('/sport/weeks', { count }) }

  goals(): Observable<WeeklyGoalDto[]> { return this.api.get<WeeklyGoalDto[]>('/sport/goals') }
  createGoal(request: WeeklyGoalRequest): Observable<WeeklyGoalDto> { return this.api.post<WeeklyGoalDto>('/sport/goals', request) }
  /** Changes the target from the current week; past weeks keep the goal they had. */
  updateGoal(goalId: string, request: WeeklyGoalRequest): Observable<WeeklyGoalDto> {
    return this.api.put<WeeklyGoalDto>(`/sport/goals/${id(goalId)}`, request)
  }
  stopGoal(goalId: string): Observable<void> { return this.api.delete(`/sport/goals/${id(goalId)}`) }

  challenges(calendarWeeks = 1, includeStopped = false): Observable<ChallengeReportDto[]> {
    return this.api.get<ChallengeReportDto[]>('/sport/challenges', { calendarWeeks, includeStopped })
  }
  createChallenge(request: DailyChallengeRequest): Observable<DailyChallengeDto> {
    return this.api.post<DailyChallengeDto>('/sport/challenges', request)
  }
  updateChallenge(challengeId: string, request: DailyChallengeRequest): Observable<DailyChallengeDto> {
    return this.api.put<DailyChallengeDto>(`/sport/challenges/${id(challengeId)}`, request)
  }
  stopChallenge(challengeId: string): Observable<void> { return this.api.delete(`/sport/challenges/${id(challengeId)}`) }
  addEntry(challengeId: string, quantity: number): Observable<ChallengeReportDto> {
    return this.api.post<ChallengeReportDto>(`/sport/challenges/${id(challengeId)}/entries`, { quantity })
  }
  removeEntry(challengeId: string, entryId: string): Observable<ChallengeReportDto> {
    return this.api.delete<ChallengeReportDto>(`/sport/challenges/${id(challengeId)}/entries/${id(entryId)}`)
  }

  exercises(includeArchived = false): Observable<ExerciseDto[]> { return this.api.get<ExerciseDto[]>('/sport/exercises', { includeArchived }) }
  createExercise(request: ExerciseRequest): Observable<ExerciseDto> { return this.api.post<ExerciseDto>('/sport/exercises', request) }
  updateExercise(exerciseId: string, request: ExerciseRequest): Observable<ExerciseDto> {
    return this.api.put<ExerciseDto>(`/sport/exercises/${id(exerciseId)}`, request)
  }
  archiveExercise(exerciseId: string): Observable<void> { return this.api.delete(`/sport/exercises/${id(exerciseId)}`) }
  /** Reduces the picture, then uploads it; returns the updated exercise. */
  uploadExerciseImage(exerciseId: string, file: File): Observable<ExerciseDto> {
    return from(reduceImage(file, LIBRARY_IMAGE)).pipe(switchMap(image => {
      const form = new FormData()
      form.append('image', image)
      return this.api.put<ExerciseDto>(`/sport/exercises/${id(exerciseId)}/image`, form)
    }))
  }
  removeExerciseImage(exerciseId: string): Observable<void> { return this.api.delete(`/sport/exercises/${id(exerciseId)}/image`) }
  exerciseProgress(exerciseId: string, weeks = 12): Observable<ExerciseProgressDto> {
    return this.api.get<ExerciseProgressDto>(`/sport/exercises/${id(exerciseId)}/progress`, { weeks })
  }

  routines(calendarWeeks = 1, includeStopped = false): Observable<RoutineReportDto[]> {
    return this.api.get<RoutineReportDto[]>('/sport/routines', { calendarWeeks, includeStopped })
  }
  createRoutine(request: RoutineRequest): Observable<RoutineReportDto> { return this.api.post<RoutineReportDto>('/sport/routines', request) }
  updateRoutine(routineId: string, request: RoutineRequest): Observable<RoutineReportDto> {
    return this.api.put<RoutineReportDto>(`/sport/routines/${id(routineId)}`, request)
  }
  stopRoutine(routineId: string): Observable<void> { return this.api.delete(`/sport/routines/${id(routineId)}`) }
  /** Opens a pass for today; refused while another pass of the day is unfinished. */
  startSession(routineId: string): Observable<RoutineReportDto> {
    return this.api.post<RoutineReportDto>(`/sport/routines/${id(routineId)}/sessions`, { date: null })
  }
  /** Sets done for an exercise in a pass, with the target's value and load; 0 unticks it. */
  setSets(routineId: string, sessionId: string, exerciseId: string, setsDone: number): Observable<RoutineReportDto> {
    return this.api.put<RoutineReportDto>(`/sport/routines/${id(routineId)}/sessions/${id(sessionId)}/exercises/${id(exerciseId)}`, { setsDone })
  }
  deletePass(routineId: string, sessionId: string): Observable<RoutineReportDto> {
    return this.api.delete<RoutineReportDto>(`/sport/routines/${id(routineId)}/sessions/${id(sessionId)}`)
  }
}
