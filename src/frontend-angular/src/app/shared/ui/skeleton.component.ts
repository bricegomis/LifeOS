import { Component, computed, input } from '@angular/core'

/**
 * Loading placeholder (ADR 0016): grey shapes the size of what is coming, so the page does not jump
 * when the data arrives. `list` mimics a `lo-list` of rows, `page` a detail page (title and panels).
 */
@Component({
  selector: 'app-skeleton',
  standalone: true,
  template: `
    <div class="skeleton" role="status" aria-label="Chargement" [attr.aria-busy]="true">
      @if (variant() === 'page') {
        <span class="bone title"></span>
        <span class="bone line short"></span>
        <span class="bone panel"></span>
        <span class="bone panel low"></span>
      } @else {
        <div class="rows">
          @for (row of rowList(); track row) {
            <div class="row"><span class="bone lead"></span><span class="text"><span class="bone line"></span><span class="bone line short"></span></span></div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .skeleton { display: flex; flex-direction: column; gap: 12px; }
    .rows { overflow: hidden; border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; }
    .row { display: flex; min-height: 64px; align-items: center; gap: 16px; padding: 8px 16px; }
    .row + .row { border-top: 1px solid var(--mat-sys-outline-variant); }
    .text { display: flex; flex: 1 1 auto; flex-direction: column; gap: 8px; }
    .bone { display: block; border-radius: 6px; background: var(--mat-sys-surface-container-high); animation: pulse 1.4s ease-in-out infinite; }
    .lead { width: 40px; height: 40px; flex: 0 0 40px; border-radius: 20px; }
    .line { width: 60%; height: 12px; }
    .line.short { width: 35%; }
    .title { width: 45%; height: 28px; }
    .panel { height: 160px; border-radius: 12px; }
    .panel.low { height: 96px; }
    @keyframes pulse { 50% { opacity: .55; } }
    @media (prefers-reduced-motion: reduce) { .bone { animation: none; } }
  `],
})
export class SkeletonComponent {
  readonly variant = input<'list' | 'page'>('list')
  readonly rows = input(4)
  readonly rowList = computed(() => Array.from({ length: this.rows() }, (_, index) => index))
}
