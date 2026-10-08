export type DismissDecision = 'ignore' | 'confirm' | 'close'

/** Decides what a dismiss request (Escape, close button, Annuler) should do. */
export function dismissDecision(state: { busy: boolean; dirty: boolean }): DismissDecision {
  if (state.busy) return 'ignore'
  return state.dirty ? 'confirm' : 'close'
}

/** Stable serialisation used to detect unsaved changes in a form draft. */
export function formSnapshot(value: unknown): string {
  return JSON.stringify(value, (_key, current: unknown) => {
    if (current && typeof current === 'object' && !Array.isArray(current)) {
      return Object.fromEntries(Object.entries(current as Record<string, unknown>)
        .sort(([a], [b]) => a.localeCompare(b)))
    }
    return current
  }) ?? ''
}

export function hasChanges(initialSnapshot: string, current: unknown): boolean {
  return formSnapshot(current) !== initialSnapshot
}

export const focusableFieldSelector = [
  '[autofocus]',
  'input:not([type="hidden"]):not([disabled]):not([readonly])',
  'select:not([disabled])',
  'textarea:not([disabled]):not([readonly])',
].join(', ')
