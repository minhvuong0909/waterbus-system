import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { ManualRefundInput, ManualRefundResult, RefundRequestResult } from '../types'

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
