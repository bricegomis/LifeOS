export interface NavigationItem {
  path: string
  label: string
  /** Shows the number of receipts still to process. */
  receiptsBadge?: boolean
  /** Active only on this exact path (a module home whose sub-pages have their own entries). */
  exact?: boolean
}

export interface NavigationBlock {
  id: string
  title: string
  /** Material Symbol of the block (shared/ui/icons.ts). */
  icon: string
  items: NavigationItem[]
}

/**
 * Left menu, on the desktop and on the phone (ADR 0015): one block per domain that opens on its
 * pages. The phone has no tab bar any more; the same menu opens from the top bar.
 */
export const NAVIGATION_BLOCKS: NavigationBlock[] = [
  {
    id: 'food',
    title: 'Alimentation',
    icon: 'restaurant',
    items: [
      { path: '/recipes', label: 'Recettes' },
      { path: '/foods', label: 'Aliments' },
      { path: '/products', label: 'Produits et prix' },
      { path: '/stores', label: 'Magasins' },
      { path: '/tickets', label: 'Tickets', receiptsBadge: true },
    ],
  },
  {
    id: 'sport',
    title: 'Sport',
    icon: 'fitness_center',
    items: [
      { path: '/sports', label: 'Aujourd’hui', exact: true },
      { path: '/sports/history', label: 'Historique' },
      { path: '/sports/progress', label: 'Progression' },
      { path: '/sports/routines', label: 'Routines' },
      { path: '/sports/exercises', label: 'Exercices' },
      { path: '/sports/challenges', label: 'Défis' },
      { path: '/sports/goals', label: 'Objectifs' },
      { path: '/sports/templates', label: 'Séances types' },
    ],
  },
  {
    id: 'tasks',
    title: 'Tâches',
    icon: 'task_alt',
    items: [
      { path: '/tasks', label: 'Aujourd’hui', exact: true },
      { path: '/tasks/all', label: 'Toutes les tâches' },
      { path: '/tasks/routines', label: 'Routines' },
      { path: '/tasks/projects', label: 'Projets' },
    ],
  },
  {
    id: 'health',
    title: 'Santé',
    icon: 'favorite',
    items: [
      { path: '/health', label: 'Aujourd’hui', exact: true },
      { path: '/health/supplements', label: 'Compléments' },
      { path: '/health/symptoms', label: 'Symptômes' },
      { path: '/health/analyses', label: 'Analyses' },
      { path: '/health/measures', label: 'Mesures' },
      { path: '/health/documents', label: 'Documents' },
      { path: '/health/appointments', label: 'Rendez-vous' },
    ],
  },
]

/** The menu entry of a URL: the longest matching path, so `/sports/routine/42` belongs to Sport. */
export function currentEntry(url: string): { block: NavigationBlock, item: NavigationItem | null } | null {
  const path = url.split(/[?#]/)[0] ?? ''
  let best: { block: NavigationBlock, item: NavigationItem, length: number } | null = null
  for (const block of NAVIGATION_BLOCKS) {
    for (const item of block.items) {
      const matches = path === item.path || path.startsWith(item.path + '/')
      if (matches && (!best || item.path.length > best.length)) best = { block, item, length: item.path.length }
    }
  }
  if (!best) return null
  // A sub-page of a module home (the routine of the day) is not the home itself.
  const item = best.item.exact && path !== best.item.path ? null : best.item
  return { block: best.block, item }
}

/** Title of the phone's top bar: "Sport · Routines", the domain alone on a sub-page, or Paramètres. */
export function pageTitle(url: string): string {
  if ((url.split(/[?#]/)[0] ?? '').startsWith('/settings')) return 'Paramètres'
  const entry = currentEntry(url)
  if (!entry) return 'LifeOS'
  return entry.item ? `${entry.block.title} · ${entry.item.label}` : entry.block.title
}

const MENU_KEY = 'lifeos.menu-open'

/** The open/closed desktop menu is a per-browser convenience; storage may be unavailable. */
export function readMenuOpen(storage: Pick<Storage, 'getItem'> | undefined = globalThis.localStorage): boolean {
  try { return storage?.getItem(MENU_KEY) !== 'false' } catch { return true }
}

export function writeMenuOpen(open: boolean, storage: Pick<Storage, 'setItem'> | undefined = globalThis.localStorage): void {
  try { storage?.setItem(MENU_KEY, String(open)) } catch { /* private mode: the menu simply reopens */ }
}
