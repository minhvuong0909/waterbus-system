export interface SignInPayload {
  email: string
  password: string
  remember: boolean
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
