import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import type { FoodDto, ProductDto, StoreDto } from '../food.models.ts'
import { buildPriceRequest, buildProductRequest, productRows } from './product-form.ts'

const product = { foodItemId: 'f1', name: ' Lait de coco 400 ml ', packageQuantity: '400', packageUnit: 'ml' as const, brand: ' ', barcode: '' }

describe('product and price forms', () => {
  it('only builds a complete product', () => {
    assert.deepEqual(buildProductRequest(product), {
      ok: true,
      value: { foodItemId: 'f1', name: 'Lait de coco 400 ml', packageQuantity: 400, packageUnit: 'ml', brand: null, barcode: null },
    })
    assert.equal(buildProductRequest({ ...product, packageQuantity: '' }).ok, false)
    assert.equal(buildProductRequest({ ...product, packageQuantity: '0' }).ok, false)
    assert.equal(buildProductRequest({ ...product, foodItemId: '' }).ok, false)
    assert.equal(buildProductRequest({ ...product, name: ' ' }).ok, false)
    const loose = buildProductRequest({ ...product, packageQuantity: '1,5', packageUnit: 'kg' })
    assert.ok(loose.ok && loose.value.packageQuantity === 1.5)
  })

  it('takes a price in euros and cents on a past or present date', () => {
    const values = { productId: 'p1', storeId: 's1', price: '5,95', observedOn: '2026-11-02' }
    assert.deepEqual(buildPriceRequest(values, '2026-11-12'), { ok: true, value: { storeId: 's1', price: 5.95, observedOn: '2026-11-02' } })
    assert.equal(buildPriceRequest({ ...values, price: '0' }, '2026-11-12').ok, false)
    assert.equal(buildPriceRequest({ ...values, price: '1,234' }, '2026-11-12').ok, false)
    assert.equal(buildPriceRequest({ ...values, storeId: '' }, '2026-11-12').ok, false)
    assert.equal(buildPriceRequest({ ...values, observedOn: '2026-11-13' }, '2026-11-12').ok, false)
  })

  it('shows one column per store with the latest price and filters by text or store', () => {
    const stores = [{ id: 'leclerc', name: 'Leclerc' }, { id: 'biocoop', name: 'Biocoop' }] as StoreDto[]
    const foods = [{ id: 'coco', name: 'Lait de coco' }, { id: 'riz', name: 'Riz basmati' }] as FoodDto[]
    const products = [
      { id: 'p1', foodItemId: 'riz', name: 'Riz 1 kg', packageQuantity: 1, packageUnit: 'kg', isArchived: false, brand: null,
        latestPrices: [{ storeId: 'leclerc', price: 2.4, unitPrice: 2.4, unitLabel: 'kg', observedOn: '2026-11-02' }] },
      { id: 'p2', foodItemId: 'coco', name: 'Lait de coco 400 ml', packageQuantity: 400, packageUnit: 'ml', isArchived: false, brand: null,
        latestPrices: [{ storeId: 'biocoop', price: 2.2, unitPrice: 5.5, unitLabel: 'L', observedOn: '2026-10-28' }] },
      { id: 'p3', foodItemId: 'riz', name: 'Archivé', packageQuantity: 1, packageUnit: 'kg', isArchived: true, brand: null, latestPrices: [] },
    ] as unknown as ProductDto[]

    const rows = productRows(products, foods, stores, '', '')
    assert.deepEqual(rows.map(row => row.product.id), ['p2', 'p1'])
    assert.equal(rows[1]!.pack, 'Au kilo')
    assert.deepEqual(rows[0]!.cells, [null, { price: '2,20 €', detail: '5,50 €/L · 28/10/2026' }])
    assert.deepEqual(productRows(products, foods, stores, 'basmati', '').map(row => row.product.id), ['p1'])
    assert.deepEqual(productRows(products, foods, stores, '', 'biocoop').map(row => row.product.id), ['p2'])
  })
})
