import assert from 'node:assert/strict'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, it } from 'node:test'

// ADR 0009: a feature module (app/<module>/) imports only core and shared, never another
// module. `household` is the core household screen. Add a module here when its folder is created.
const MODULES = ['food', 'sports', 'household']
const appDir = new URL('.', import.meta.url).pathname

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap(name => {
    const path = join(dir, name)
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.ts$/.test(name) ? [path] : []
  })
}

function importsOf(file: string): string[] {
  return [...readFileSync(file, 'utf8').matchAll(/(?:from|import\()\s*['"]([^'"]+)['"]/g)].map(match => match[1]!)
}

describe('frontend module boundaries', () => {
  for (const module of MODULES) {
    it(`${module} imports only itself, core and shared`, () => {
      const files = sourceFiles(join(appDir, module))
      assert.ok(files.length > 0, `no source file found for module ${module}`)
      const violations = files.flatMap(file => importsOf(file)
        .filter(target => target.startsWith('@/app/'))
        .filter(target => !['core', 'shared', module].includes(target.split('/')[2] ?? ''))
        .map(target => `${relative(appDir, file)} -> ${target}`))
      assert.deepEqual(violations, [])
    })
  }

  it('core and shared never import a feature module', () => {
    const violations = ['core', 'shared'].flatMap(layer => sourceFiles(join(appDir, layer)).flatMap(file => importsOf(file)
      .filter(target => MODULES.some(module => target.startsWith(`@/app/${module}/`)))
      .map(target => `${relative(appDir, file)} -> ${target}`)))
    assert.deepEqual(violations, [])
  })
})
