import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import type { ReviewDto } from '../../core/api/api.models.ts'
import type { HouseholdReviewsComponent } from './household-reviews.component.ts'
import type { ReviewDraftsService } from './review-drafts.service.ts'
import type { LifeosDialogComponent } from '../dialog/lifeos-dialog.component.ts'
import { dismissDecision, focusableFieldSelector, formSnapshot, hasChanges } from '../dialog/dialog-guard.ts'

function signal<T>(initial: T) {
  let value = initial
  return Object.assign(() => value, { set: (next: T) => { value = next } })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}

function harness() {
  const calls: { method: string; path: string; body?: unknown }[] = []
  const review: ReviewDto = {
    id: 'review-1', memberProfileId: 'profile-1', householdGroupId: null,
    targetName: 'Test', score: 0, comment: 'Déjà enregistré', createdByUserId: 'user-1',
    createdAt: '', updatedAt: '',
  }
  let rows = [review]
  let failure = ''
  let hold: Promise<unknown> | null = null
  let confirmAnswer = true
  let confirmations = 0
  let dismissed = 0
  const loadedReviews: ReviewDto[][] = []
  let focused = 0
  const onDestroy: (() => void)[] = []
  const modal = { open: false, showModal() { this.open = true } }
  let openDialogs: { contains: (target: unknown) => boolean }[] = []
  const target = { isConnected: true, focus: () => { focused++ } }
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
  let drafts: ReviewDraftsService
  const input = Object.assign(signal, { required: () => signal('') })
  const imports: Record<string, unknown> = {
    '@angular/core': {
      Component: noopDecorator, Injectable: noopDecorator, Input: noopDecorator, HostListener: noopDecorator,
      DestroyRef: destroyRef, signal, input,
      output: () => ({ emit: (reviews?: ReviewDto[]) => { if (reviews) loadedReviews.push(reviews); else dismissed++ } }),
      viewChild: { required: () => () => ({ nativeElement: modal }) },
      afterRenderEffect: () => {},
      inject: (token: unknown) => token === destroyRef ? destroyRef : token === reviewDraftsToken ? drafts : api,
    },
    '@angular/forms': { FormsModule: {} },
    'rxjs': { firstValueFrom: (value: unknown) => Promise.resolve(value) },
    '@/app/core/api/lifeos-api.service': { LifeosApiService: {}, apiErrorMessage: (error: Error) => error.message },
    '@/app/shared/dialog/dialog-guard': { formSnapshot, hasChanges },
    './dialog-guard': { dismissDecision, focusableFieldSelector },
    '@/app/shared/dialog/lifeos-dialog.component': { LifeosDialogComponent: {} },
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
      document: { querySelectorAll: () => openDialogs },
      HTMLElement: class {}, console,
    })
    return new exports[name]!()
  }
  drafts = load<ReviewDraftsService>('./review-drafts.service.ts', 'ReviewDraftsService')
  const component = load<HouseholdReviewsComponent>('./household-reviews.component.ts', 'HouseholdReviewsComponent')
  const dialog = load<LifeosDialogComponent>('../dialog/lifeos-dialog.component.ts', 'LifeosDialogComponent')
  component.targetId = 'target/1'
  return {
    component, dialog, drafts, review, calls, modal, target, loadedReviews,
    fail: (message: string) => { failure = message },
    hold: (promise: Promise<unknown> | null) => { hold = promise },
    confirm: (answer: boolean) => { confirmAnswer = answer },
    confirmations: () => confirmations, dismissed: () => dismissed, focused: () => focused,
    openDialogs: (dialogs: typeof openDialogs) => { openDialogs = dialogs },
    destroy: () => onDestroy.forEach(callback => callback()),
  }
}

describe('shared household review popup', () => {
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
    assert.equal(component.target, 'profile:profile-1')
    assert.equal(component.dirty(), false)
    component.cancelEdit()
    assert.equal(component.editorOpen(), false)
    assert.equal(component.dirty(), false)
  })

  for (const [targetType, segment] of [['food', 'foods'], ['recipe', 'recipes'], ['planned-line', 'planned-lines']] as const) {
    it(`saves and refreshes ${targetType} reviews in the original encoded scope, including zero and groups`, async () => {
      const { component, calls, loadedReviews } = harness()
      component.targetType = targetType
      component.add()
      component.target = 'group:group-1'
      component.score = 0
      await component.save()
      assert.equal(calls[0]?.path, `/reviews/${segment}/target%2F1`)
      assert.equal(JSON.stringify(calls[0]?.body), JSON.stringify({
        memberProfileId: null, householdGroupId: 'group-1', score: 0, comment: null,
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
    component.target = 'profile:profile-1'
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
    component.target = 'profile:profile-1'
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
    component.target = 'profile:profile-1'
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
    component.targetId = ''
    await component.load()
    pending.resolve(null)
    await loading
    assert.equal(component.reviews().length, 0)
    assert.equal(component.loading(), false)
  })
})

describe('native stacked dialog guards and focus', () => {
  it('routes cross, Escape and Cancel through the same dirty/pending guard', () => {
    const { dialog, confirm, confirmations, dismissed } = harness()
    const dirty = signal(true)
    const busy = signal(false)
    Object.assign(dialog, { dirty, busy })
    confirm(false)
    dialog.requestDismiss()
    let prevented = false
    dialog.onCancel({ preventDefault: () => { prevented = true } } as Event)
    assert.equal(prevented, true)
    assert.equal(dismissed(), 0)
    assert.equal(confirmations(), 2)
    busy.set(true)
    confirm(true)
    dialog.requestDismiss()
    assert.equal(dismissed(), 0)
    assert.equal(confirmations(), 2)
    busy.set(false)
    dialog.requestDismiss()
    assert.equal(dismissed(), 1)
  })

  it('restores focus inside an open parent but not behind an unrelated modal', () => {
    const { dialog, target, focused, openDialogs } = harness()
    Object.assign(dialog, { returnFocus: target })
    openDialogs([{ contains: candidate => candidate === target }])
    dialog.onNativeClose()
    assert.equal(focused(), 1)
    Object.assign(dialog, { returnFocus: target })
    openDialogs([{ contains: () => false }])
    dialog.onNativeClose()
    assert.equal(focused(), 1)
    Object.assign(dialog, { returnFocus: target })
    openDialogs([])
    dialog.onNativeClose()
    assert.equal(focused(), 2)
  })

  it('reopens a native force-close while the owner still considers the dialog open', () => {
    const { dialog, modal, focused } = harness()
    Object.assign(dialog, { open: signal(true) })
    dialog.onNativeClose()
    assert.equal(modal.open, true)
    assert.equal(focused(), 0)
  })
})
