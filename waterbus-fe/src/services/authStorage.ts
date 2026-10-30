import type { AuthSession } from '../features/authentication/types'
import { isTokenExpired } from '../utils/jwt'

const STORAGE_KEY = 'waterbus.session'

// "Keep me signed in" uses localStorage; otherwise the session ends with the browser tab.
// Storage can throw (private mode, blocked site data), so every access is guarded.
function stores(): Storage[] {
  try {
    return [window.localStorage, window.sessionStorage]
  } catch {
    return []
  }
}

function read(): AuthSession | null {
  for (const store of stores()) {
    try {
      const raw = store.getItem(STORAGE_KEY)
      if (raw) return JSON.parse(raw) as AuthSession
    } catch {
      // Corrupt or unreadable entry: try the next store.
    }
  }
  return null
}

/** The saved session, or null when there is none or its token has expired. */
export function loadSession(): AuthSession | null {
  const session = read()
  if (!session) return null
  if (session.token && isTokenExpired(session.token)) {
    clearSession()
    return null
  }
  return session
}

export function saveSession(session: AuthSession, remember: boolean) {
  clearSession()
  const [persistent, temporary] = stores()
  const target = remember ? persistent : temporary
  try {
    target?.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // The session then only lives in memory (Redux) until the page is reloaded.
  }
}

export function clearSession() {
  for (const store of stores()) {
    try {
      store.removeItem(STORAGE_KEY)
    } catch {
      // Nothing to clear.
    }
  }
}

/** Bearer token of the saved session, if any. */
export function getToken(): string | null {
  return read()?.token ?? null
}
