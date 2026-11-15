import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PageHeader, Tabs } from '../../components/ui'
import type { TabItem } from '../../components/ui'
import PendingRefundsPanel from '../../features/admin/components/PendingRefundsPanel'
import ReconciliationPanel from '../../features/admin/components/ReconciliationPanel'
import RefundResultPanel from '../../features/admin/components/RefundResultPanel'
import RefundTripPanel from '../../features/admin/components/RefundTripPanel'
import { countReconciliationToDo, listPendingRefunds } from '../../features/admin/services/refundService'
import type { RefundRequestResult } from '../../features/admin/types'
import { useDocumentTitle, useFetch } from '../../hooks'

type RefundTab = 'trip' | 'pending' | 'reconcile'
const TAB_KEYS: RefundTab[] = ['trip', 'pending', 'reconcile']

/** Số dòng của một danh sách; `undefined` khi không đọc được (tab vẫn dùng được, chỉ không có số). */
const countOf = (load: () => Promise<{ totalCount: number }>) => () =>
  load()
    .then((page) => page.totalCount)
    .catch(() => undefined)

/**
 * Admin hoàn tiền (BR-ROLE-04, BR-RF-07, BR-RF-10), chia theo bước: (1) tạo yêu cầu cho chuyến bị huỷ/chấm dứt,
 * (2) tải CSV cho nhân viên hoàn qua cổng merchant rồi xác nhận kèm bằng chứng, và riêng các khoản cần đối soát
 * (tiền về muộn, lệnh hoàn VNPay chưa có kết quả). Tab đang mở nằm trên URL (`?tab=`) để Tổng quan liên kết thẳng tới.
 */
export default function RefundsPage() {
  useDocumentTitle('Refunds')
  const [searchParams, setSearchParams] = useSearchParams()
  const requested = searchParams.get('tab') as RefundTab | null
  const tab: RefundTab = requested && TAB_KEYS.includes(requested) ? requested : 'trip'
  const [refund, setRefund] = useState<RefundRequestResult | null>(null)
  // Đổi khi có yêu cầu hoàn mới để các danh sách và số trên tab tải lại.
  const [refreshKey, setRefreshKey] = useState(0)
  const refresh = () => setRefreshKey((key) => key + 1)
  const pendingCount = useFetch(countOf(() => listPendingRefunds(1, 1)), [refreshKey])
  // Chỉ đếm khoản Admin còn phải làm (xem `countReconciliationToDo`).
  const reconcileCount = useFetch(() => countReconciliationToDo().catch(() => undefined), [refreshKey])

  const tabs: TabItem<RefundTab>[] = [
    { key: 'trip', label: '1. Request for a trip' },
    { key: 'pending', label: '2. Refund and confirm', count: pendingCount.data },
    { key: 'reconcile', label: 'Needs reconciling', count: reconcileCount.data },
  ]

  return (
    <>
      <PageHeader
        title="Refunds"
        description="Request refunds for a cancelled or terminated trip, hand them to staff with the CSV, then confirm with evidence."
      />
      <Tabs
        label="Refund steps"
        tabs={tabs}
        value={tab}
        onChange={(next) => setSearchParams(next === 'trip' ? {} : { tab: next }, { replace: true })}
      >
        {tab === 'trip' && <RefundTripPanel onCreated={refresh} />}
        {tab === 'pending' && <PendingRefundsPanel refreshKey={refreshKey} />}
        {tab === 'reconcile' && (
          <>
            <ReconciliationPanel
              refreshKey={refreshKey}
              onRefundCreated={(created) => {
                setRefund(created)
                refresh()
              }}
            />
            {refund && <RefundResultPanel refund={refund} />}
          </>
        )}
      </Tabs>
    </>
  )
}
