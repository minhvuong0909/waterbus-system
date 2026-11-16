import { useState } from 'react'
import { ErrorState, FilterChips, Modal, Notice, PageHeader, PageLoader, SectionHeader } from '../../components/ui'
import ClosedAlertsSection from '../../features/admin/components/ClosedAlertsSection'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import OpenAlertsSection from '../../features/admin/components/OpenAlertsSection'
import OperationForm from '../../features/admin/components/OperationForm'
import OperationLogTable from '../../features/admin/components/OperationLogTable'
import { useIncidentBoard } from '../../features/admin/hooks/useIncidentBoard'
import { pendingCountOf } from '../../features/admin/utils/alertUtils'
import { resolveIncident } from '../../features/operations/services/operationsService'
import type { AdminTrip, IncidentAlert, OperationType } from '../../features/operations/types'
import { OPERATION_LABEL } from '../../features/operations/utils'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

/**
 * Admin xem các sự cố đang mở và quyết định cho từng chuyến bị ảnh hưởng (BR v2.0: sự cố không tự đổi trạng thái
 * chuyến). Quyết định được ghi vào liên kết sự cố - chuyến; thao tác thường ngày của chuyến nằm ở trang Chuyến.
 */
export default function IncidentsPage() {
  useDocumentTitle('Incidents')
  const board = useIncidentBoard()
  const [pending, setPending] = useState<{ trips: AdminTrip[]; type: OperationType; incidentId: string } | null>(null)
  const [confirmClose, setConfirmClose] = useState<IncidentAlert | null>(null)
  const [view, setView] = useState<'open' | 'closed'>('open')

  if (board.state.error) return <ErrorState onRetry={board.state.retry} />
  if (board.state.loading || !board.trips || !board.logs || !board.alerts) return <PageLoader />

  return (
    <>
      <PageHeader
        title="Incidents"
        description="Decide for each affected trip. An alert never changes a trip by itself; routine trip operations are on the Trips page."
      />

      <FilterChips
        label="Show"
        value={view}
        onChange={(value) => setView(value === 'closed' ? 'closed' : 'open')}
        options={[
          { value: 'open', label: 'Open alerts' },
          { value: 'closed', label: 'Closed alerts' },
        ]}
      />

      {view === 'open' ? (
        <>
          <p className="mb-4 mt-3 text-sm text-on-surface-variant">
            {t('{alerts} open alerts', { alerts: board.alerts.length })}
            {' · '}
            <span className={board.waitingTrips > 0 ? 'font-semibold text-coral-glow' : ''}>
              {t('{count} trip decisions waiting', { count: board.waitingTrips })}
            </span>
          </p>

          <div>
            <OpenAlertsSection
              alerts={board.alerts}
              trips={board.trips}
              version={board.version}
              onClose={setConfirmClose}
              onDecide={(trips, type, incidentId) => setPending({ trips, type, incidentId })}
            />
          </div>
        </>
      ) : (
        <div className="mt-3">
          <ClosedAlertsSection />
        </div>
      )}

      <SectionHeader title="Operation log" className="mb-3 mt-8" />
      <OperationLogTable logs={board.logs} trips={board.trips} />

      {confirmClose && (
        <ConfirmModal
          title="Close this alert?"
          message={t('Closing only ends the alert: no trip changes, and every trip keeps its current status.')}
          confirmLabel="Close alert"
          tone="primary"
          action={() => resolveIncident(confirmClose.id)}
          onClose={() => setConfirmClose(null)}
          onDone={() => {
            setConfirmClose(null)
            board.reloadAlerts()
          }}
        >
          {pendingCountOf(confirmClose) > 0 && (
            <Notice tone="warning">
              {t('{count} trips still have no decision.', { count: pendingCountOf(confirmClose) })}
            </Notice>
          )}
        </ConfirmModal>
      )}

      {pending && (
        <Modal title={OPERATION_LABEL[pending.type]} onClose={() => setPending(null)}>
          <OperationForm
            trips={pending.trips}
            type={pending.type}
            incidentId={pending.incidentId}
            onCancel={() => setPending(null)}
            onDone={() => {
              setPending(null)
              board.reloadAll()
            }}
          />
        </Modal>
      )}
    </>
  )
}
