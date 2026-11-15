import { useState } from 'react'
import { FilterChips, Notice, Pagination } from '../../../components/ui'
import { t } from '../../../i18n'
import { INCIDENT_KINDS } from '../../operations/services/operationsService'
import type { AdminTrip, IncidentAlert, OperationType } from '../../operations/types'
import AlertCard from './AlertCard'
import AlertsEmpty from './AlertsEmpty'

const PAGE_SIZE = 10

/** Các sự cố đang mở (đã sắp xếp), lọc theo loại và chia trang trên trình duyệt. */
export default function OpenAlertsSection({
  alerts,
  trips,
  version,
  onClose,
  onDecide,
}: {
  alerts: IncidentAlert[]
  trips: AdminTrip[]
  version: number
  onClose: (alert: IncidentAlert) => void
  onDecide: (trips: AdminTrip[], type: OperationType, incidentId: string) => void
}) {
  const [kind, setKind] = useState('')
  const [page, setPage] = useState(1)

  const filtered = kind ? alerts.filter((alert) => alert.kind === kind) : alerts
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  // Khi sự cố được đóng làm số trang giảm, đứng ở trang cuối còn lại.
  const current = Math.min(page, totalPages)
  const shown = filtered.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE)

  return (
    <section className="space-y-3" aria-label={t('Open alerts')}>
      {alerts.length > 0 && (
        <FilterChips
          label="Incident type"
          value={kind}
          onChange={(value) => {
            setKind(value)
            setPage(1)
          }}
          options={[{ value: '', label: 'All types' }, ...INCIDENT_KINDS.map((k) => ({ value: k, label: k }))]}
        />
      )}
      {alerts.length === 0 ? (
        <AlertsEmpty />
      ) : filtered.length === 0 ? (
        <Notice tone="info">{t('No open incidents match this filter.')}</Notice>
      ) : (
        shown.map((alert) => (
          <AlertCard key={alert.id} alert={alert} trips={trips} version={version} onClose={onClose} onDecide={onDecide} />
        ))
      )}
      <Pagination pageNumber={current} totalPages={totalPages} onChange={setPage} />
    </section>
  )
}
