export function packageProposal(label: string): { quantity: number | null; unit: string | null } {
  const match = label.match(/\b(\d+(?:[.,]\d+)?)\s*(kg|g|ml|l)\b/i)
  if (!match?.[1] || !match[2]) return { quantity: null, unit: null }
  return { quantity: Number(match[1].replace(',', '.')), unit: match[2].toLowerCase() === 'l' ? 'L' : match[2].toLowerCase() }
}

export function receiptDateInput(date: string | null): string {
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return ''
  return `${date.slice(8, 10)}/${date.slice(5, 7)}/${date.slice(0, 4)}`
}
