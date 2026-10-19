import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { ROLES } from '../../../constant/roles'
import { AUTH_SHOWCASE, MOCK_USER } from '../../../mocks/auth'
import { withDelay } from '../../../mocks/delay'
import { ApiError, apiRequest } from '../../../services/apiClient'
import type {
  AuthSession,
  AuthShowcase,
  LoginResponse,
  RegisterPayload,
  SignInPayload,
} from '../authenticationTypes'

const MOCK_SESSION: AuthSession = { user: MOCK_USER, token: null }

// Mock sign-in: "admin@...", "dispatcher@..." or "captain@..." get that role, anything else is a passenger.
const MOCK_ROLES: Record<string, string> = {
  admin: ROLES.admin,
  captain: ROLES.captain,
}
const mockRoleOf = (email: string): string => MOCK_ROLES[email.split('@')[0].toLowerCase()] ?? ROLES.passenger

/** `POST /auth/login`. Rejects with ApiError(401) when the email or password is wrong. */
export async function signIn({ email, password }: SignInPayload): Promise<AuthSession> {
  if (USE_MOCK) {
    return withDelay({ ...MOCK_SESSION, user: { ...MOCK_USER, email, roles: [mockRoleOf(email)] } })
  }

  const response = await apiRequest<LoginResponse>(API_ENDPOINTS.auth.login, {
    method: 'POST',
    body: { email, password },
  })
  if (response.roles.includes(ROLES.staff) && !response.roles.some((role) => role === ROLES.admin || role === ROLES.captain || role === ROLES.passenger)) {
    throw new ApiError(403, 'Tài khoản Staff dùng ứng dụng Staff Scanner để soát vé.')
  }
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
  if (!USE_MOCK) return Promise.reject(new ApiError(501, 'Backend chưa hỗ trợ đăng ký tài khoản. Vui lòng dùng tài khoản đã được cấp.'))
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
  if (!USE_MOCK) return Promise.reject(new ApiError(501, 'Backend chưa hỗ trợ đăng nhập Google.'))
  return withDelay(MOCK_SESSION)
}

export function getAuthShowcase(): Promise<AuthShowcase> {
  return withDelay(AUTH_SHOWCASE, 0)
}
