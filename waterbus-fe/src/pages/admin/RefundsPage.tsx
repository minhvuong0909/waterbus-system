import { useState } from 'react'
import { PageHeader } from '../../components/ui'
import PendingRefundsPanel from '../../features/admin/components/PendingRefundsPanel'
import RefundReconcilePanel from '../../features/admin/components/RefundReconcilePanel'
import RefundRequestForm from '../../features/admin/components/RefundRequestForm'
import RefundResultPanel from '../../features/admin/components/RefundResultPanel'
import RefundTripPanel from '../../features/admin/components/RefundTripPanel'
import type { RefundRequestResult } from '../../features/admin/types'
import { useDocumentTitle } from '../../hooks'

/**
 * Admin hoàn tiền Booking của chuyến bị huỷ/chấm dứt (BR-ROLE-04, BR-RF-07). Hệ thống không tự chuyển tiền:
 * Admin tạo yêu cầu theo chuyến, tải CSV cho nhân viên hoàn qua cổng merchant, rồi xác nhận kèm bằng chứng (BR-RF-10).
 * Form hoàn một Booking dùng cho trường hợp lẻ, vd tiền về muộn không có vé.
 */
export default function RefundsPage() {
  useDocumentTitle('Refunds')
  const [refund, setRefund] = useState<RefundRequestResult | null>(null)
  const refundId = refund?.refundTransactionId
  // Đổi khi có yêu cầu hoàn mới để danh sách chờ hoàn tải lại.
  const [refreshKey, setRefreshKey] = useState(0)
  const refresh = () => setRefreshKey((key) => key + 1)

  return (
    <>
      <PageHeader title="Refunds" description="Refund a booking of a cancelled or terminated trip." />
      <RefundTripPanel onCreated={refresh} />
      <PendingRefundsPanel refreshKey={refreshKey} />
      <div className="mt-4">
        <RefundRequestForm
          onCreated={(created) => {
            setRefund(created)
            refresh()
          }}
        />
      </div>
      {refund && <RefundResultPanel refund={refund} />}
      {/* key: điền sẵn mã hoàn tiền vừa tạo khi có kết quả mới. */}
      <RefundReconcilePanel key={refundId ?? 'none'} defaultId={refundId} />
    </>
  )
}
