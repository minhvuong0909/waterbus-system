import { useState } from 'react'
import { Button, ErrorState, Modal, Notice, PageLoader } from '../../components/ui'
import BigButton from '../../features/captain/components/BigButton'
import GpsSharePanel from '../../features/captain/components/GpsSharePanel'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { getOperationTrips, operateTrip } from '../../features/operations/services/operationsService'
import type { AdminTrip, OperationType } from '../../features/operations/types'
import { isUpcomingOrRunning } from '../../features/operations/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatClock, formatDateTime } from '../../utils/time'

type CaptainAction = Extract<OperationType, 'OpenBoarding' | 'Start' | 'Arrive'>

/** Việc thuyền trưởng làm được theo thứ tự chuyến đi; backend quyết định việc nào đang được phép (`allowedOperations`). */
const ACTIONS: { type: CaptainAction; icon: string; label: string; tone: 'teal' | 'dark'; reason: string }[] = [
  { type: 'OpenBoarding', icon: 'groups', label: 'Open boarding', tone: 'dark', reason: 'Boarding opened by the captain.' },
  { type: 'Start', icon: 'play_arrow', label: 'Start trip', tone: 'teal', reason: 'Started by the captain.' },
  { type: 'Arrive', icon: 'anchor', label: 'Arrived', tone: 'dark', reason: 'Arrived at the pier.' },
]

/** Gợi ý việc tiếp theo theo trạng thái chuyến (chuỗi nguồn tiếng Anh). */
const NEXT_STEP: Partial<Record<AdminTrip['status'], string>> = {
  Scheduled: 'Open boarding when passengers start to board, then start the trip when the boat leaves the pier.',
  Boarding: 'Start the trip when the boat leaves the pier.',
  EnRoute: 'Press Arrived when the boat reaches the last pier.',
}

