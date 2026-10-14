import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, it } from 'node:test'
import ts from 'typescript'
import type { FoodItemDto, ProductDto } from '../../core/api/api.models.ts'
import type { FoodItemsPageComponent } from './food-items-page.component.ts'
import type { HouseholdReviewsComponent } from '../../shared/reviews/household-reviews.component.ts'
import { newFoodDraft, missingFoodFields } from './food-draft.ts'
import { formSnapshot, hasChanges } from '../../shared/dialog/dialog-guard.ts'

function signal<T>(initial: T) {
  let value = initial
  return Object.assign(() => value, { set: (next: T) => { value = next } })
}

// Exercise production transitions with deterministic API ports, like native-services.spec.ts.
// The Angular builds validate the real templates and bindings.
function harness() {
  let foods: FoodItemDto[] = [{
    ...newFoodDraft(), id: 'food-1', name: 'Carotte', householdId: 'house-1',
    isArchived: false, missingFields: [], source: 'manual', createdAt: '', updatedAt: '',
  }]
  let products: ProductDto[] = []
  let failure = ''
  let confirm = true
  const api = {
    get: async (path: string) => {
      if (failure) throw new Error(failure)
      return path === '/foods' ? foods : path === '/products' ? products : path === '/households/current' ? { memberProfiles: [] } : []
    },
    put: async (path: string, body: object) => {
      if (failure) throw new Error(failure)
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
    delete: async () => {},
  }
  let reviews: HouseholdReviewsComponent | undefined
  const noopDecorator = () => () => {}
  const destroyRef = { onDestroy: () => {} }
  const reviewDrafts = { register: () => () => {} }
  const imports: Record<string, unknown> = {
    '@angular/core': {
      Component: noopDecorator, HostListener: noopDecorator, Input: noopDecorator,
      DestroyRef: destroyRef, inject: (token: unknown) => token === destroyRef ? destroyRef : token === reviewDrafts ? reviewDrafts : api,
      signal, viewChild: () => () => reviews,
    },
    '@angular/forms': { FormsModule: {} },
    '@angular/router': { RouterLink: {} },
    'rxjs': { firstValueFrom: (value: unknown) => Promise.resolve(value) },
    '@/app/core/api/lifeos-api.service': { LifeosApiService: {}, apiErrorMessage: (error: Error) => error.message },
    '@/app/shared/dialog/lifeos-dialog.component': { LifeosDialogComponent: {} },
    '@/app/shared/reviews/household-reviews.component': { HouseholdReviewsComponent: {} },
    '@/app/shared/dialog/dialog-guard': { formSnapshot, hasChanges },
    './food-draft': { newFoodDraft, missingFoodFields },
    './review-drafts.service': { ReviewDraftsService: reviewDrafts },
  }
  function load<T>(path: string, name: string): T {
    const source = readFileSync(new URL(path, import.meta.url), 'utf8')
    const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true } }).outputText
    const exports: Record<string, new () => T> = {}
    runInNewContext(js, {
      exports, require: (id: string) => { assert.ok(id in imports, `Missing port ${id}`); return imports[id] },
      window: { confirm: () => confirm }, HTMLElement: class {}, console,
    })
    return new exports[name]!()
  }
  reviews = load<HouseholdReviewsComponent>('../../shared/reviews/household-reviews.component.ts', 'HouseholdReviewsComponent')
  const page = load<FoodItemsPageComponent>('./food-items-page.component.ts', 'FoodItemsPageComponent')
  return { page, reviews, food: foods[0]!, fail: (message: string) => { failure = message }, confirm: (answer: boolean) => { confirm = answer } }
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
})
