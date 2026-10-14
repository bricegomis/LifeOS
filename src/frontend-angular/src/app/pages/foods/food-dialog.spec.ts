import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import type { FoodItemDto, ProductDto, ReviewDto } from '../../core/api/api.models.ts'
import type { FoodItemsPageComponent } from './food-items-page.component.ts'
import type { HouseholdReviewsComponent } from '../../shared/reviews/household-reviews.component.ts'
import { newFoodDraft, missingFoodFields } from './food-draft.ts'
import { formSnapshot, hasChanges } from '../../shared/dialog/dialog-guard.ts'
import { summarizeFoodReviews } from './food-review-summary.ts'

function signal<T>(initial: T) {
  let value = initial
  return Object.assign(() => value, { set: (next: T) => { value = next } })
}

// Exercise production transitions with deterministic API ports, like native-services.spec.ts.
// The Angular builds validate the real templates and bindings.
function harness() {
  let foods: FoodItemDto[] = [{
    ...newFoodDraft(), id: 'food-1', name: 'Carotte', householdId: 'house-1',
    isArchived: false, hasImage: false, missingFields: [], source: 'manual', createdAt: '', updatedAt: '',
  }]
  let products: ProductDto[] = []
  let failure = ''
  let reviewFailure = ''
  let reviewRows: ReviewDto[] = []
  const imagePaths: string[] = []
  let confirm = true
  const api = {
    get: async (path: string) => {
      if (failure) throw new Error(failure)
      if (path.startsWith('/reviews/foods/')) {
        if (reviewFailure) throw new Error(reviewFailure)
        return reviewRows
      }
      return path === '/foods' ? foods : path === '/products' ? products : path === '/households/current' ? { memberProfiles: [] } : []
    },
    getBlob: async (path: string) => {
      imagePaths.push(path)
      return new Blob(['food-image'], { type: 'image/png' })
    },
    put: async (path: string, body: object) => {
      if (failure) throw new Error(failure)
      if (path.endsWith('/image')) {
        foods = [{ ...foods[0]!, hasImage: true }]
        return foods[0]
      }
      if (path.startsWith('/products/')) {
        products = [{ ...products[0]!, ...body }]
        return products[0]
      }
      foods = [{ ...foods[0]!, ...body }]
      return foods[0]
    },
    post: async (path: string, body: object) => {
      if (failure) throw new Error(failure)
      if (path === '/foods') {
        foods = [{ ...foods[0]!, ...body, id: 'created' }]
        return foods[0]
      }
      const product = {
        ...body, id: 'product-1', foodItemId: 'food-1', householdId: 'house-1', isArchived: false,
        priceHistory: [], purchaseUnitConfirmed: true, legacyPurchaseName: null, migrationOrigin: null,
      } as ProductDto
      products = [product]
      return product
    },
    delete: async (path: string) => {
      foods = path.endsWith('/image')
        ? foods.map(food => ({ ...food, hasImage: false }))
        : foods.map(food => ({ ...food, isArchived: true }))
    },
  }
  let reviews: HouseholdReviewsComponent | undefined
  const noopDecorator = () => () => {}
  const destroyRef = { onDestroy: () => {} }
  const reviewDrafts = { register: () => () => {} }
  const imports: Record<string, unknown> = {
    '@angular/core': {
      Component: noopDecorator, HostListener: noopDecorator, Input: noopDecorator,
      DestroyRef: destroyRef, inject: (token: unknown) => token === destroyRef ? destroyRef : token === reviewDrafts ? reviewDrafts : api,
      signal, viewChild: () => () => reviews, output: () => ({ emit: () => {} }),
    },
    '@angular/forms': { FormsModule: {} },
    '@angular/router': { RouterLink: {} },
    'rxjs': { firstValueFrom: (value: unknown) => Promise.resolve(value) },
    '@/app/core/api/lifeos-api.service': { LifeosApiService: {}, apiErrorMessage: (error: Error) => error.message },
    '@/app/shared/dialog/lifeos-dialog.component': { LifeosDialogComponent: {} },
    '@/app/shared/reviews/household-reviews.component': { HouseholdReviewsComponent: {} },
    '@/app/shared/dialog/dialog-guard': { formSnapshot, hasChanges },
    './food-draft': { newFoodDraft, missingFoodFields },
    './food-review-summary': { summarizeFoodReviews },
    './review-drafts.service': { ReviewDraftsService: reviewDrafts },
  }
  function load<T>(path: string, name: string): T {
    const source = readFileSync(new URL(path, import.meta.url), 'utf8')
    const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true } }).outputText
    const exports: Record<string, new () => T> = {}
    runInNewContext(js, {
      exports, require: (id: string) => { assert.ok(id in imports, `Missing port ${id}`); return imports[id] },
      window: { confirm: () => confirm }, HTMLElement: class {}, FormData, URL, console,
    })
    return new exports[name]!()
  }
  reviews = load<HouseholdReviewsComponent>('../../shared/reviews/household-reviews.component.ts', 'HouseholdReviewsComponent')
  const page = load<FoodItemsPageComponent>('./food-items-page.component.ts', 'FoodItemsPageComponent')
  return { page, reviews, food: foods[0]!, imagePaths, currentFoods: () => foods, fail: (message: string) => { failure = message }, confirm: (answer: boolean) => { confirm = answer },
    reviewRows: (rows: ReviewDto[]) => { reviewRows = rows }, failReviews: (message: string) => { reviewFailure = message } }
}

