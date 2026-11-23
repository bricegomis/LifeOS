import { inject, Injectable } from '@angular/core'
import type { Observable } from 'rxjs'
import { LifeosApiService } from '@/app/core/api/lifeos-api.service'
import type {
  BudgetClassifyResultDto, BudgetImportDto, BudgetLineDetailDto, BudgetLineDto, BudgetLineListDto, BudgetLineRequest, BudgetMonthDto,
  BudgetOperationDto, BudgetOperationListDto, BudgetRuleDto, BudgetRuleRequest, BudgetRuleResultDto, BudgetTrendDto,
} from './budget.models'

const id = (value: string) => encodeURIComponent(value)

/** Every HTTP call of the Budget screens. The API only ever returns the caller's own budget. */
@Injectable({ providedIn: 'root' })
export class BudgetApiService {
  private readonly api = inject(LifeosApiService)

  month(month: string): Observable<BudgetMonthDto> { return this.api.get<BudgetMonthDto>(`/budget/months/${id(month)}`) }
  trend(months: number, until: string | null = null): Observable<BudgetTrendDto> { return this.api.get<BudgetTrendDto>('/budget/trend', { months, until }) }

  lines(includeStopped = false): Observable<BudgetLineListDto> { return this.api.get<BudgetLineListDto>('/budget/lines', { includeStopped }) }
  line(lineId: string): Observable<BudgetLineDetailDto> { return this.api.get<BudgetLineDetailDto>(`/budget/lines/${id(lineId)}`) }
  createLine(request: BudgetLineRequest): Observable<BudgetLineDto> { return this.api.post<BudgetLineDto>('/budget/lines', request) }
  updateLine(lineId: string, request: BudgetLineRequest): Observable<BudgetLineDto> { return this.api.put<BudgetLineDto>(`/budget/lines/${id(lineId)}`, request) }
  stopLine(lineId: string, fromMonth: string | null): Observable<BudgetLineDto> { return this.api.post<BudgetLineDto>(`/budget/lines/${id(lineId)}/stop`, { fromMonth }) }
  resumeLine(lineId: string): Observable<BudgetLineDto> { return this.api.post<BudgetLineDto>(`/budget/lines/${id(lineId)}/resume`, {}) }
  deleteLine(lineId: string): Observable<void> { return this.api.delete(`/budget/lines/${id(lineId)}`) }

  operations(month: string, filter: 'all' | 'unclassified' | 'spread' = 'all', q = ''): Observable<BudgetOperationListDto> {
    return this.api.get<BudgetOperationListDto>('/budget/operations', { month, filter, q: q.trim() || undefined })
  }
  addOperation(request: { date: string, label: string, amount: number, lineId: string | null, spread: number }): Observable<BudgetOperationDto> {
    return this.api.post<BudgetOperationDto>('/budget/operations', request)
  }
  classify(operationId: string, lineId: string | null, spread: number, ruleText: string | null): Observable<BudgetClassifyResultDto> {
    return this.api.put<BudgetClassifyResultDto>(`/budget/operations/${id(operationId)}/classification`, { lineId, spread, ruleText })
  }
  deleteOperation(operationId: string): Observable<void> { return this.api.delete(`/budget/operations/${id(operationId)}`) }
  importStatement(file: File): Observable<BudgetImportDto> {
    const form = new FormData()
    form.append('file', file)
    return this.api.post<BudgetImportDto>('/budget/imports', form)
  }

  rules(): Observable<BudgetRuleDto[]> { return this.api.get<BudgetRuleDto[]>('/budget/rules') }
  createRule(request: BudgetRuleRequest): Observable<BudgetRuleResultDto> { return this.api.post<BudgetRuleResultDto>('/budget/rules', request) }
  updateRule(ruleId: string, request: BudgetRuleRequest): Observable<BudgetRuleResultDto> { return this.api.put<BudgetRuleResultDto>(`/budget/rules/${id(ruleId)}`, request) }
  moveRule(ruleId: string, position: number): Observable<BudgetRuleDto[]> { return this.api.post<BudgetRuleDto[]>(`/budget/rules/${id(ruleId)}/move`, { position }) }
  deleteRule(ruleId: string): Observable<void> { return this.api.delete(`/budget/rules/${id(ruleId)}`) }
}
