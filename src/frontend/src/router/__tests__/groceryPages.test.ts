import assert from 'node:assert/strict'
import { after, before, beforeEach, describe, it } from 'node:test'
import { createPinia, setActivePinia } from 'pinia'
import PrimeVue from 'primevue/config'
import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import { createServer, type ViteDevServer } from 'vite'

let server: ViteDevServer
let routeModule: typeof import('../routes.ts')
let storesModule: typeof import('../../stores/groceryStores.ts')
let itemsModule: typeof import('../../stores/groceryItems.ts')
const saved = new Map<string, string>()

before(async () => {
  server = await createServer({ server: { middlewareMode: true, hmr: false }, appType: 'custom' })
  routeModule = await server.ssrLoadModule('/src/router/routes.ts')
  storesModule = await server.ssrLoadModule('/src/stores/groceryStores.ts')
  itemsModule = await server.ssrLoadModule('/src/stores/groceryItems.ts')
  Object.defineProperty(globalThis, 'window', {
    configurable: true,
    value: {
      localStorage: {
        getItem: (key: string) => saved.get(key) ?? null,
        setItem: (key: string, value: string) => { saved.set(key, value) },
      },
    },
  })
})

after(async () => {
  await server?.close()
  delete (globalThis as { window?: unknown }).window
})

beforeEach(() => {
  saved.clear()
  setActivePinia(createPinia())
})

async function renderPage(path: string): Promise<string> {
  const router = createRouter({ history: createMemoryHistory(), routes: routeModule.routes })
  const app = createSSRApp({ render: () => h(RouterView) })
  app.use(createPinia())
  app.use(PrimeVue)
  app.use(router)
  await router.push(path)
  await router.isReady()
  return renderToString(app)
}

describe('dedicated grocery pages', () => {
  it('keeps the existing stores URL and resolves articles to a distinct page', () => {
    const router = createRouter({ history: createMemoryHistory(), routes: routeModule.routes })
    const stores = router.resolve('/stores')
    const articles = router.resolve('/articles')
    assert.equal(stores.name, 'stores')
    assert.equal(articles.name, 'articles')
    assert.equal(router.resolve({ name: 'stores' }).path, '/stores')
    assert.equal(router.resolve({ name: 'articles' }).path, '/articles')
    assert.notEqual(stores.matched.at(-1)?.components?.default, articles.matched.at(-1)?.components?.default)
  })

  it('renders only store management on the stores page with both navigation entries', async () => {
    const html = await renderPage('/stores')
    assert.match(html, /<h1\b[^>]*>Magasins<\/h1>/)
    assert.match(html, /Aucun magasin enregistré/)
    assert.match(html, /Ajouter un magasin/)
    assert.doesNotMatch(html, /Gestion des articles|Historique des prix/)
    assert.equal((html.match(/href="\/stores"/g) ?? []).length, 2)
    assert.equal((html.match(/href="\/articles"/g) ?? []).length, 2)
    assert.equal((html.match(/aria-current="page"/g) ?? []).length, 2)
  })

  it('renders only article management on the articles page and its empty state', async () => {
    const html = await renderPage('/articles')
    assert.match(html, /<h1\b[^>]*>Articles<\/h1>/)
    assert.match(html, /Aucun article enregistré/)
    assert.match(html, /Ajouter un article/)
    assert.doesNotMatch(html, /Gestion des magasins|Ajouter un magasin/)
    assert.equal((html.match(/href="\/stores"/g) ?? []).length, 2)
    assert.equal((html.match(/href="\/articles"/g) ?? []).length, 2)
    assert.equal((html.match(/aria-current="page"/g) ?? []).length, 2)
  })

  it('preserves CRUD, price associations and persistence between pages', async () => {
    const stores = storesModule.useGroceryStoresStore()
    const items = itemsModule.useGroceryItemsStore()
    assert.equal(stores.createStore({ name: ' ', address: '' }), false)
    assert.equal(items.createItem({ name: '', description: '', unit: 'unit' }), false)
    assert.equal(stores.createStore({ name: 'Marché', address: 'Rue du marché', isLocal: true }), true)
    assert.equal(items.createItem({ name: 'Riz', description: 'Complet', unit: 'kilogram' }), true)
    const storeId = stores.stores[0]!.id
    const itemId = items.items[0]!.id
    assert.equal(items.addPriceEntry(itemId, { storeId, price: -1, observedAt: '2026-10-09' }), false)
    assert.equal(items.addPriceEntry(itemId, { storeId, price: 3.5, observedAt: '2026-10-09' }), true)
    assert.equal(stores.updateStore(storeId, { name: 'Marché local', isLocal: true }), true)
    assert.equal(items.updateItem(itemId, { name: 'Riz complet', description: 'Vrac', unit: 'kilogram' }), true)

    setActivePinia(createPinia())
    const reloadedStores = storesModule.useGroceryStoresStore()
    const reloadedItems = itemsModule.useGroceryItemsStore()
    assert.equal(reloadedStores.stores[0]!.name, 'Marché local')
    assert.equal(reloadedItems.items[0]!.name, 'Riz complet')
    assert.equal(reloadedItems.items[0]!.priceHistory[0]!.storeId, storeId)
    assert.match(await renderPage('/articles'), /Riz complet/)
    assert.match(await renderPage('/stores'), /Marché local/)

    reloadedStores.deleteStore(storeId)
    assert.equal(reloadedItems.items[0]!.priceHistory[0]!.storeId, storeId)
    reloadedItems.deletePriceEntry(itemId, reloadedItems.items[0]!.priceHistory[0]!.id)
    assert.equal(reloadedItems.items[0]!.priceHistory.length, 0)
    reloadedItems.deleteItem(itemId)
    assert.equal(reloadedItems.items.length, 0)
  })
})
