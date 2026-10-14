export function validateIosConfig(config) {
  for (const field of ['apiBaseUrl', 'supabaseUrl']) {
    let url
    try { url = new URL(config[field]) } catch { throw new Error(`iOS : ${field} absent ou invalide`) }
    if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash) {
      throw new Error(`iOS : ${field} doit être une URL HTTPS publique sans credentials`)
    }
  }
  const api = new URL(config.apiBaseUrl)
  if (api.pathname.replace(/\/+$/, '').endsWith('/api')) {
    throw new Error('iOS : VITE_LIFEOS_API_URL doit omettre /api, ajouté par le client')
  }
  const key = config.supabaseAnonKey
  if (typeof key !== 'string' || !key.trim()) throw new Error('iOS : clé Supabase publique manquante')
  if (key.startsWith('sb_publishable_')) return
  try {
    const payload = JSON.parse(Buffer.from(key.split('.')[1], 'base64url').toString('utf8'))
    if (payload.role === 'anon') return
  } catch {
    throw new Error('iOS : utiliser seulement la clé Supabase anon/publishable publique')
  }
  throw new Error('iOS : clé Supabase serveur interdite dans le bundle')
}
