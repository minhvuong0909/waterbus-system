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
}

export interface AuthShowcase {
  eyebrow: string
  headline: [string, string]
  intro: string
  crossing: { corridor: string; duration: string; from: string; to: string; vessel: string }
  highlights: string[]
}
