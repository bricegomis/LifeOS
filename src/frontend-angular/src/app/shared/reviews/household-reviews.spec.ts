import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import type { ReviewDto } from '../../core/api/api.models.ts'
import type { HouseholdReviewsComponent } from './household-reviews.component.ts'
import type { ReviewDraftsService } from './review-drafts.service.ts'
import { formSnapshot, hasChanges } from '../dialog/dialog-guard.ts'

function signal<T>(initial: T) {
  let value = initial
  return Object.assign(() => value, { set: (next: T) => { value = next } })
}

/** Sets a signal input as the parent template would. */
function setInput<T>(input: () => T, value: T): void {
  (input as unknown as { set(next: T): void }).set(value)
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}

function harness() {
  const calls: { method: string; path: string; body?: unknown }[] = []
  const review: ReviewDto = {
    id: 'review-1', memberProfileId: 'profile-1',
    profileName: 'Test', score: 0, comment: 'Déjà enregistré', createdByUserId: 'user-1',
    createdAt: '', updatedAt: '',
  }
  let rows = [review]
  let failure = ''
  let hold: Promise<unknown> | null = null
  let confirmAnswer = true
  let confirmations = 0
  const loadedReviews: ReviewDto[][] = []
  const onDestroy: (() => void)[] = []
  const windows = { opened: 0, open: 0 }
  const dialogToken = {}
  /** Stands for MatDialog through openEditor: closing the window runs its afterClosed subscribers. */
  const openEditor = () => {
    windows.opened++
    windows.open++
    const closed: (() => void)[] = []
    let isOpen = true
    return {
      afterClosed: () => ({ subscribe: (callback: () => void) => { closed.push(callback) } }),
      close: () => { if (!isOpen) return; isOpen = false; windows.open--; closed.forEach(callback => callback()) },
    }
  }
  const api = Object.fromEntries(['get', 'post', 'put', 'delete'].map(method => [
    method, async (path: string, body?: unknown) => {
      calls.push({ method, path, body })
      if (hold) await hold
      if (failure) throw new Error(failure)
      if (method === 'post') rows = [...rows, { ...review, id: 'review-2', ...(body as object) }]
      if (method === 'put') rows = rows.map(row => ({ ...row, ...(body as object) }))
      if (method === 'delete') rows = []
      return rows
    },
  ]))
  const noopDecorator = () => () => {}
  const destroyRef = { onDestroy: (callback: () => void) => { onDestroy.push(callback) } }
  const reviewDraftsToken = {}
  const input = Object.assign(signal, { required: () => signal('') })
  const imports: Record<string, unknown> = {
    '@angular/core': {
      Component: noopDecorator, Injectable: noopDecorator, Input: noopDecorator, HostListener: noopDecorator,
      DestroyRef: destroyRef, signal, input,
      output: () => ({ emit: (reviews?: ReviewDto[]) => { if (reviews) loadedReviews.push(reviews) } }),
      inject: (token: unknown) => token === destroyRef ? destroyRef : token === reviewDraftsToken ? drafts : token === dialogToken ? {} : api,
    },
    '@angular/material/button': { MatButtonModule: {} },
    '@angular/material/dialog': { MatDialog: dialogToken },
    '@angular/material/icon': { MatIconModule: {} },
    '@/app/shared/ui/editor-dialog': { openEditor },
    './review-editor-dialog.component': { ReviewEditorDialogComponent: {} },
    'rxjs': { firstValueFrom: (value: unknown) => Promise.resolve(value) },
    '@/app/core/api/lifeos-api.service': { LifeosApiService: {}, apiErrorMessage: (error: Error) => error.message },
    '@/app/shared/dialog/dialog-guard': { formSnapshot, hasChanges },
    './review-drafts.service': { ReviewDraftsService: reviewDraftsToken },
  }
  function load<T>(path: string, name: string): T {
    const source = readFileSync(new URL(path, import.meta.url), 'utf8')
    const js = ts.transpileModule(source, { compilerOptions: {
      module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true,
    } }).outputText
    const exports: Record<string, new () => T> = {}
    runInNewContext(js, {
      exports, require: (id: string) => { assert.ok(id in imports, `Missing port ${id}`); return imports[id] },
      window: { confirm: () => { confirmations++; return confirmAnswer } },
      console,
    })
    return new exports[name]!()
  }
  const drafts = load<ReviewDraftsService>('./review-drafts.service.ts', 'ReviewDraftsService')
  const component = load<HouseholdReviewsComponent>('./household-reviews.component.ts', 'HouseholdReviewsComponent')
  setInput(component.targetType, 'food')
  setInput(component.targetId, 'target/1')
  return {
    component, drafts, review, calls, windows, loadedReviews,
    fail: (message: string) => { failure = message },
    hold: (promise: Promise<unknown> | null) => { hold = promise },
    confirm: (answer: boolean) => { confirmAnswer = answer },
    confirmations: () => confirmations,
    destroy: () => onDestroy.forEach(callback => callback()),
  }
}

