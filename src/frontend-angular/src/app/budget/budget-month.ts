import { signal } from '@angular/core'

/** The month shown by the Budget pages, kept while moving between them. */
export const SELECTED_MONTH = signal(new Date().toISOString().slice(0, 7))