describe('food popup transitions', () => {
  it('keeps selection, filters and review drafts when returning from either editor', async () => {
    const { page, reviews, food } = harness()
    await page.load()
    page.search = 'car'
    page.incompleteOnly = true
    page.select(food)
    assert.equal(page.dialog(), 'detail')
    reviews.comment = 'En cours'
    page.editFood(food)
    assert.equal(page.dialog(), 'food')
    page.closeEditor()
    assert.equal(page.dialog(), 'detail')
    page.editProduct()
    assert.equal(page.productDraft.foodItemId, food.id)
    page.closeEditor()
    assert.equal(page.dialog(), 'detail')
    assert.equal(reviews.comment, 'En cours')
    assert.equal(page.search, 'car')
    assert.equal(page.incompleteOnly, true)
    page.closeDetail()
    assert.equal(page.dialog(), null)
    assert.equal(page.selected()?.id, food.id)
    assert.equal(reviews.dirty(), false)
  })

  it('returns updated food and new creations to their detail, but creation cancellation closes', async () => {
    const { page, food } = harness()
    await page.load()
    page.select(food)
    page.editFood(food)
    page.foodDraft.name = 'Carotte modifiée'
    await page.saveFood()
    assert.equal(page.dialog(), 'detail')
    assert.equal(page.selected()?.name, 'Carotte modifiée')
    page.closeDetail()
    page.editFood()
    page.closeEditor()
    assert.equal(page.dialog(), null)
    page.editFood()
    page.foodDraft.name = 'Nouvel aliment'
    await page.saveFood()
    assert.equal(page.dialog(), 'detail')
    assert.equal(page.selected()?.id, 'created')
  })

  it('uploads an optional image, reloads it through the API and supports removal', async () => {
    const { page, food, imagePaths, currentFoods } = harness()
    await page.load()
    page.select(food)
    page.editFood(food)
    page.foodImageFile.set(new File(['food-image'], 'carotte.png', { type: 'image/png' }))
    assert.equal(page.dirty(), true)
    await page.saveFood()
    assert.equal(currentFoods()[0]?.hasImage, true, page.dialogError())
    assert.equal(page.selected()?.hasImage, true)
    assert.ok(page.foodImageUrls()[food.id])
    assert.deepEqual(imagePaths, [`/foods/${food.id}/image`])

    page.editFood(page.selected()!)
    page.removeFoodImage()
    assert.equal(page.dirty(), true)
    assert.equal(page.foodImageRemoving(), true)
    await page.saveFood()
    assert.equal(page.selected()?.hasImage, false)
  })

  it('returns saved products to detail unless a price draft still needs saving', async () => {
    const { page, food } = harness()
    await page.load()
    page.select(food)
    page.editProduct()
    page.productDraft.name = 'Sachet'
    await page.saveProduct()
    assert.equal(page.dialog(), 'detail')
    assert.equal(page.productsFor(food.id)[0]?.name, 'Sachet')
    page.editProduct(page.products()[0])
    page.priceDraft.price = 2.5
    await page.saveProduct()
    assert.equal(page.dialog(), 'product')
    assert.equal(page.priceDraft.price, 2.5)
    assert.equal(page.dirty(), true)
    assert.equal(page.productDirty(), false)
  })

  it('keeps failed editor saves open with their draft and a visible error', async () => {
    const { page, food, fail } = harness()
    await page.load()
    page.select(food)
    page.editFood(food)
    page.foodDraft.name = 'À conserver'
    fail('Erreur réseau')
    await page.saveFood()
    assert.equal(page.dialog(), 'food')
    assert.equal(page.foodDraft.name, 'À conserver')
    assert.equal(page.dialogError(), 'Erreur réseau')
    assert.equal(page.busy(), false)
  })

  it('guards review edits and pending requests, including page navigation', async () => {
    const { page, reviews, food, confirm } = harness()
    await page.load()
    page.select(food)
    reviews.comment = 'Brouillon'
    assert.equal(reviews.dirty(), true)
    confirm(false)
    assert.equal(page.canLeave(), false)
    reviews.busy.set(true)
    page.editFood(food)
    assert.equal(page.dialog(), 'detail')
    page.editProduct()
    assert.equal(page.dialog(), 'detail')
    let prevented = false
    page.beforeUnload({ preventDefault: () => { prevented = true }, returnValue: '' } as BeforeUnloadEvent)
    assert.equal(prevented, true)
    reviews.busy.set(false)
    confirm(true)
    assert.equal(page.canLeave(), true)
  })

  it('loads real review summaries and updates them after detail review changes', async () => {
    const { page, food, reviewRows } = harness()
    reviewRows([{ score: 0 }, { score: 8 }, { score: null }] as ReviewDto[])
    await page.load()
    assert.deepEqual(JSON.parse(JSON.stringify(page.reviewSummaries()[food.id])), { count: 3, scoredCount: 2, average: 4 })
    page.updateReviewSummary(food.id, [{ score: 10 }] as ReviewDto[])
    assert.equal(page.reviewSummaries()[food.id]?.average, 10)
    page.updateReviewSummary(food.id, [])
    assert.equal(page.reviewSummaries()[food.id]?.average, null)
    assert.equal(page.reviewSummaries()[food.id]?.count, 0)
  })

  it('archives only after protecting review drafts and keeps the archived detail', async () => {
    const { page, reviews, food, confirm } = harness()
    await page.load()
    page.select(food)
    reviews.comment = 'À garder'
    confirm(false)
    await page.archive(food)
    assert.equal(page.selected()?.isArchived, false)
    confirm(true)
    await page.archive(food)
    assert.equal(page.dialog(), 'detail')
    assert.equal(page.selected()?.isArchived, true)
    assert.equal(page.visibleFoods().length, 0)
    page.archivedOnly = true
    assert.equal(page.visibleFoods()[0]?.id, food.id)
  })

  it('reports review retrieval failures without inventing a score', async () => {
    const { page, food, failReviews } = harness()
    await page.load()
    assert.equal(page.reviewSummaries()[food.id]?.average, null)
    failReviews('Avis indisponibles')
    await page.load()
    assert.equal(page.error(), 'Avis indisponibles')
    assert.equal(page.loading(), false)
    assert.equal(page.reviewSummaries()[food.id], undefined)
    assert.equal(page.visibleFoods()[0]?.id, food.id)
  })
})

describe('food card contract', () => {
  it('uses one native dialog trigger for the whole card and leaves archive in the detail only', () => {
    const source = readFileSync(new URL('./food-items-page.component.ts', import.meta.url), 'utf8')
    const list = source.split('<ul class="food-grid">')[1]!.split('</ul>')[0]!
    assert.match(list, /<button type="button" class="food-card-open"/)
    assert.match(list, /aria-haspopup="dialog"/)
    assert.match(list, /\(click\)="select\(food, \$event\)"/)
    assert.equal((list.match(/<button/g) ?? []).length, 1)
    assert.doesNotMatch(list, /Ouvrir|archive\(food\)/)
    assert.match(source, /Archiver cet aliment/)
    assert.match(list, /summary.average !== null/)
    const css = readFileSync(new URL('./catalog.css', import.meta.url), 'utf8')
    assert.match(css, /\.food-card-open::after \{[^}]*inset: 0/)
    assert.match(css, /\.food-card-open:focus-visible::after \{[^}]*outline: 3px/)
    assert.match(css, /minmax\(min\(100%, 300px\), 1fr\)/)
  })
})
