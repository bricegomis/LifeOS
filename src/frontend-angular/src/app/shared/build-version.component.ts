import { Component, computed, input } from '@angular/core'
import { formatBuildInfo, type BuildInfo } from '../core/build-info'

@Component({
  selector: 'app-build-version',
  standalone: true,
  template: `
    <span>{{ component() }} </span>
    @if (version(); as version) {
      @if (version.runUrl) {
        <a
          [href]="version.runUrl"
          target="_blank"
          rel="noopener noreferrer"
          [attr.aria-label]="component() + ' ' + version.label + ' — ouvrir le run GitHub Actions'"
          >{{ version.label }}</a
        >
      } @else {
        <span>{{ version.label }}</span>
      }
      @if (version.details) {
        <details>
          <summary>Détails {{ component() }}</summary>
          <span>{{ version.details }}</span>
        </details>
      }
    } @else {
      <span>{{ status() }}</span>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        overflow-wrap: anywhere;
      }
      a {
        color: inherit;
        text-decoration: underline;
      }
      details {
        margin-top: 0.2rem;
      }
      summary {
        cursor: pointer;
      }
    `,
  ],
})
export class BuildVersionComponent {
  readonly component = input.required<string>()
  readonly info = input<BuildInfo | null>(null)
  readonly status = input('')
  readonly version = computed(() => {
    const info = this.info()
    return info ? formatBuildInfo(info) : null
  })
}
