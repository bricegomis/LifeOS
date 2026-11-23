/** Budget API shapes (ADR 0020). Months are "aaaa-mm". */
export type LineKind = 'fixed' | 'envelope' | 'extra' | 'off' | 'income'
export type AmountPeriod = 'month' | 'year' | 'months'

export interface BudgetAmountDto { fromMonth: string, amount: number, period: AmountPeriod, months: number }

export interface BudgetLineDto {
  id: string, name: string, category: string, kind: LineKind, reduce: boolean, fromSupplements: boolean, notes: string | null,
  stoppedFrom: string | null, active: boolean, amount: BudgetAmountDto | null, planned: number, history: BudgetAmountDto[]
}

export interface BudgetLineListDto { month: string, supplementsMonthlyCost: number, categories: string[], lines: BudgetLineDto[] }

export interface BudgetLineRequest {
  name: string, category: string | null, kind: LineKind, reduce: boolean, fromSupplements: boolean, notes: string | null,
  amount: number | null, period: AmountPeriod | null, months: number | null, fromMonth: string | null
}

export interface BudgetOperationDto {
  id: string, date: string, postedOn: string | null, label: string, amount: number, source: 'ofx' | 'manual', account: string | null,
  lineId: string | null, lineName: string | null, spread: number, classifiedBy: 'rule' | 'hand' | 'assistant' | null, ruleId: string | null
}

export interface BudgetAccountDto { account: string, balance: number | null, balanceOn: string | null, lastImportAt: string | null, lastFrom: string | null, lastTo: string | null }

export interface BudgetOperationListDto { month: string, unclassifiedCount: number, total: number, operations: BudgetOperationDto[], accounts: BudgetAccountDto[] }

export interface BudgetImportDto {
  account: string, from: string | null, to: string | null, read: number, duplicates: number, created: number, classified: number, unclassified: number,
  balance: number | null, balanceOn: string | null, operations: BudgetOperationDto[]
}

export type RuleDirection = 'any' | 'out' | 'in'

export interface BudgetRuleDto { id: string, position: number, text: string, direction: RuleDirection, lineId: string, lineName: string, spread: number, matches: number }
export interface BudgetRuleResultDto { rule: BudgetRuleDto, classified: number }
export interface BudgetRuleRequest { text: string, lineId: string, direction: RuleDirection, spread: number, position: number | null }

export interface BudgetClassifyResultDto { operation: BudgetOperationDto, rule: BudgetRuleDto | null, classified: number }

export interface BudgetMonthLineDto { line: BudgetLineDto, planned: number, real: number, gap: number, over: boolean, operations: number }
export interface BudgetMonthCategoryDto { category: string, planned: number, real: number, lines: BudgetMonthLineDto[] }

export interface BudgetMonthDto {
  month: string, status: 'past' | 'current' | 'future', plannedIncome: number, received: number, plannedExpenses: number, spent: number,
  plannedRest: number, rest: number, extras: number, unclassifiedCount: number, unclassifiedTotal: number, off: number, supplementsMonthlyCost: number,
  envelopes: BudgetMonthLineDto[], categories: BudgetMonthCategoryDto[], incomes: BudgetMonthLineDto[], offLines: BudgetMonthLineDto[]
}

export interface BudgetTrendMonthDto { month: string, spent: number, planned: number, extras: number, received: number, byCategory: Record<string, number> }
export interface BudgetTrendLineDto { lineId: string, name: string, values: number[] }
export interface BudgetTrendDto { months: BudgetTrendMonthDto[], reduce: BudgetTrendLineDto[] }

export interface BudgetLineMonthDto { month: string, planned: number, real: number }
export interface BudgetLineDetailDto { line: BudgetLineDto, months: BudgetLineMonthDto[], rules: BudgetRuleDto[] }
