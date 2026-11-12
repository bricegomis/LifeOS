export function validateIosConfig(config) {
  let url
  try { url = new URL(config.apiBaseUrl) } catch { throw new Error('iOS : apiBaseUrl absent ou invalide') }
  if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash) {
    throw new Error('iOS : apiBaseUrl doit être une URL HTTPS publique sans credentials')
  }
  if (url.pathname.replace(/\/+$/, '').endsWith('/api')) {
    throw new Error('iOS : LIFEOS_API_URL doit omettre /api, ajouté par le client')
  }
}
