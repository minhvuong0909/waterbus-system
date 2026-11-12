import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { DownloadedFile } from '../../../services/apiClient'
import type {
  BatchConfirmResult,
  BatchRefundRequestResult,
  ManualRefundInput,
  ManualRefundResult,
  Paged,
  PendingRefund,
  RefundCandidate,
  RefundRequestResult,
} from '../types'

/**
 * Tạo yêu cầu hoàn tiền cho một Booking của chuyến bị huỷ/chấm dứt (BR-RF-07). Đơn khứ hồi chỉ
 * hoàn phần của Booking đủ điều kiện (BR-RF-08). Backend chống lặp nên gửi lại không hoàn hai lần.
 */
export function requestRefund(bookingId: string, reason: string): Promise<RefundRequestResult> {
  return apiRequest<RefundRequestResult>(API_ENDPOINTS.refunds.request, {
    method: 'POST',
    body: { bookingId, reason },
  })
}

/** Admin xác nhận đã hoàn tay; bắt buộc có bằng chứng, không xác nhận được lần hai (BR-RF-10). */
export function confirmManualRefund(input: ManualRefundInput): Promise<ManualRefundResult> {
  return apiRequest<ManualRefundResult>(API_ENDPOINTS.refunds.confirmManual, {
    method: 'POST',
    body: input,
  })
}

/** Hỏi lại cổng thanh toán trạng thái của một giao dịch hoàn tiền đang chờ. */
export async function reconcileRefund(refundId: string): Promise<string> {
  const result = await apiRequest<{ status: string }>(API_ENDPOINTS.refunds.reconcile(refundId), {
    method: 'POST',
  })
  return result.status
}

/** Booking của một chuyến bị huỷ/chấm dứt cần hoàn (chỉ đọc). Chuyến chưa huỷ → 409. */
export function listRefundCandidates(tripId: string, page = 1, pageSize = 100): Promise<Paged<RefundCandidate>> {
  return apiRequest<Paged<RefundCandidate>>(API_ENDPOINTS.refunds.candidates, { query: { tripId, page, pageSize } })
}

/** Tạo yêu cầu hoàn cho mọi Booking đủ điều kiện của chuyến; Booking lỗi nằm ở `skippedItems`. Gọi lại không hoàn trùng. */
export function requestRefundsForTrip(tripId: string, reason?: string): Promise<BatchRefundRequestResult> {
  return apiRequest<BatchRefundRequestResult>(API_ENDPOINTS.refunds.batchRequest, {
    method: 'POST',
    body: { tripId, reason: reason?.trim() || undefined },
  })
}

/** Các khoản hoàn Manual đang chờ nhân viên hoàn (chỉ đọc, không ghi dấu đã xuất). */
export function listPendingRefunds(page = 1, pageSize = 100): Promise<Paged<PendingRefund>> {
  return apiRequest<Paged<PendingRefund>>(API_ENDPOINTS.refunds.pending, { query: { page, pageSize } })
}

/** Tải CSV các khoản đang chờ hoàn; server ghi dấu "đã xuất" nhưng cho xuất lại. */
export function exportPendingRefundsCsv(tripId?: string): Promise<DownloadedFile> {
  return apiRequest<DownloadedFile>(API_ENDPOINTS.refunds.exportCsv, {
    method: 'POST',
    body: { tripId },
    responseType: 'download',
  })
}

/** Xác nhận đã hoàn nhiều khoản (1–100); khoản lỗi nằm ở `items[].error`, không làm mất khoản đã xác nhận. */
export function confirmRefundsBatch(items: ManualRefundInput[]): Promise<BatchConfirmResult> {
  return apiRequest<BatchConfirmResult>(API_ENDPOINTS.refunds.confirmManualBatch, {
    method: 'POST',
    body: { items },
  })
}
