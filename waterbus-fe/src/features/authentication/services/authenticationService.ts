import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { ROLES } from '../../../constant/roles'
import { ApiError, apiRequest } from '../../../services/apiClient'
import type {
  AuthSession,
  LoginResponse,
  SignInPayload,
} from '../types'

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
