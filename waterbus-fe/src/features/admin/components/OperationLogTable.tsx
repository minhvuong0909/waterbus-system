import { useState } from 'react'
import { DataTable, FilterBar, SelectField } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import type { AdminTrip, OperationLog } from '../../operations/types'
import { OPERATION_LABEL } from '../../operations/utils'

/** Nhật ký thao tác vận hành (mỗi lần đổi trạng thái chuyến là một dòng `TripOperationEvent`). */
export default function OperationLogTable({ logs, trips }: { logs: OperationLog[]; trips: AdminTrip[] }) {
  const byId = new Map(trips.map((trip) => [trip.id, trip]))
  // Chuyến ngoài cửa sổ vận hành (đã qua lâu) không có trong `trips` nên hiện "—".
  const tripLabel = (id: string | null) => {
    const trip = id ? byId.get(id) : undefined
    if (!trip) return '—'
    return (
      <>
        <span className="block">{`${trip.boatCode ?? '—'} • ${formatDateTime(trip.departureTime)}`}</span>
        <span className="block text-xs text-on-surface-variant">{trip.routeName}</span>
      </>
    )
  }
  const [type, setType] = useState('')
  const shown = type ? logs.filter((l) => l.type === type) : logs
  const types = [...new Set(logs.map((l) => l.type))]

  const columns: Column<OperationLog>[] = [
    { header: 'Time', render: (l) => formatDateTime(l.at) },
    {
      header: 'Operation',
      render: (l) => (
        <span className={l.type === 'SOS' ? 'font-bold text-coral-glow' : 'font-semibold'}>{t(OPERATION_LABEL[l.type])}</span>
      ),
    },
    { header: 'Trip', render: (l) => tripLabel(l.tripId) },
    { header: 'Reason', render: (l) => l.reason },
    { header: 'By', render: (l) => l.by },
  ]

  return (
    <>
      <FilterBar>
        <SelectField label="Operation" value={type} onChange={(e) => setType(e.target.value)}>
          <option value="">{t('All')}</option>
          {types.map((x) => (
            <option key={x} value={x}>
              {t(OPERATION_LABEL[x])}
            </option>
          ))}
        </SelectField>
      </FilterBar>
      <DataTable paginate columns={columns} rows={shown} rowKey={(l) => l.id} empty="Nothing has been done yet." />
    </>
  )
}
