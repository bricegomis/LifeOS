import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodItemDto } from '../../core/api/api.models.ts'
import { newProductEditor, productDraftProblem, productEditorFromItem, productRequest } from './product-draft.ts'

const product: FoodItemDto = {
  id: 'one-id', articleId: 'one-id', householdId: 'household', name: 'Riz', description: 'Achats et nutrition',
  referenceUnit: '100 g', unit: 'kilogram', purchaseUnitConfirmed: true,
  nutrition: { caloriesPerUnit: 150, proteinsPerUnit: 3, carbsPerUnit: 30, fatsPerUnit: 1 },
  priceHistory: [{ id: 'price', storeId: 'store', price: 2.5, observedAt: '2026-10-08', createdAt: '2026-10-08' }],
  source: 'Manual', offBarcode: null, isCorrectionOf: null, isArchived: false,
  legacyPurchaseName: null, migrationOrigin: null, createdAt: '', updatedAt: '',
}

describe('single product creation and editing', () => {
  it('sends one request with independent nutrition reference and purchase units, without scaling nutrition', () => {
    const editor = productEditorFromItem(product)
    editor.name = ' Nouveau nom '
    const request = productRequest(editor)
    assert.equal(request.name, 'Nouveau nom')
    assert.equal(request.unit, 'kilogram')
    assert.equal(request.referenceUnit, '100 g')
    assert.equal(request.nutrition?.caloriesPerUnit, 150)
    assert.equal(request.description, product.description)
    assert.equal('articleId' in request, false)
    assert.equal(productDraftProblem(request), '')
    assert.equal(product.priceHistory[0]?.price, 2.5)
  })

  it('creates non-food products with no invented nutrition or reference quantity', () => {
    const editor = newProductEditor()
    editor.name = 'Savon'
    editor.unit = 'unit'
    const request = productRequest(editor)
    assert.equal(request.nutrition, null)
    assert.equal(request.referenceUnit, '')
    assert.equal(productDraftProblem(request), '')
  })

  it('keeps unknown nutrients distinct from zero and requires a reference when nutrition exists', () => {
    const editor = newProductEditor()
    editor.name = 'Produit'
    editor.unit = 'liter'
    editor.caloriesPerUnit = 0
    assert.match(productDraftProblem(productRequest(editor)), /référence/)
    editor.referenceUnit = '100 ml'
    const request = productRequest(editor)
    assert.equal(request.nutrition?.caloriesPerUnit, 0)
    assert.equal(request.nutrition?.proteinsPerUnit, null)
    assert.equal(request.unit, 'liter')
    assert.equal(productDraftProblem(request), '')
    editor.caloriesPerUnit = -1
    assert.match(productDraftProblem(productRequest(editor)), /positives/)
  })

  it('requires explicit purchase-unit resolution for migrated unlinked foods and OFF imports', () => {
    const editor = productEditorFromItem({ ...product, purchaseUnitConfirmed: false, migrationOrigin: 'food-only' })
    assert.equal(editor.unit, '')
    assert.equal(productRequest(editor).unit, null)
    assert.match(productDraftProblem(productRequest(editor)), /achat/)
    editor.unit = 'unit'
    assert.equal(productDraftProblem(productRequest(editor)), '')
  })
})
