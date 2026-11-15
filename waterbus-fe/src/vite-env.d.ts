/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_STAFF_LOGIN_PATH?: string
  readonly VITE_SHOW_PUBLIC_SIGNIN?: string
  readonly VITE_MAP_TILE_URL?: string
  readonly VITE_MAP_TILE_ATTRIBUTION?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
