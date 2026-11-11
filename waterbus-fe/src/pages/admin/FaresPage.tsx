import { DataTable, ErrorState, PageHeader, PageLoader } from '../../components/ui'
import { getFares } from '../../features/trips/services/fareService'
import type { FareDto } from '../../features/trips/services/fareService'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatVnd } from '../../utils/format'
import type { Column } from '../../components/ui'

const columns: Column<FareDto>[] = [
  { header: 'Trip type', render: (f) => t(f.tripType === 'Commuter' ? 'Regular' : f.tripType) },
  { header: 'Seat class', render: (f) => f.seatClassName },
  { header: 'Price', render: (f) => formatVnd(f.price), className: 'text-right' },
]

/** Active fares (BR-FARE-01: price depends on trip type and seat class). Read only: the backend has no fare editing API. */
export default function FaresPage() {
  useDocumentTitle('Fares')
  const fares = useFetch(getFares)

  if (fares.error) return <ErrorState onRetry={fares.retry} />
  if (!fares.data) return <PageLoader />

  const rows = [...fares.data].sort((a, b) => a.tripType.localeCompare(b.tripType) || a.price - b.price)
  return (
    <>
      <PageHeader title="Fares" description="Active fares per trip type and seat class." />
      <DataTable columns={columns} rows={rows} rowKey={(f) => `${f.tripType}-${f.seatClassId}`} empty="No active fares." />
    </>
  )
}
