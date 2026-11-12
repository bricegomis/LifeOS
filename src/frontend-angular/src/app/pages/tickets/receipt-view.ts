import type { ReceiptDto, StoreDto } from '@/app/core/api/api.models'

export type ReceiptFilter = 'pending' | 'all'

/** yyyy-MM-dd (date only, no time zone) to dd/MM/yyyy. */
export function frenchDate(date: string | null): string {
  if (!date || !/^\d{4}-\d{2}-\d{2}/.test(date)) return ''
  return `${date.slice(8, 10)}/${date.slice(5, 7)}/${date.slice(0, 4)}`
}

/** Local calendar day of an upload timestamp, as dd/MM/yyyy. */
export function uploadDay(timestamp: string): string {
  const value = new Date(timestamp)
  if (Number.isNaN(value.getTime())) return ''
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${pad(value.getDate())}/${pad(value.getMonth() + 1)}/${value.getFullYear()}`
}

export function receiptSubtitle(receipt: ReceiptDto, stores: readonly StoreDto[]): string {
  const store = stores.find(item => item.id === receipt.storeId)?.name ?? 'Magasin non renseigné'
  const purchase = frenchDate(receipt.purchaseDate)
  return purchase ? `${store} · achat du ${purchase}` : store
}

export function visibleReceipts(receipts: readonly ReceiptDto[], filter: ReceiptFilter): ReceiptDto[] {
  return receipts.filter(receipt => filter === 'all' || receipt.status === 'pending')
}

export function pendingCount(receipts: readonly ReceiptDto[]): number {
  return receipts.filter(receipt => receipt.status === 'pending').length
}

/** Today in the browser's time zone, as the yyyy-MM-dd value a date input expects. */
export function todayInputValue(now = new Date()): string {
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}
