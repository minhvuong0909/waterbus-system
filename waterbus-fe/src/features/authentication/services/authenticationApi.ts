import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { ROLES } from '../../../constant/roles'
import { AUTH_SHOWCASE } from '../../../content/auth'
import { localize } from '../../../i18n'
import { ApiError, apiRequest } from '../../../services/apiClient'
import type {
  AuthSession,
  AuthShowcase,
  LoginResponse,
  RegisterPayload,
  SignInPayload,
} from '../authenticationTypes'

/** `POST /auth/login`. Rejects with ApiError(401) when the email or password is wrong. */
export async function signIn({ email, password }: SignInPayload): Promise<AuthSession> {
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

// Backend chưa có endpoint đăng ký (roadmap P1-BE-06): báo rõ thay vì giả vờ đã tạo tài khoản.
// eslint-disable-next-line @typescript-eslint/no-unused-vars -- giữ chữ ký như API thật
export function register(_payload: RegisterPayload): Promise<AuthSession> {
  return Promise.reject(new ApiError(501, 'Backend chưa hỗ trợ đăng ký tài khoản. Vui lòng dùng tài khoản đã được cấp.'))
}

// Đăng nhập Google chưa thuộc backend.
export function signInWithGoogle(): Promise<AuthSession> {
  return Promise.reject(new ApiError(501, 'Backend chưa hỗ trợ đăng nhập Google.'))
}

export function getAuthShowcase(): Promise<AuthShowcase> {
  return Promise.resolve(localize(AUTH_SHOWCASE))
}