export default function CaptainPortalPage() {
  useDocumentTitle('Captain Portal')
  const { user } = useAuth()
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [armed, setArmed] = useState<CaptainAction | null>(null)
  const [sosOpen, setSosOpen] = useState(false)
  const [note, setNote] = useState('')
  const [sosSent, setSosSent] = useState(false)
  const action = useSubmit()
  // Thuyền trưởng theo dõi chính tàu của mình: Admin đổi trạng thái chuyến thì màn hình cập nhật ngay.
  const boatIds = [...new Set((trips.data ?? []).map((x) => x.boatId).filter(Boolean))]
  useFleetHub({ onTripStatus: reload, onReconnected: reload }, boatIds.length > 0, boatIds)

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  const open = trips.data.filter((x) => isUpcomingOrRunning(x.status))
  // Mặc định là chuyến đang chạy trên sông, nếu không có thì chuyến sắp tới.
  const trip: AdminTrip | undefined =
    open.find((x) => x.id === selectedId) ?? open.find((x) => x.status === 'EnRoute') ?? open[0]

  const perform = async (current: AdminTrip, type: OperationType, reason: string) => {
    const done = await action.run(() => operateTrip(current.id, type, reason))
    if (done) reload()
    return Boolean(done)
  }

  const press = (current: AdminTrip, item: (typeof ACTIONS)[number]) => {
    // Đổi trạng thái chuyến không hoàn tác được: bấm lần đầu để chuẩn bị, lần hai mới thực hiện.
    if (armed !== item.type) {
      setArmed(item.type)
      return
    }
    setArmed(null)
    void perform(current, item.type, t(item.reason))
  }

  const nextStep = trip ? NEXT_STEP[trip.status] : undefined

  return (
    <div className="space-y-4">
      {user && <GpsSharePanel userId={user.id} />}

      {!trip ? (
        <div className="space-y-3 rounded-2xl bg-white p-8 text-center shadow-xs">
          <p className="text-on-surface-variant">{t('You have no trips to run right now.')}</p>
          <p className="text-xs text-on-surface-variant">
            {t('Trips on your boat from 12 hours ago to the next 7 days appear here.')}
          </p>
          <Button size="sm" variant="secondary" icon="refresh" onClick={reload}>
            {t('Refresh')}
          </Button>
        </div>
      ) : (
        <>
          <div className="rounded-2xl bg-white p-5 shadow-xs">
            {open.length > 1 && (
              <>
                <label htmlFor="captain-trip" className="mb-1 block text-xs font-semibold uppercase tracking-wide text-on-surface-variant">
                  {t('Trip')}
                </label>
                <select
                  id="captain-trip"
                  value={trip.id}
                  onChange={(e) => {
                    setSelectedId(e.target.value)
                    setArmed(null)
                    setSosSent(false)
                  }}
                  className="mb-4 w-full rounded-lg border border-outline-variant px-3 py-2.5 text-base text-deep-river"
                >
                  {open.map((x) => (
                    <option key={x.id} value={x.id}>
                      {formatDateTime(x.departureTime)} • {x.routeName}
                    </option>
                  ))}
                </select>
              </>
            )}
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="font-display text-3xl font-bold text-deep-river">
                  {formatClock(new Date(trip.departureTime))} → {formatClock(new Date(trip.arrivalTime))}
                </div>
                <div className="mt-1 text-sm font-semibold text-deep-river">{trip.routeName}</div>
                <div className="text-sm text-on-surface-variant">
                  {formatDateTime(trip.departureTime)} • {trip.boatCode ?? '—'} •{' '}
                  {t(trip.tripType === 'Sightseeing' ? 'Sightseeing trip' : 'Regular trip')}
                </div>
              </div>
              <TripStatusBadge status={trip.status} />
            </div>
          </div>

          <div className="grid grid-cols-3 gap-3">
            {ACTIONS.map((item) => (
              <BigButton
                key={item.type}
                icon={item.icon}
                label={armed === item.type ? t('Tap again to confirm') : t(item.label)}
                tone={item.tone}
                armed={armed === item.type}
                disabled={!trip.allowedOperations.includes(item.type) || action.busy}
                onClick={() => press(trip, item)}
              />
            ))}
          </div>
          {nextStep && <p className="text-center text-xs text-on-surface-variant">{t(nextStep)}</p>}

          <div className="flex">
            <BigButton
              icon="sos"
              label="SOS"
              tone="red"
              onClick={() => {
                setSosSent(false)
                setSosOpen(true)
              }}
            />
          </div>

          {trip.status === 'Suspended' && <Notice tone="error">{t('This trip is on hold. Wait for the admin to resume it.')}</Notice>}
          {action.error && <Notice tone="error">{action.error}</Notice>}
          {sosSent && <Notice tone="success">{t('SOS sent. The admin has been alerted.')}</Notice>}

          {sosOpen && (
            <Modal title="Send an SOS" onClose={() => setSosOpen(false)}>
              <p className="text-sm text-on-surface-variant">{t('This alerts the admin right away. Only use it in an emergency.')}</p>
              <label htmlFor="sos-note" className="mb-1 mt-3 block text-xs font-semibold text-on-surface-variant">
                {t('What is happening? (optional)')}
              </label>
              <textarea
                id="sos-note"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                className="w-full rounded-lg border border-outline-variant px-3 py-2 text-sm text-deep-river"
              />
              <div className="mt-4 flex justify-end gap-2">
                <Button size="sm" variant="neutral" onClick={() => setSosOpen(false)}>
                  {t('Cancel')}
                </Button>
                <Button
                  size="sm"
                  variant="danger"
                  disabled={action.busy}
                  onClick={async () => {
                    if (await perform(trip, 'SOS', note.trim() || t('Emergency reported by the captain.'))) {
                      setSosOpen(false)
                      setNote('')
                      setSosSent(true)
                    }
                  }}
                >
                  {t('Send SOS')}
                </Button>
              </div>
            </Modal>
          )}
        </>
      )}
    </div>
  )
}
