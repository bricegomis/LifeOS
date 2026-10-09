import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { formatBuildInfo, parseApiBuildInfo } from './build-info.ts'

const build = {
  component: 'api',
  buildId: 'abcdef0123456789abcdef0123456789abcdef0123',
  versionMajor: '1',
  buildNumber: '123',
  runId: '987654321',
  runAttempt: '1',
  repository: 'bricegomis/LifeOS',
  serverUrl: 'https://github.com',
  workflow: 'Build & Push Docker Images',
}

describe('deployed build provenance', () => {
  it('shows only the manually selected major and artifact build number', () => {
    const result = formatBuildInfo(parseApiBuildInfo(build))
    assert.equal(result.label, '1.123')
    const retry = formatBuildInfo({ ...build, runAttempt: '2' })
    assert.equal(retry.label, '1.123')
  })

  it('keeps the build number visible when a legacy API has no major version', () => {
    assert.equal(formatBuildInfo({ ...build, versionMajor: null }).label, 'Build #123')
  })

  it('keeps local and legacy artifacts explicit without inventing a build number', () => {
    assert.equal(formatBuildInfo({ buildId: 'local' }).label, 'Local')
    assert.equal(formatBuildInfo({ buildId: 'dev' }).label, 'Local (développement)')
    const legacy = formatBuildInfo(parseApiBuildInfo({ component: 'api', buildId: build.buildId }))
    assert.equal(legacy.label, 'Build non identifié')
    assert.equal(formatBuildInfo({ ...build, buildNumber: null, runId: null }).label, 'Build non identifié')
  })

  it('validates unknown API data and version numbers', () => {
    for (const invalid of [
      null,
      {},
      { ...build, component: 'ui' },
      { ...build, versionMajor: '0' },
      { ...build, versionMajor: '1.2' },
      { ...build, buildNumber: 123 },
      { ...build, runId: 'NaN' },
      { ...build, runAttempt: '-1' },
      { ...build, buildId: '' },
    ]) {
      assert.throws(() => parseApiBuildInfo(invalid))
    }
  })
})
