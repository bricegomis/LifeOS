import { Component, computed, input } from '@angular/core'
import { formatBuildInfo, type BuildInfo } from '../core/build-info'

@Component({
  selector: 'app-build-version',
  standalone: true,
  template: `
    <span>{{ component() }} </span>
    @if (version(); as version) {
      <span>{{ version.label }}</span>
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
