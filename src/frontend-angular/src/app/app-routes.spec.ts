import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

// inject() only works synchronously: after an await the injection context is gone and the
// guard throws (NG0203), which left a blank page right after signing in.
describe('route guards', () => {
  it('call inject() before their first await', () => {
    const source = readFileSync(new URL('./app.routes.ts', import.meta.url), 'utf8')
    for (const guard of source.split(/\n(?=const \w+: Can\w+Fn)/).slice(1)) {
      const body = guard.split(/\n}\n/)[0]
      const firstAwait = body.indexOf('await ')
      if (firstAwait < 0) continue
      assert.equal(body.slice(firstAwait).includes('inject('), false, body)
    }
  })
})
