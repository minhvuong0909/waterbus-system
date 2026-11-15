import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { TicketLookupOtpInfo, TicketLookupResult } from '../types'

/**
 * `POST /tickets/lookup/otp`. Always resolves the same way when the input is well formed, so the
 * screen cannot tell whether the booking code exists. The code goes to the purchaser's email.
 */
export function requestLookupOtp(email: string, bookingCode: string): Promise<TicketLookupOtpInfo> {
  return apiRequest<TicketLookupOtpInfo>(API_ENDPOINTS.ticketLookup.otp, {
    method: 'POST',
    body: { email: email.trim(), bookingCode: bookingCode.trim() },
  })
}

/** `POST /tickets/lookup/verify`. Rejects with ApiError(403) for a wrong, expired or used code. */
export function verifyLookupOtp(email: string, bookingCode: string, otp: string): Promise<TicketLookupResult> {
  return apiRequest<TicketLookupResult>(API_ENDPOINTS.ticketLookup.verify, {
    method: 'POST',
    body: { email: email.trim(), bookingCode: bookingCode.trim(), otp: otp.trim() },
  })
}
