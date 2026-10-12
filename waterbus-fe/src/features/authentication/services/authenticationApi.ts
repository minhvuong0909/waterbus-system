import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { AUTH_SHOWCASE, MOCK_USER } from '../../../mocks/auth'
import { withDelay } from '../../../mocks/delay'
import { apiRequest } from '../../../services/apiClient'
import type {
  AuthSession,
  AuthShowcase,
  LoginResponse,
  RegisterPayload,
  SignInPayload,
} from '../authenticationTypes'

const MOCK_SESSION: AuthSession = { user: MOCK_USER, token: null }

/** `POST /auth/login`. Rejects with ApiError(401) when the email or password is wrong. */
export async function signIn({ email, password }: SignInPayload): Promise<AuthSession> {
  if (USE_MOCK) return withDelay({ ...MOCK_SESSION, user: { ...MOCK_USER, email } })

  const response = await apiRequest<LoginResponse>(API_ENDPOINTS.auth.login, {
    method: 'POST',
    body: { email, password },
  })
  return {
    token: response.token,
    user: {
      id: response.userId,
      fullName: response.fullName,
      email: response.email,
      roles: response.roles,
    },
  }
}

// Mock: the backend has no register endpoint yet (roadmap P1-BE-06).
export function register(payload: RegisterPayload): Promise<AuthSession> {
  return withDelay({
    ...MOCK_SESSION,
    user: {
      ...MOCK_USER,
      fullName: payload.fullName || MOCK_USER.fullName,
      email: payload.email || MOCK_USER.email,
    },
  })
}

// Mock: Google sign-in is not part of the backend yet.
export function signInWithGoogle(): Promise<AuthSession> {
  return withDelay(MOCK_SESSION)
}

export function getAuthShowcase(): Promise<AuthShowcase> {
  return withDelay(AUTH_SHOWCASE, 0)
}
