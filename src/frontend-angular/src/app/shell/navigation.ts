export interface NavigationItem {
  path: string
  label: string
  /** Shows the number of receipts still to process. */
  receiptsBadge?: boolean
  /** Active only on this exact path (a module home whose sub-pages have their own entries). */
  exact?: boolean
}

export interface NavigationBlock {
  title: string
  items: NavigationItem[]
}

/** Desktop menu: one block per domain, one entry per page (validated step 1 mockup). */
export const NAVIGATION_BLOCKS: NavigationBlock[] = [
  {
    title: 'Alimentation',
    items: [
      { path: '/recipes', label: 'Recettes' },
      { path: '/foods', label: 'Aliments' },
      { path: '/products', label: 'Produits et prix' },
      { path: '/stores', label: 'Magasins' },
      { path: '/tickets', label: 'Tickets', receiptsBadge: true },
    ],
  },
  {
    title: 'Sport',
    items: [
      { path: '/sports', label: 'Aujourd’hui', exact: true },
      { path: '/sports/history', label: 'Historique' },
      { path: '/sports/progress', label: 'Progression' },
      { path: '/sports/settings', label: 'Réglages' },
    ],
  },
]

/** Phone tabs: everyday screens only; stores, products and prices are entered on a computer. */
export const MOBILE_TABS: NavigationItem[] = [
  { path: '/recipes', label: 'Recettes' },
  { path: '/foods', label: 'Aliments' },
  { path: '/sports', label: 'Sport' },
  { path: '/tickets', label: 'Tickets' },
  { path: '/settings', label: 'Réglages' },
]

const MENU_KEY = 'lifeos.menu-open'

/** The open/closed menu is a per-browser convenience; storage may be unavailable. */
export function readMenuOpen(storage: Pick<Storage, 'getItem'> | undefined = globalThis.localStorage): boolean {
  try { return storage?.getItem(MENU_KEY) !== 'false' } catch { return true }
}

export function writeMenuOpen(open: boolean, storage: Pick<Storage, 'setItem'> | undefined = globalThis.localStorage): void {
  try { storage?.setItem(MENU_KEY, String(open)) } catch { /* private mode: the menu simply reopens */ }
}
