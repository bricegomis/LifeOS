import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { formatBuildInfo, parseApiBuildInfo } from '../buildInfo.ts'

const build = {
  component: 'api',
  buildId: 'abcdef0123456789abcdef0123456789abcdef0123',
  buildNumber: '123',
  runId: '987654321',
  runAttempt: '1',
  repository: 'bricegomis/LifeOS',
  serverUrl: 'https://github.com',
  workflow: 'Build & Push Docker Images',
}

describe('deployed build provenance', () => {
  it('shows the artifact build number and links to the exact workflow attempt', () => {
    const result = formatBuildInfo(parseApiBuildInfo(build))
    assert.equal(result.label, 'Build #123')
    assert.equal(
      result.runUrl,
      'https://github.com/bricegomis/LifeOS/actions/runs/987654321/attempts/1',
    )
    assert.ok(result.details.includes(`Commit ${build.buildId}`))
    assert.ok(result.details.includes('Run 987654321'))
    assert.ok(result.details.includes(build.workflow))
    const retry = formatBuildInfo({ ...build, runAttempt: '2' })
    assert.equal(retry.label, 'Build #123 · tentative 2')
    assert.ok(retry.runUrl?.endsWith('/attempts/2'))
  })

  it('does not conflate workflow-scoped numbers or commits with build identity', () => {
    const pages = formatBuildInfo({ ...build, runId: '111111', workflow: 'Deploy GitHub Pages' })
    const docker = formatBuildInfo(build)
    assert.equal(pages.label, docker.label)
    assert.notEqual(pages.runUrl, docker.runUrl)
    assert.notEqual(pages.details, docker.details)
  })

  it('keeps local and legacy artifacts explicit without inventing a build number', () => {
    assert.equal(formatBuildInfo({ buildId: 'local' }).label, 'Local')
    assert.equal(formatBuildInfo({ buildId: 'dev' }).label, 'Local (développement)')
    const legacy = formatBuildInfo(parseApiBuildInfo({ component: 'api', buildId: build.buildId }))
    assert.equal(legacy.label, 'Build non identifié')
    assert.equal(legacy.runUrl, null)
    assert.ok(legacy.details.includes(build.buildId))
    assert.equal(formatBuildInfo({ ...build, buildNumber: null, runId: null }).runUrl, null)
  })

  it('validates unknown API data and never emits unsafe run links', () => {
    for (const invalid of [
      null,
      {},
      { ...build, component: 'ui' },
      { ...build, buildNumber: 123 },
      { ...build, runId: 'NaN' },
      { ...build, runAttempt: '-1' },
      { ...build, buildId: '' },
    ]) {
      assert.throws(() => parseApiBuildInfo(invalid))
    }
    for (const serverUrl of [
      'not a URL',
      'javascript:alert(1)',
      'https://user@github.com',
      'https://github.com/path',
    ]) {
      assert.equal(formatBuildInfo({ ...build, serverUrl }).runUrl, null)
    }
    assert.equal(formatBuildInfo({ ...build, repository: '../elsewhere' }).runUrl, null)
  })
})
