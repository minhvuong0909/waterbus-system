import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Button, DataTable, ErrorState, FilterBar, Modal, Notice, PageHeader, PageLoader, SelectField, TextField } from '../../components/ui'
import AdHocTripForm from '../../features/admin/components/AdHocTripForm'
import AssignBoatForm from '../../features/admin/components/AssignBoatForm'
import OperationForm from '../../features/admin/components/OperationForm'
import StatusBadge from '../../features/admin/components/StatusBadge'
import { getBoats } from '../../features/operations/services/fleetService'
import { getOperationTrips } from '../../features/operations/services/operationsService'
import { getRoutes } from '../../features/admin/services/routeService'
import type { AdminTrip, OperationType } from '../../features/operations/types'
import { DISPATCH_OPERATIONS, OPERATION_LABEL } from '../../features/operations/utils'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { combineFetches, useDocumentTitle, useFetch, useNow, useReload } from '../../hooks'
import { t } from '../../i18n'
import { formatDateTime, toVietnamDate } from '../../utils/time'
import type { TripStatus } from '../../features/trips/types'
import type { Column } from '../../components/ui'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'
import { isWithoutBoat } from '../../features/admin/utils/attention'

const STATUSES: TripStatus[] = [
  'Scheduled',
  'Boarding',
  'EnRoute',
  'Suspended',
  'Arrived',
  'Completed',
  'Cancelled',
  'Terminated',
]

const NO_BOAT = 'no-boat'
type TripFilter = 'all' | typeof NO_BOAT | TripStatus

/** Giá trị `?filter=` hợp lệ từ liên kết ngoài (Tổng quan): chưa có tàu hoặc một trạng thái chuyến. */
const filterFromUrl = (value: string | null): TripFilter =>
  value === NO_BOAT ? NO_BOAT : STATUSES.find((status) => status === value) ?? 'all'

type Dialog =
  | { kind: 'actions'; trip: AdminTrip }
  | { kind: 'boat'; trip: AdminTrip }
  | { kind: 'operation'; trip: AdminTrip; type: OperationType }
  | { kind: 'trip' }
  | null

/** Thao tác vận hành Admin làm được với chuyến lúc này (theo `allowedOperations` của backend). */
const operationsOf = (trip: AdminTrip): OperationType[] =>
  DISPATCH_OPERATIONS.filter((op) => trip.allowedOperations.includes(op))

/** Chỉ đổi được tàu khi chuyến chưa mở đón khách (BR-SCH-04). */
const canChangeBoat = (trip: AdminTrip) => trip.status === 'Scheduled'

