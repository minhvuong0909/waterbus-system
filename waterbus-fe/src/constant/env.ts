const DEFAULT_API_BASE_URL = ''

/** Base URL of the backend API, without a trailing slash. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL).replace(
  /\/+$/,
  '',
)

/**
 * Map tiles of the pier maps. The default is Esri's light gray canvas, which keeps the routes and boats readable, and
 * needs no key. Set
 * `VITE_MAP_TILE_URL` (and `VITE_MAP_TILE_ATTRIBUTION`) to use another provider, for example a
 * self-hosted tile server or a keyed service.
 */
export const MAP_TILE_URL =
  import.meta.env.VITE_MAP_TILE_URL ??
  'https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}'
export const MAP_TILE_ATTRIBUTION =
  import.meta.env.VITE_MAP_TILE_ATTRIBUTION ??
  'Tiles &copy; Esri &mdash; Esri, HERE, Garmin, OpenStreetMap contributors'
