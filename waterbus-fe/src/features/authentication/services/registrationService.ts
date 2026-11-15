import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AccountRequestAccepted, RegisterPayload } from '../types'

/**
 * `POST /auth/register`: a guest creates a customer account. The backend answers the same way whether
 * or not the email already has an account, so this never tells the visitor which emails are taken.
 */
export function register(payload: RegisterPayload): Promise<AccountRequestAccepted> {
  return apiRequest<AccountRequestAccepted>(API_ENDPOINTS.auth.register, {
    method: 'POST',
    body: {
      email: payload.email.trim(),
      password: payload.password,
      fullName: payload.fullName.trim(),
      phoneNumber: payload.phoneNumber.trim() || null,
    },
  })
}

/** `POST /auth/confirm-email` with the `uid` and `token` of the link in the verification email. Rejects with ApiError(400) when invalid or expired. */
export function confirmEmail(userId: string, token: string): Promise<void> {
  return apiRequest<void>(API_ENDPOINTS.auth.confirmEmail, { method: 'POST', body: { userId, token } })
}

/** `POST /auth/resend-verification`: the same answer for any email; the backend only sends when it applies. */
export function resendVerification(email: string): Promise<AccountRequestAccepted> {
  return apiRequest<AccountRequestAccepted>(API_ENDPOINTS.auth.resendVerification, {
    method: 'POST',
    body: { email: email.trim() },
  })
}
