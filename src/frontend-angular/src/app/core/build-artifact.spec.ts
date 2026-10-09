import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { runInNewContext } from 'node:vm'

it('embeds CI provenance separately from replaceable deployment config, with honest local defaults', () => {
  const cwd = mkdtempSync(join(tmpdir(), 'lifeos-build-'))
  const script = fileURLToPath(new URL('../../../scripts/generate-config.mjs', import.meta.url))
  try {
    const env = {
      PATH: process.env.PATH,
      LIFEOS_BUILD_ID: 'abcdef123456',
      LIFEOS_BUILD_NUMBER: '123',
      LIFEOS_RUN_ID: '987654321',
      LIFEOS_RUN_ATTEMPT: '2',
      LIFEOS_BUILD_REPOSITORY: 'bricegomis/LifeOS',
      LIFEOS_BUILD_SERVER_URL: 'https://github.com',
      LIFEOS_BUILD_WORKFLOW: 'Build & Push Docker Images',
      VITE_LIFEOS_API_URL: 'https://api.example.test',
    }
    execFileSync(process.execPath, [script], { cwd, env })
    const readBuild = (): Record<string, unknown> =>
      JSON.parse(
        readFileSync(join(cwd, 'src/environments/build-info.generated.ts'), 'utf8')
          .replace(/^export const buildInfo = /, '')
          .replace(/;\s*$/, ''),
      )
    const window: Record<string, Record<string, unknown>> = {}
    runInNewContext(readFileSync(join(cwd, 'public/config.js'), 'utf8'), { window })
    const build = readBuild()
    assert.equal(build.buildNumber, '123')
    assert.equal(build.runId, '987654321')
    assert.equal(build.runAttempt, '2')
    assert.equal(build.buildId, 'abcdef123456')
    assert.equal(build.workflow, env.LIFEOS_BUILD_WORKFLOW)
    assert.equal(window.LIFEOS_CONFIG?.apiBaseUrl, env.VITE_LIFEOS_API_URL)
    assert.equal(window.LIFEOS_CONFIG?.buildId, undefined)
    execFileSync(process.execPath, [script], { cwd, env: { PATH: process.env.PATH } })
    const local = readBuild()
    assert.equal(local.buildId, 'local')
    assert.equal(local.buildNumber, null)
    assert.equal(local.runId, null)
  } finally {
    rmSync(cwd, { recursive: true })
  }
})
