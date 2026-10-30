export interface SignInPayload {
  email: string
  password: string
  remember: boolean
}

export interface AuthUser {
  id: string
  fullName: string
  email: string
  /** Role names as issued by the backend, e.g. "Admin" (see `constant/roles.ts`). */
  roles: string[]
}

/** The signed-in user and their JWT. */
export interface AuthSession {
  user: AuthUser
  token: string
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
  highlights: string[]
}
