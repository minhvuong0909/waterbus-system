import { AUTH_SHOWCASE, MOCK_USER } from '../../../mocks/auth'
import { withDelay } from '../../../mocks/delay'
import type { AuthShowcase, AuthUser, RegisterPayload, SignInPayload } from '../authenticationTypes'

// Mock service: no real authentication. Always succeeds so no page is ever blocked.
export function signIn(payload: SignInPayload): Promise<AuthUser> {
  return withDelay({ ...MOCK_USER, email: payload.email || MOCK_USER.email })
}

export function register(payload: RegisterPayload): Promise<AuthUser> {
  return withDelay({
    ...MOCK_USER,
    fullName: payload.fullName || MOCK_USER.fullName,
    email: payload.email || MOCK_USER.email,
  })
}

export function signInWithGoogle(): Promise<AuthUser> {
  return withDelay(MOCK_USER)
}

export function getAuthShowcase(): Promise<AuthShowcase> {
  return withDelay(AUTH_SHOWCASE, 0)
}
