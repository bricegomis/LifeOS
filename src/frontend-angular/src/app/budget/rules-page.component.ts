import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatButtonModule } from '@angular/material/button'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { BudgetApiService } from './budget-api.service'
import type { BudgetLineDto, BudgetRuleDto } from './budget.models'
import { RuleDialogComponent, type RuleDialogData, type RuleDialogResult } from './rule-dialog.component'

/** Classification rules in their order (ADR 0020): the first that matches classifies an imported operation. */
@Component({
  selector: 'app-budget-rules-page',
  standalone: true,
  imports: [ListPageComponent, MatButtonModule, MatIconModule],
  template: `
    <app-list-page heading="Règles de classement" addLabel="Nouvelle règle" [(query)]="query" (add)="edit(null)"
      intro="Appliquées à chaque import, dans l’ordre : la première qui correspond range l’opération. Le reste attend d’être rangé, par toi ou par Claude.">
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      <p class="lo-notice">L’ordre compte : une règle « CSE YOUNUP » doit passer avant « YOUNUP », sinon la prime du CSE serait rangée en salaire.</p>
      @if (shown().length) {
        <div class="lo-list">
          @for (r of shown(); track r.id) {
            <div class="lo-row">
              <button type="button" class="lo-row-main" (click)="edit(r)">
                <span class="lo-lead-icon"><mat-icon svgIcon="rule" /></span>
                <span class="lo-row-text">
                  <span class="lo-row-title">{{ r.position + 1 }}. Libellé contient « {{ r.text }} »</span>
                  <span class="lo-row-sub">→ {{ r.lineName }}{{ r.direction === 'in' ? ' · revenus seulement' : r.direction === 'out' ? ' · dépenses seulement' : '' }}{{ r.spread > 1 ? ' · étalée sur ' + r.spread + ' mois' : '' }} · {{ r.matches }} op.</span>
                </span>
              </button>
              <span class="lo-row-action">
                <button mat-icon-button type="button" [disabled]="busy() || r.position === 0" (click)="move(r, -1)" [attr.aria-label]="'Monter la règle ' + r.text" title="Monter"><mat-icon svgIcon="arrow_upward" /></button>
                <button mat-icon-button type="button" [disabled]="busy() || r.position === rules().length - 1" (click)="move(r, 1)" [attr.aria-label]="'Descendre la règle ' + r.text" title="Descendre"><mat-icon svgIcon="arrow_downward" /></button>
              </span>
            </div>
          }
        </div>
      } @else {
        <p class="lo-empty">{{ rules().length ? 'Aucun résultat.' : 'Aucune règle. Range une opération en cochant « Créer une règle », ou ajoute-en une ici.' }}</p>
      }
    </app-list-page>
  `,
})
export class BudgetRulesPageComponent implements OnInit {
  private readonly api = inject(BudgetApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly rules = signal<BudgetRuleDto[]>([])
  readonly busy = signal(false)
  readonly error = signal('')
  readonly shown = computed(() => this.rules().filter(r => matchesQuery(r.text, this.query()) || matchesQuery(r.lineName, this.query())))
  private lines: BudgetLineDto[] = []

  async ngOnInit(): Promise<void> {
    await this.load()
    try { this.lines = (await firstValueFrom(this.api.lines(true))).lines } catch { this.lines = [] }
  }

  async edit(rule: BudgetRuleDto | null): Promise<void> {
    const result = await firstValueFrom(openEditor<RuleDialogComponent, RuleDialogData, RuleDialogResult>(this.dialog, RuleDialogComponent, { rule, lines: this.lines, lineId: null }).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  async move(rule: BudgetRuleDto, step: number): Promise<void> {
    this.busy.set(true)
    try { this.rules.set(await firstValueFrom(this.api.moveRule(rule.id, rule.position + step))) }
    catch (error) { this.error.set(apiErrorMessage(error)) }
    finally { this.busy.set(false) }
  }

  private async load(): Promise<void> {
    try { this.rules.set(await firstValueFrom(this.api.rules())); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
