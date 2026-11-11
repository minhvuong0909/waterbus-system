import { useState } from 'react'
import { PageHeader } from '../../components/ui'
import RefundReconcilePanel from '../../features/admin/components/RefundReconcilePanel'
import RefundRequestForm from '../../features/admin/components/RefundRequestForm'
import RefundResultPanel from '../../features/admin/components/RefundResultPanel'
import type { RefundRequestResult } from '../../features/admin/types'
import { useDocumentTitle } from '../../hooks'

/**
 * Admin hoàn tiền Booking của chuyến bị huỷ/chấm dứt (BR-ROLE-04, BR-RF-07). Khi cổng thanh toán
 * không hoàn được, Admin hoàn tay rồi xác nhận kèm bằng chứng (BR-RF-10).
 */
export default function RefundsPage() {
  useDocumentTitle('Refunds')
  const [refund, setRefund] = useState<RefundRequestResult | null>(null)
  const refundId = refund?.refundTransactionId

  return (
    <>
      <PageHeader title="Refunds" description="Refund a booking of a cancelled or terminated trip." />
      <RefundRequestForm onCreated={setRefund} />
      {refund && <RefundResultPanel refund={refund} />}
      {/* key: điền sẵn mã hoàn tiền vừa tạo khi có kết quả mới. */}
      <RefundReconcilePanel key={refundId ?? 'none'} defaultId={refundId} />
    </>
  )
}
