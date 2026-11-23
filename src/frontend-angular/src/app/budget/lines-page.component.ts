import { Component, computed, inject, OnInit, signal } from '@angular/core'
import { MatDialog } from '@angular/material/dialog'
import { MatIconModule } from '@angular/material/icon'
import { MatSnackBar } from '@angular/material/snack-bar'
import { RouterLink } from '@angular/router'
import { firstValueFrom } from 'rxjs'
import { apiErrorMessage } from '@/app/core/api/lifeos-api.service'
import { openEditor } from '@/app/shared/ui/editor-dialog'
import { ListPageComponent, matchesQuery } from '@/app/shared/ui/list-page.component'
import { BudgetApiService } from './budget-api.service'
import { amountLabel, euro, kindOf, monthLabel } from './budget-rules'
import type { BudgetLineDto, BudgetLineListDto } from './budget.models'
import { LineDialogComponent, type LineDialogData, type LineDialogResult } from './line-dialog.component'

/** The planned lines (ADR 0020) by poste, then incomes and off-budget lines; stopped ones on demand. */
@Component({
  selector: 'app-budget-lines-page',
  standalone: true,
  imports: [ListPageComponent, MatIconModule, RouterLink],
  template: `
    <app-list-page heading="Lignes prévues" addLabel="Nouvelle ligne" archivedLabel="Afficher les arrêtées" [(query)]="query" [(showArchived)]="showStopped" (add)="add()"
      intro="Le prévu de chaque mois. Un changement de montant vaut à partir du mois choisi ; les mois passés gardent leur prévu.">
      @if (error()) { <p class="lo-error" role="alert">{{ error() }}</p> }
      @if (list(); as l) {
        @for (g of groups(); track g.name) {
          <section class="lo-section">
            <div class="lo-section-head"><h2>{{ g.name }}</h2>@if (g.planned) { <span class="lo-muted">{{ euro(g.planned) }} par mois</span> }</div>
            <div class="lo-list">
              @for (line of g.lines; track line.id) {
                <div class="lo-row">
                  <a class="lo-row-main" [routerLink]="['/budget/line', line.id]">
                    <span class="lo-lead-icon"><mat-icon [svgIcon]="kindOf(line.kind).icon" /></span>
                    <span class="lo-row-text">
                      <span class="lo-row-title">{{ line.name }}</span>
                      <span class="lo-row-sub">{{ amountLabel(line) }}{{ line.reduce ? ' · à diminuer' : '' }}{{ line.stoppedFrom ? ' · arrêtée depuis ' + monthLabel(line.stoppedFrom).toLowerCase() : '' }}</span>
                    </span>
                    @if (line.kind !== 'extra' && line.kind !== 'off') { <strong class="amount">{{ euro(line.planned) }}</strong> }
                  </a>
                </div>
              }
            </div>
          </section>
        } @empty {
          <p class="lo-empty">{{ l.lines.length ? 'Aucun résultat.' : 'Aucune ligne. Ajoute « Courses » en enveloppe, ou demande à Claude de reprendre ton fichier Excel.' }}</p>
        }
      }
    </app-list-page>
  `,
  styles: [`a.lo-row-main { text-decoration: none; } .amount { flex: 0 0 auto; }`],
})
export class BudgetLinesPageComponent implements OnInit {
  private readonly api = inject(BudgetApiService)
  private readonly dialog = inject(MatDialog)
  private readonly snack = inject(MatSnackBar)
  readonly query = signal('')
  readonly showStopped = signal(false)
  readonly list = signal<BudgetLineListDto | null>(null)
  readonly error = signal('')
  readonly euro = euro
  readonly kindOf = kindOf
  readonly amountLabel = amountLabel
  readonly monthLabel = monthLabel
  readonly groups = computed(() => {
    const lines = (this.list()?.lines ?? []).filter((l: BudgetLineDto) => (this.showStopped() || l.active)
      && (matchesQuery(l.name, this.query()) || matchesQuery(l.category, this.query())))
    const names = [...new Set(lines.map(l => l.category))]
    return names.map(name => {
      const ls = lines.filter(l => l.category === name)
      return { name, lines: ls, planned: ls.filter(l => l.kind !== 'income').reduce((t, l) => t + (l.active ? l.planned : 0), 0) }
    })
  })

  async ngOnInit(): Promise<void> { await this.load() }

  async add(): Promise<void> {
    const l = this.list()
    const data: LineDialogData = { line: null, categories: l?.categories ?? [], currentMonth: l?.month ?? new Date().toISOString().slice(0, 7),
      supplementsMonthlyCost: l?.supplementsMonthlyCost ?? 0 }
    const result = await firstValueFrom(openEditor<LineDialogComponent, LineDialogData, LineDialogResult>(this.dialog, LineDialogComponent, data).afterClosed())
    if (!result) return
    this.snack.open(result.message, undefined, { duration: 4000 })
    await this.load()
  }

  private async load(): Promise<void> {
    try { this.list.set(await firstValueFrom(this.api.lines(true))); this.error.set('') }
    catch (error) { this.error.set(apiErrorMessage(error)) }
  }
}
