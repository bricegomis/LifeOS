export function purchaseDraftHasChanges(
  articleId: string | null,
  selectedArticleId: string,
  purchaseUnitChoice: string,
): boolean {
  return selectedArticleId !== (articleId ?? '')
    || (!articleId && purchaseUnitChoice !== 'unit')
}
