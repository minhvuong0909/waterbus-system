/** Phần tiền hoàn của một Ticket (BE `RefundTicketAllocationDto`). */
export interface RefundTicketAllocation {
  ticketId: string
  ticketCode: string
  amount: number
  reason: string
}

/** Kết quả `POST /refunds/request` (BE `RequestRefundResultDto`). */
export interface RefundRequestResult {
  refundTransactionId: string
  orderId: string
  bookingId: string
  amount: number
  currency: string
  status: string
  gateway: string
  /** Cổng thanh toán không hoàn được: Admin phải hoàn tay rồi xác nhận kèm bằng chứng (BR-RF-10). */
  requiresManualAction: boolean
  tickets: RefundTicketAllocation[]
  /** Chế độ Demo: hoàn tiền mô phỏng, KHÔNG có tiền thật được chuyển. */
  isDemo?: boolean
  notice?: string | null
}

/** Body of `POST /refunds/confirm-manual` (BE `ConfirmManualRefundRequest`). */
export interface ManualRefundInput {
  refundTransactionId: string
  evidence: string
  gatewayReference: string | null
  confirmedAmount: number
}

/** Kết quả `POST /refunds/confirm-manual` (BE `ConfirmManualRefundResultDto`). */
export interface ManualRefundResult {
  refundTransactionId: string
  amount: number
  status: string
  completedAt: string | null
  isDemo?: boolean
  notice?: string | null
}

/** Trang kết quả của BE (`PaginatedList<T>`). */
export interface Paged<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  totalPages: number
}

/** Booking của chuyến bị huỷ/chấm dứt cần hoàn (BE `RefundCandidateResponse`). */
export interface RefundCandidate {
  bookingId: string
  bookingCode: string
  orderId: string
  purchaserName: string
  purchaserEmail: string
  purchaserPhone: string | null
  amount: number
  currency: string
  ticketCodes: string[]
  reason: string
}

/** Kết quả `POST /refunds/batch-request` (BE `BatchRefundRequestResultResponse`). */
export interface BatchRefundRequestResult {
  tripId: string
  requested: number
  skipped: number
  created: { bookingId: string; bookingCode: string; refundTransactionId: string; amount: number; status: string }[]
  skippedItems: { bookingId: string; bookingCode: string; reason: string }[]
}

/** Khoản hoàn Manual đang chờ nhân viên hoàn (BE `PendingManualRefund`). */
export interface PendingRefund {
  refundId: string
  bookingCode: string
  orderId: string
  purchaserName: string
  purchaserEmail: string
  purchaserPhone: string | null
  amount: number
  currency: string
  ticketCodes: string[]
  routeName: string | null
  departureTime: string | null
  /** Tiền về sau khi đơn hết hạn (không có vé), không phải do chuyến bị huỷ. */
  isLatePayment: boolean
  tripStatus: string | null
  merchantReference: string | null
  paymentGatewayReference: string | null
  paidAt: string | null
  status: string
  requestedAt: string
  lastExportedAt: string | null
}

/** Kết quả `POST /refunds/confirm-manual/batch` (BE `BatchConfirmManualRefundsResponse`). */
export interface BatchConfirmResult {
  confirmed: number
  failed: number
  items: { refundTransactionId: string; succeeded: boolean; status: string | null; error: string | null }[]
}