export default function TripsPage() {
  useDocumentTitle('Trips')
  const [version, reload] = useReload()
  // Để trống: cửa sổ mặc định (12 giờ trước đến 7 ngày tới). Chọn một ngày: 7 ngày tính từ ngày đó, theo giờ Việt Nam.
  const [weekFrom, setWeekFrom] = useState('')
  const trips = useFetch(
    () => getOperationTrips(weekFrom ? { from: new Date(`${weekFrom}T00:00:00+07:00`).toISOString() } : undefined),
    [weekFrom, version],
  )
  const boats = useFetch(getBoats)
  const routes = useFetch(() => getRoutes(true))
  // Chuyến đổi trạng thái (thuyền trưởng xuất bến, Admin tạm ngưng…) thì bảng đổi theo, không cần tải lại trang.
  useFleetHub({ onTripStatus: reload, onReconnected: reload })
  // Mốc "bây giờ" để đánh dấu chuyến theo lịch đã quá giờ khởi hành.
  const now = useNow(60_000)
  // `?filter=` (liên kết từ Tổng quan): `no-boat` là chuyến chưa có tàu, tức chưa mở bán; hoặc một trạng thái chuyến.
  const [searchParams] = useSearchParams()
  const [filter, setFilter] = useState<TripFilter>(() => filterFromUrl(searchParams.get('filter')))
  const [routeFilter, setRouteFilter] = useState('')
  const [boatFilter, setBoatFilter] = useState('')
  const [dialog, setDialog] = useState<Dialog>(null)
  const [movedSeats, setMovedSeats] = useState(0)

  const all = combineFetches(trips, boats, routes)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!trips.data || !boats.data || !routes.data) return <PageLoader />

  const rows = trips.data.filter(
    (trip) =>
      (filter === 'all' ? true : filter === NO_BOAT ? isWithoutBoat(trip) : trip.status === filter) &&
      (routeFilter === '' || trip.routeId === routeFilter) &&
      (boatFilter === '' || trip.boatId === boatFilter),
  )

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime), className: 'whitespace-nowrap' },
    { header: 'Route', render: (x) => x.routeName },
    {
      header: 'Boat',
      render: (x) =>
        x.boatCode ? (
          <span className="font-mono font-semibold">{x.boatCode}</span>
        ) : x.status === 'Scheduled' ? (
          // Chưa có tàu thì chuyến chưa mở bán (BR-SCH-04).
          <StatusBadge tone="upcoming" label="No boat" />
        ) : (
          '—'
        ),
    },
    { header: 'Type', render: (x) => t(x.tripType === 'Commuter' ? 'Regular' : 'Sightseeing') },
    {
      header: 'Status',
      render: (x) => (
        <span className="inline-flex flex-wrap items-center gap-1.5">
          <TripStatusBadge status={x.status} />
          {/* Chuyến theo lịch mà đã quá giờ khởi hành: chưa ai bắt đầu hay xử lý, Admin cần xem. */}
          {x.status === 'Scheduled' && Date.parse(x.departureTime) < now && (
            <StatusBadge tone="upcoming" label="Past departure time" />
          )}
        </span>
      ),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) =>
        // Chuyến chưa có tàu: nút gán tàu là việc cần làm ngay; còn lại gom vào một nút "Thao tác".
        x.status === 'Scheduled' && !x.boatId ? (
          <Button size="sm" variant="secondary" onClick={() => {
            setMovedSeats(0)
            setDialog({ kind: 'boat', trip: x })
          }}>
            {t('Assign boat')}
          </Button>
        ) : (
          (canChangeBoat(x) || operationsOf(x).length > 0) && (
            <Button size="sm" variant="neutral" onClick={() => setDialog({ kind: 'actions', trip: x })}>
              {t('Actions')}
            </Button>
          )
        ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Trips"
        description="Seven days of trips: assign a boat, then hold, resume, cancel or close a trip. A trip is on sale once it has a boat."
        actions={
          <Button
            size="sm"
            icon="add"
            disabled={routes.data.length === 0}
            onClick={() => setDialog({ kind: 'trip' })}
          >
            {t('Add trip')}
          </Button>
        }
      />
      {movedSeats > 0 && (
        <div className="mb-3">
          <Notice tone="success">
            {t('Boat changed. {count} sold seats were moved to the new boat.', { count: movedSeats })}
          </Notice>
        </div>
      )}
      <FilterBar>
        <TextField label="From" type="date" value={weekFrom} onChange={(e) => setWeekFrom(e.target.value)} />
        <SelectField label="Status" value={filter} onChange={(e) => setFilter(e.target.value as TripFilter)}>
          <option value="all">{t('All')}</option>
          <option value={NO_BOAT}>{t('No boat (not on sale)')}</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {t(s === 'EnRoute' ? 'In transit' : s)}
            </option>
          ))}
        </SelectField>
        <SelectField label="Route" value={routeFilter} onChange={(e) => setRouteFilter(e.target.value)}>
          <option value="">{t('All routes')}</option>
          {routes.data.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </SelectField>
        <SelectField label="Boat" value={boatFilter} onChange={(e) => setBoatFilter(e.target.value)}>
          <option value="">{t('All boats')}</option>
          {boats.data.map((b) => (
            <option key={b.id} value={b.id}>
              {b.code}
            </option>
          ))}
        </SelectField>
      </FilterBar>
      <DataTable paginate columns={columns} rows={rows} rowKey={(x) => x.id} empty="No trips match this filter." />

      {dialog?.kind === 'actions' && (
        <Modal title="Trip actions" onClose={() => setDialog(null)}>
          <p className="mb-4 text-sm text-on-surface-variant">
            {dialog.trip.routeName} · {formatDateTime(dialog.trip.departureTime)}
            {dialog.trip.boatCode && <> · <span className="font-mono font-semibold text-deep-river">{dialog.trip.boatCode}</span></>}
          </p>
          <div className="grid gap-2">
            {canChangeBoat(dialog.trip) && (
              <Button variant="neutral" icon="sailing" className="justify-start" onClick={() => {
                setMovedSeats(0)
                setDialog({ kind: 'boat', trip: dialog.trip })
              }}>
                {t('Change boat')}
              </Button>
            )}
            {operationsOf(dialog.trip).map((op) => (
              <Button
                key={op}
                variant={op === 'Cancel' || op === 'Terminate' ? 'danger' : 'neutral'}
                className="justify-start"
                onClick={() => setDialog({ kind: 'operation', trip: dialog.trip, type: op })}
              >
                {t(OPERATION_LABEL[op])}
              </Button>
            ))}
          </div>
          <p className="mt-4 text-xs text-on-surface-variant">
            {t('To answer a weather alert for this trip, decide on the Incidents page so the decision is recorded against the alert.')}
          </p>
        </Modal>
      )}
      {dialog?.kind === 'operation' && (
        <Modal title={OPERATION_LABEL[dialog.type]} onClose={() => setDialog(null)}>
          <OperationForm
            trips={[dialog.trip]}
            type={dialog.type}
            onCancel={() => setDialog({ kind: 'actions', trip: dialog.trip })}
            onDone={() => {
              setDialog(null)
              reload()
            }}
          />
        </Modal>
      )}
      {dialog?.kind === 'boat' && (
        <Modal title={dialog.trip.boatId ? 'Change boat' : 'Assign boat'} onClose={() => setDialog(null)}>
          <AssignBoatForm
            trip={dialog.trip}
            boats={boats.data}
            onDone={(result) => {
              setMovedSeats(result.remappedReservations)
              setDialog(null)
              reload()
            }}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'trip' && (
        <Modal title="Add trip" onClose={() => setDialog(null)}>
          <AdHocTripForm
            routes={routes.data}
            onDone={(departureTime) => {
              // Hiện tuần của chuyến vừa tạo để gán tàu được ngay.
              setWeekFrom(toVietnamDate(new Date(departureTime)))
              setFilter('all')
              setDialog(null)
              reload()
            }}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
    </>
  )
}
