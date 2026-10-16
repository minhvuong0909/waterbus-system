const DEFAULT_API_BASE_URL = 'http://localhost:5000'

/** Base URL of the backend API, without a trailing slash. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL).replace(
  /\/+$/,
  '',
)

/** When true, services skip the network and return mock data (see `.env.example`). */
export const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true'

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
