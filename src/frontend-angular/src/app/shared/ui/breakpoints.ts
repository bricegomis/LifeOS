/** Phone width (ADR 0016): menu as a panel, dialogs full screen, add button floating. */
export const HANDSET_QUERY = '(max-width: 760px)'

export function isHandset(): boolean {
  return globalThis.matchMedia?.(HANDSET_QUERY).matches ?? false
}
