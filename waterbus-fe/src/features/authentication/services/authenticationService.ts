import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { LoginResponse, SignInPayload } from '../types'
import type { AuthSession } from '../../../services/authStorage'

/** `POST /auth/login`. Rejects with ApiError(401) when the email or password is wrong. */
export async function signIn({ email, password }: SignInPayload): Promise<AuthSession> {
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
