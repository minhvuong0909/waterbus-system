/** Expiry time (ms since epoch) from the `exp` claim of a JWT, or null if it cannot be read. */
export function getTokenExpiry(token: string): number | null {
  const payload = token.split('.')[1]
  if (!payload) return null
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    )
    const exp = (JSON.parse(json) as { exp?: unknown }).exp
    return typeof exp === 'number' ? exp * 1000 : null
  } catch {
    return null
  }
}

/** True when the token carries an expiry that has passed. Unreadable tokens count as valid. */
export function isTokenExpired(token: string, now = Date.now()): boolean {
  const expiry = getTokenExpiry(token)
  return expiry !== null && expiry <= now
}
