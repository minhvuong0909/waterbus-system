import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getToken } from '../../../services/authStorage'
import { guestHeaders } from '../../booking/services/bookingService'
import type { AccessLink, AccountOrder, Page, SharedTrip } from '../types'

/** Số đơn mỗi trang của lịch sử đơn hàng. */
export const ACCOUNT_ORDERS_PAGE_SIZE = 10

/** `GET /me/orders`: các đơn của tài khoản Passenger đang đăng nhập, mới nhất trước, không có đơn nháp. */
export function getAccountOrders(
  pageNumber: number,
  pageSize = ACCOUNT_ORDERS_PAGE_SIZE,
): Promise<Page<AccountOrder>> {
  return apiRequest<Page<AccountOrder>>(API_ENDPOINTS.me.orders, { query: { pageNumber, pageSize } })
}

/**
 * `POST /checkouts/{orderId}/claim` (BR-ID-04): gắn một đơn mua khi còn là Guest vào tài khoản
 * đang đăng nhập. Quyền được chứng minh bằng token của chính đơn đó, không bao giờ bằng email
 * trùng. Đơn đã thuộc tài khoản khác thì backend trả 409.
 */
export function claimOrder(orderId: string, accessToken: string): Promise<{ alreadyLinked: boolean }> {
  return apiRequest(API_ENDPOINTS.checkouts.claim(orderId), {
    method: 'POST',
    headers: guestHeaders(accessToken),
  })
}

/**
 * `POST /checkouts/{orderId}/access/rotate`: cấp link quản lý vé mới; link cũ ngừng hoạt động
 * ngay. Chủ đơn đã đăng nhập gọi được mà không cần token cũ (`accessToken` null).
 */
export function rotateOrderLink(orderId: string, accessToken: string | null): Promise<AccessLink> {
  return apiRequest<AccessLink>(API_ENDPOINTS.checkouts.rotateAccess(orderId), {
    method: 'POST',
    headers: guestHeaders(accessToken),
  })
}

/** `POST /checkouts/{orderId}/access/revoke`: thu hồi mọi link quản lý vé và link chia sẻ hành trình của đơn. */
export function revokeOrderLinks(
  orderId: string,
  accessToken: string | null,
): Promise<{ revokedCount: number }> {
  return apiRequest(API_ENDPOINTS.checkouts.revokeAccess(orderId), {
    method: 'POST',
    headers: guestHeaders(accessToken),
  })
}

/**
 * `POST /checkouts/{orderId}/bookings/{bookingId}/trip-access` (BR-ID-05): link để hành khách đi
 * cùng xem hành trình. Chỉ cấp cho đặt chỗ đã thanh toán; chưa thanh toán thì backend trả 409.
 */
export function createTripShareLink(
  orderId: string,
  bookingId: string,
  accessToken: string | null,
): Promise<AccessLink> {
  return apiRequest<AccessLink>(API_ENDPOINTS.checkouts.tripAccess(orderId, bookingId), {
    method: 'POST',
    headers: guestHeaders(accessToken),
  })
}

/** `GET /tripaccess`: hành trình sau một link chia sẻ. Token sai, hết hạn hoặc đã thu hồi → 403. */
export function getSharedTrip(token: string): Promise<SharedTrip> {
  return apiRequest<SharedTrip>(API_ENDPOINTS.tripAccess, {
    headers: { 'X-Trip-Access-Token': token },
  })
}

/** `GET /me/orders/linkable`: guest orders bought with the account's VERIFIED email and not linked yet. */
export function getLinkableOrderCount(): Promise<{ count: number }> {
  return apiRequest<{ count: number }>(API_ENDPOINTS.me.linkableOrders)
}

/**
 * `POST /me/orders/link-by-email`: links those orders to the signed-in account (up to 200 per call,
 * `remaining` > 0 means call again). Never links by an unverified email and never takes over an order of another account.
 */
export function linkOrdersByEmail(): Promise<{ linked: number; remaining: number }> {
  return apiRequest<{ linked: number; remaining: number }>(API_ENDPOINTS.me.linkOrders, { method: 'POST' })
}

const MAX_LINK_BATCHES = 10
const guestOrderSync = new Map<string, Promise<void>>()

/** Shares the current run across mounts for this session and checks eligibility again on a later visit. */
export function syncGuestOrders(token: string): Promise<void> {
  const running = guestOrderSync.get(token)
  if (running) return running

  const sync = async () => {
    if (getToken() !== token) throw new Error('The active session changed.')
    if ((await getLinkableOrderCount()).count === 0) return

    for (let batch = 0; batch < MAX_LINK_BATCHES; batch++) {
      // apiRequest reads the active token from storage at the start of each request.
      if (getToken() !== token) throw new Error('The active session changed.')
      const { linked, remaining } = await linkOrdersByEmail()
      if (remaining === 0) return
      if (linked === 0) break
    }
    throw new Error('Guest order synchronization is incomplete.')
  }

  const request = sync().finally(() => guestOrderSync.delete(token))
  guestOrderSync.set(token, request)
  return request
}
