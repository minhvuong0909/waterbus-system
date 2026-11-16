import { useState } from 'react'
import { Button, DataTable, ErrorState, PageLoader, Pagination } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import { listCaptainAlerts, resolveCaptainAlert } from '../services/personnelService'
import type { CaptainAlert } from '../types/personnel'
import ConfirmModal from './ConfirmModal'
import StatusBadge from './StatusBadge'

/** Cảnh báo Captain chưa có mặt hoặc hồ sơ cần xử lý (BR-CAP-10). Xử lý xong mới đánh dấu; không đổi trạng thái chuyến. */
export default function CaptainAlertsPanel({ captainNames, boatCodes }: { captainNames: Map<string, string>; boatCodes: Map<string, string> }) {
  const [page, setPage] = useState(1)
  const [resolving, setResolving] = useState<CaptainAlert | null>(null)
  const alerts = useFetch(() => listCaptainAlerts(page), [page])

  if (alerts.error) return <ErrorState onRetry={alerts.retry} />
  if (alerts.loading || !alerts.data) return <PageLoader />

  const columns: Column<CaptainAlert>[] = [
    { header: 'Time', render: (a) => formatDateTime(a.occurredAt), className: 'whitespace-nowrap' },
    { header: 'Boat', render: (a) => <span className="font-mono font-semibold">{boatCodes.get(a.boatId) ?? '—'}</span> },
    { header: 'Captain', render: (a) => (a.captainId ? captainNames.get(a.captainId) ?? '—' : '—') },
    { header: 'Alert', render: (a) => t(a.type) },
    { header: 'Details', render: (a) => <span className="text-on-surface-variant">{t(a.message)}</span> },
    {
      header: 'Status',
      render: (a) => (a.resolvedAt ? <StatusBadge tone="muted" label="Resolved" /> : <StatusBadge tone="upcoming" label="Open" />),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (a) =>
        !a.resolvedAt && (
          <Button size="sm" variant="neutral" icon="check_circle" onClick={() => setResolving(a)}>
            {t('Mark resolved')}
          </Button>
        ),
    },
  ]

  return (
    <>
      <DataTable columns={columns} rows={alerts.data.items} rowKey={(a) => a.id} empty="No captain alerts." />
      <div className="mt-3">
        <Pagination pageNumber={alerts.data.pageNumber} totalPages={alerts.data.totalPages} onChange={setPage} />
      </div>
      {resolving && (
        <ConfirmModal
          title="Resolve alert"
          message={t('Mark this alert as resolved? Do this only after the issue has been handled; the trip itself is not changed.')}
          confirmLabel="Mark resolved"
          tone="primary"
          action={() => resolveCaptainAlert(resolving.id)}
          onDone={() => { setResolving(null); alerts.retry() }}
          onClose={() => setResolving(null)}
        />
      )}
    </>
  )
}
