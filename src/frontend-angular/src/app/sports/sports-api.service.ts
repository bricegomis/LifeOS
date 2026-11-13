import { inject, Injectable } from '@angular/core'
import type { Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type {
  ChallengeReportDto, DailyChallengeDto, DailyChallengeRequest, SportCatalog, SportSessionDto, SportSessionRequest,
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

  createSession(request: SportSessionRequest): Observable<SportSessionDto> { return this.api.post<SportSessionDto>('/sport/sessions', request) }
  updateSession(sessionId: string, request: SportSessionRequest): Observable<SportSessionDto> {
    return this.api.put<SportSessionDto>(`/sport/sessions/${id(sessionId)}`, request)
  }
  deleteSession(sessionId: string): Observable<void> { return this.api.delete(`/sport/sessions/${id(sessionId)}`) }

  /** Week reports, oldest first, ending with the current week. */
  weeks(count: number): Observable<WeekReportDto[]> { return this.api.get<WeekReportDto[]>('/sport/weeks', { count }) }

  goals(): Observable<WeeklyGoalDto[]> { return this.api.get<WeeklyGoalDto[]>('/sport/goals') }
  createGoal(request: WeeklyGoalRequest): Observable<WeeklyGoalDto> { return this.api.post<WeeklyGoalDto>('/sport/goals', request) }
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
}
