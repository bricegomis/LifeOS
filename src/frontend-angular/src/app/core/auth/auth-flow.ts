// A known top-level section, then at most two plain segments (e.g. /sports/routine/<id>).
const INTERNAL_ROUTE = /^\/(?:(?:planning|recipes|meals|products|foods|sports|tasks|health|budget|reading|stores|articles|tickets|library|settings|today|planner)(?:\/[A-Za-z0-9-]{1,64}){0,2})?(?:\?[^#\\\s]*)?$/

export function validatedReturnPath(value: string | null): string {
  return value && INTERNAL_ROUTE.test(value) ? value : '/'
}

export function buildLoginRedirectPath(returnPath: string): string {
  return `/login?redirect=${encodeURIComponent(validatedReturnPath(returnPath))}`
}
