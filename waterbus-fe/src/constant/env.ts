const DEFAULT_API_BASE_URL = ''

/** Base URL of the backend API, without a trailing slash. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL).replace(
  /\/+$/,
  '',
)

/**
 * Map tiles of the pier maps. The default is Esri's topographic map, which
 * shows the river and the districts clearly and needs no key. Set
 * `VITE_MAP_TILE_URL` (and `VITE_MAP_TILE_ATTRIBUTION`) to use another provider, for example a
 * self-hosted tile server or a keyed service.
 */
export const MAP_TILE_URL =
  import.meta.env.VITE_MAP_TILE_URL ??
  'https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}'
export const MAP_TILE_ATTRIBUTION =
  import.meta.env.VITE_MAP_TILE_ATTRIBUTION ??
  'Tiles &copy; Esri &mdash; Source: Esri, HERE, Garmin, OpenStreetMap contributors'

/**
 * Path of the staff sign-in page (Admin and Captain). Not linked from the public pages; share it with staff.
 * Hiding it is not a security measure: the backend decides which roles may sign in.
 */
export const STAFF_LOGIN_PATH = normalizePath(import.meta.env.VITE_STAFF_LOGIN_PATH, '/staff-login')

/** Shows "Sign in" in the public header. Off by default: only staff sign in, through STAFF_LOGIN_PATH. */
export const SHOW_PUBLIC_SIGNIN = import.meta.env.VITE_SHOW_PUBLIC_SIGNIN === 'true'

function normalizePath(value: string | undefined, fallback: string): string {
  const trimmed = (value ?? '').trim().replace(/\/+$/, '')
  if (!trimmed) return fallback
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`
}
