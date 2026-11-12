const INTERNAL_ROUTE = /^\/(?:(?:planning|recipes|meals|products|foods|sports|stores|articles|tickets|library|settings|today|planner)(?:\/[A-Za-z0-9-]{1,64})?)?(?:\?[^#\\\s]*)?$/

export function validatedReturnPath(value: string | null): string {
  return value && INTERNAL_ROUTE.test(value) ? value : '/'
}

export function buildLoginRedirectPath(returnPath: string): string {
  return `/login?redirect=${encodeURIComponent(validatedReturnPath(returnPath))}`
}
