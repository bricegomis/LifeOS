import assert from 'node:assert/strict'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, it } from 'node:test'

// ADR 0016: every screen is built with Angular Material and the LifeOS components of shared/ui,
// with colours from the theme (`--mat-sys-*`). This test keeps new screens on that path.
const appDir = new URL('.', import.meta.url).pathname
const srcDir = join(appDir, '..')

function files(dir: string): string[] {
  return readdirSync(dir).flatMap(name => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return files(path)
    return /\.(ts|html|css|scss)$/.test(name) && !name.endsWith('.spec.ts') ? [path] : []
  })
}

const sources = [...files(appDir), ...files(srcDir).filter(path => !path.startsWith(appDir))]
  .map(path => ({ path: relative(srcDir, path), text: readFileSync(path, 'utf8') }))

function offenders(pattern: RegExp, allowed: string[] = []): string[] {
  return sources.filter(file => !allowed.includes(file.path))
    .flatMap(file => [...file.text.matchAll(pattern)].map(match => `${file.path}: ${match[0].trim()}`))
}

describe('UI kit (ADR 0016)', () => {
  it('uses Material form fields instead of native selects, inputs and text areas', () => {
    // A file picker stays a hidden native input opened by a Material button; the library search bar is the
    // one LifeOS search field (app-list-page).
    const nativeFields = offenders(/<(select|textarea)\b(?![^>]*\bmatInput\b)[^>]*>|<input\b(?![^>]*\bmatInput\b)(?![^>]*type="file")[^>]*>/g,
      ['app/shared/ui/list-page.component.ts'])
    assert.deepEqual(nativeFields, [])
  })

  it('uses Material Symbols, not PrimeIcons', () => {
    assert.deepEqual(offenders(/\bpi pi-[\w-]+|primeicons/g), [])
  })

  it('takes colours from the Material theme, not from the former LifeOS tokens or fixed values', () => {
    // The theme files define the palette; picture placeholders keep fixed warm tints; the canvas used to
    // shrink photos paints white.
    const colours = offenders(/var\(--lifeos-[\w-]+\)|#[0-9a-fA-F]{3,8}\b(?![\w-])|rgba?\([^)]*\)/g,
      ['theme.scss', '_theme-colors.scss', 'app/food/shared/tint.ts', 'app/shared/image-reduction.ts'])
    assert.deepEqual(colours, [])
  })

  it('no longer uses the former shared classes and dialog', () => {
    assert.deepEqual(offenders(/\b(lifeos-button[\w-]*|lifeos-input|text-input|app-lifeos-dialog|dialogExtraActions)\b/g), [])
  })
})
