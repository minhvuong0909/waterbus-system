export type AuthMode = 'signin' | 'register'

export interface SignInPayload {
  email: string
  password: string
  remember: boolean
}

export interface RegisterPayload {
  fullName: string
  email: string
  phone: string
  password: string
}

export interface AuthUser {
  id: string
  fullName: string
  email: string
  /** Role names as issued by the backend, e.g. "Admin" (see `constant/roles.ts`). */
  roles: string[]
}

/** The signed-in user and their JWT. `token` is null only for mock sessions. */
export interface AuthSession {
  user: AuthUser
  token: string | null
}

/** Response body of `POST /api/v1/auth/login` (AuthResponseDto in the backend). */
export interface LoginResponse {
  userId: string
  email: string
  fullName: string
  roles: string[]
  token: string
}

export interface AuthShowcase {
  eyebrow: string
  headline: [string, string]
  intro: string
  crossing: { corridor: string; duration: string; from: string; to: string; vessel: string }
  highlights: string[]
}
