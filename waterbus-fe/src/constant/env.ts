const DEFAULT_API_BASE_URL = 'http://localhost:5000'

/** Base URL of the backend API, without a trailing slash. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL).replace(
  /\/+$/,
  '',
)

/** When true, services skip the network and return mock data (see `.env.example`). */
export const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true'
