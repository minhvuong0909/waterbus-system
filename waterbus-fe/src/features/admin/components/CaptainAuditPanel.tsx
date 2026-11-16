import { useState } from 'react'
import { DataTable, ErrorState, PageLoader, Pagination } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import { listCaptainAudit } from '../services/personnelService'
import type { CaptainAuditEvent } from '../types/personnel'

/** Nhật ký mọi thay đổi với Captain: ai làm, khi nào, lý do (BR-CAP-07). Captain tự kích hoạt được ghi đúng là Captain, không phải Admin. */
export default function CaptainAuditPanel({ captainNames }: { captainNames: Map<string, string> }) {
  const [page, setPage] = useState(1)
  const audit = useFetch(() => listCaptainAudit(page), [page])

  if (audit.error) return <ErrorState onRetry={audit.retry} />
  if (audit.loading || !audit.data) return <PageLoader />

  const actor = (event: CaptainAuditEvent) =>
    event.actedByAdminId ? t('Admin') : event.actedByCaptainId ? t('The Captain') : '—'

  const columns: Column<CaptainAuditEvent>[] = [
    { header: 'Time', render: (e) => formatDateTime(e.occurredAt), className: 'whitespace-nowrap' },
    { header: 'Captain', render: (e) => captainNames.get(e.captainId) ?? '—' },
    { header: 'Action', render: (e) => t(e.action) },
    { header: 'Done by', render: actor },
    { header: 'Reason', render: (e) => <span className="text-on-surface-variant">{t(e.reason)}</span> },
  ]

  return (
    <>
      <DataTable columns={columns} rows={audit.data.items} rowKey={(e) => e.id} empty="No captain activity yet." />
      <div className="mt-3">
        <Pagination pageNumber={audit.data.pageNumber} totalPages={audit.data.totalPages} onChange={setPage} />
      </div>
    </>
  )
}
