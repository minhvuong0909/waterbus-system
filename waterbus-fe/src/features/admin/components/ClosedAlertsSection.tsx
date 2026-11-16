import { useState } from 'react'
import { ErrorState, Notice, PageLoader, Pagination } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { getClosedIncidents } from '../../operations/services/operationsService'
import AlertCard from './AlertCard'

const PAGE_SIZE = 10

/** Sự cố đã đóng, mới nhất trước, chỉ xem: Admin xem lại quyết định đã ghi cho từng chuyến. Phân trang ở server. */
export default function ClosedAlertsSection() {
  const [page, setPage] = useState(1)
  const closed = useFetch(() => getClosedIncidents(page, PAGE_SIZE), [page])

  if (closed.error) return <ErrorState compact onRetry={closed.retry} />
  if (!closed.data) return <PageLoader />

  const totalPages = Math.max(1, Math.ceil(closed.data.totalCount / PAGE_SIZE))
  return (
    <section className="space-y-3" aria-label={t('Closed alerts')}>
      {closed.data.items.length === 0 ? (
        <Notice tone="info">{t('No incident has been closed yet.')}</Notice>
      ) : (
        closed.data.items.map((alert) => <AlertCard key={alert.id} alert={alert} trips={[]} version={0} readOnly />)
      )}
      <Pagination pageNumber={page} totalPages={totalPages} onChange={setPage} />
    </section>
  )
}