describe('shared household review window', () => {
  it('opens one window per draft and drops the draft when the window is closed', async () => {
    const { component, windows } = harness()
    component.add()
    assert.equal(windows.open, 1)
    component.add()
    assert.equal(windows.opened, 1, 'A second window never opens over a draft')
    component.comment = 'Brouillon'
    component.cancelEdit()
    assert.equal(windows.open, 0)
    assert.equal(component.editorOpen(), false)
    assert.equal(component.comment, '')
  })

  it('opens clean creation and editing drafts without removing existing reviews', async () => {
    const { component, review } = harness()
    await component.load()
    component.add()
    assert.equal(component.editorOpen(), true)
    assert.equal(component.dirty(), false)
    assert.equal(component.reviews().length, 1)
    component.comment = 'Brouillon'
    component.edit(review)
    assert.equal(component.comment, 'Brouillon', 'An open draft cannot be overwritten')
    component.cancelEdit()
    component.edit(review)
    assert.equal(component.editingId(), review.id)
    assert.equal(component.score, 0)
    assert.equal(component.target, 'profile-1')
    assert.equal(component.dirty(), false)
    component.cancelEdit()
    assert.equal(component.editorOpen(), false)
    assert.equal(component.dirty(), false)
  })

  for (const [targetType, segment] of [['food', 'foods'], ['recipe', 'recipes']] as const) {
    it(`saves and refreshes ${targetType} reviews in the original encoded scope, including zero`, async () => {
      const { component, calls, loadedReviews } = harness()
      setInput(component.targetType, targetType)
      component.add()
      component.target = 'profile-2'
      component.score = 0
      await component.save()
      assert.equal(calls[0]?.path, `/reviews/${segment}/target%2F1`)
      assert.equal(JSON.stringify(calls[0]?.body), JSON.stringify({
        memberProfileId: 'profile-2', score: 0, comment: null,
      }))
      assert.equal(calls[1]?.method, 'get')
      assert.equal(component.reviews().length, 2)
      assert.equal(loadedReviews.length, 1)
      assert.equal(loadedReviews[0]?.length, 2)
      assert.equal(loadedReviews[0]?.[1]?.score, 0)
      assert.equal(component.editorOpen(), false)
      assert.equal(component.dirty(), false)
    })
  }

  it('keeps failed saves editable with a visible error and blocks empty/invalid payloads', async () => {
    const { component, calls, fail } = harness()
    component.add()
    await component.save()
    assert.equal(calls.length, 0)
    assert.ok(component.error())
    component.target = 'profile-1'
    component.score = 10.5
    await component.save()
    assert.equal(calls.length, 0)
    component.score = null
    component.comment = ' À conserver '
    fail('Erreur réseau')
    await component.save()
    assert.equal(component.error(), 'Erreur réseau')
    assert.equal(component.comment, ' À conserver ')
    assert.equal(component.editorOpen(), true)
    assert.equal(component.busy(), false)
  })

  it('updates only score/comment, then preserves deletion and its confirmation', async () => {
    const { component, review, calls, confirm } = harness()
    component.edit(review)
    component.comment = ' Modifié '
    await component.save()
    assert.equal(calls[0]?.method, 'put')
    assert.equal(calls[0]?.path, '/reviews/foods/target%2F1/review-1')
    assert.equal(JSON.stringify(calls[0]?.body), '{"score":0,"comment":"Modifié"}')
    assert.equal(component.reviews()[0]?.comment, 'Modifié')
    confirm(false)
    await component.remove(review)
    assert.equal(calls.length, 2)
    confirm(true)
    await component.remove(review)
    assert.equal(calls[2]?.method, 'delete')
    assert.equal(component.reviews().length, 0)
  })

  it('protects pending saves against duplicate requests, navigation and reload', async () => {
    const { component, drafts, hold, calls } = harness()
    const pending = deferred<unknown>()
    component.add()
    component.target = 'profile-1'
    component.comment = 'Saisie'
    hold(pending.promise)
    const saving = component.save()
    assert.equal(component.busy(), true)
    assert.equal(drafts.canLeave(), false)
    await component.save()
    component.edit({ id: 'other' } as ReviewDto)
    assert.equal(calls.length, 1)
    assert.equal(component.editingId(), '')
    let prevented = false
    component.beforeUnload({ preventDefault: () => { prevented = true }, returnValue: '' } as BeforeUnloadEvent)
    assert.equal(prevented, true)
    hold(null)
    pending.resolve(null)
    await saving
    assert.equal(component.busy(), false)
    assert.equal(component.editorOpen(), false)
    assert.equal(drafts.canLeave(), true)
  })

  it('keeps the popup open through list refresh and exposes refresh failures after a persisted save', async () => {
    const { component, hold, calls, fail } = harness()
    component.add()
    component.target = 'profile-1'
    component.score = 5
    const pending = deferred<unknown>()
    const saving = component.save()
    hold(pending.promise)
    await new Promise(resolve => setImmediate(resolve))
    assert.equal(calls[1]?.method, 'get')
    assert.equal(component.editorOpen(), true)
    assert.equal(component.busy(), true)
    fail('Lecture indisponible')
    hold(null)
    pending.resolve(null)
    await saving
    assert.equal(component.editorOpen(), false)
    assert.equal(component.busy(), false)
    assert.equal(component.dirty(), false)
    assert.equal(component.error(), 'Lecture indisponible')
  })

  it('guards browser navigation in every host and unregisters destroyed components', () => {
    const { component, drafts, confirm, destroy, confirmations } = harness()
    component.add()
    assert.equal(drafts.canLeave(), true)
    component.comment = 'Brouillon'
    confirm(false)
    assert.equal(drafts.canLeave(), false)
    assert.equal(confirmations(), 1)
    confirm(true)
    assert.equal(drafts.canLeave(), true)
    destroy()
    confirm(false)
    assert.equal(drafts.canLeave(), true)
  })

  it('ignores stale list responses after a target change', async () => {
    const { component, hold } = harness()
    const pending = deferred<unknown>()
    hold(pending.promise)
    const loading = component.load()
    hold(null)
    setInput(component.targetId, '')
    await component.load()
    pending.resolve(null)
    await loading
    assert.equal(component.reviews().length, 0)
    assert.equal(component.loading(), false)
  })
})
