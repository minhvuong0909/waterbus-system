import { useState } from 'react'
import { Button, ErrorState, Modal, Notice, PageLoader } from '../../components/ui'
import BigButton from '../../features/captain/components/BigButton'
import GpsSharePanel from '../../features/captain/components/GpsSharePanel'
import OnboardPlayerPanel from '../../features/captain/components/OnboardPlayerPanel'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { getOperationTrips, operateTrip } from '../../features/operations/services/operationsService'
import type { AdminTrip, CaptainActionHint, OperationType } from '../../features/operations/types'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatClock, formatDateTime } from '../../utils/time'

type CaptainAction = Extract<OperationType, 'OpenBoarding' | 'Start' | 'Arrive' | 'Complete'>

/** Việc thuyền trưởng làm được theo thứ tự chuyến đi; backend quyết định việc nào đang được phép (`allowedOperations`). */
const ACTIONS: { type: CaptainAction; icon: string; label: string; tone: 'teal' | 'dark'; reason: string }[] = [
  { type: 'OpenBoarding', icon: 'how_to_reg', label: 'Open boarding', tone: 'dark', reason: 'Boarding opened by the captain.' },
  { type: 'Start', icon: 'play_arrow', label: 'Start trip', tone: 'teal', reason: 'Started by the captain.' },
  { type: 'Arrive', icon: 'anchor', label: 'Arrived', tone: 'dark', reason: 'Arrived at the pier.' },
  { type: 'Complete', icon: 'check_circle', label: 'Finish trip', tone: 'teal', reason: 'Trip finished by the captain.' },
]

/** Gợi ý việc tiếp theo theo trạng thái chuyến (chuỗi nguồn tiếng Anh). */
const NEXT_STEP: Partial<Record<AdminTrip['status'], string>> = {
  Scheduled: 'Open boarding when passengers start to board, then start the trip when the boat leaves the pier.',
  Boarding: 'Start the trip when the boat leaves the pier.',
  EnRoute: 'Press Arrived when the boat reaches the last pier.',
  Arrived: 'Finish the trip once everyone has left the boat.',
}

/** Chuyến còn việc cho thuyền trưởng: chưa chạy, đang chạy, đã cập bến mà chưa kết thúc. */
const isActiveForCaptain = (status: AdminTrip['status']) =>
  status === 'Scheduled' || status === 'Boarding' || status === 'EnRoute' || status === 'Suspended' || status === 'Arrived'

/** Chuyến nào được chọn sẵn: đang chạy trước, rồi đang đón khách, đã cập bến, tạm ngưng, cuối cùng là chuyến sắp tới. */
const DEFAULT_ORDER: AdminTrip['status'][] = ['EnRoute', 'Boarding', 'Arrived', 'Suspended', 'Scheduled']

/** Vì sao nút chưa bấm được (mã từ `CaptainOperationRules` của backend); null khi không cần giải thích. */
function blockedReason(hint: CaptainActionHint | undefined): string | null {
  if (!hint || hint.allowed) return null
  switch (hint.reason) {
    case 'NotYet':
      return t('Boarding opens at {time}.', { time: hint.availableAt ? formatClock(new Date(hint.availableAt)) : '' })
    case 'OpenBoardingFirst':
      return t('Open boarding first.')
    case 'NoPosition':
      return t('Waiting for the boat GPS. Turn on Share GPS.')
    case 'NotAtLastStop':
      return t('The boat has not reached the last pier yet.')
    default:
      return null
  }
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

  const open = trips.data.filter((x) => isActiveForCaptain(x.status))
  // Mặc định là chuyến đang chạy trên sông, nếu không có thì chuyến sắp tới.
  const trip: AdminTrip | undefined =
    open.find((x) => x.id === selectedId) ?? DEFAULT_ORDER.flatMap((status) => open.filter((x) => x.status === status))[0] ?? open[0]

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
      <OnboardPlayerPanel />

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

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {ACTIONS.map((item) => {
              const hint = trip.captainActions?.find((h) => h.operation === item.type)
              const allowed = hint ? hint.allowed : trip.allowedOperations.includes(item.type)
              const why = blockedReason(hint)
              return (
                <div key={item.type} className="flex flex-col gap-1.5">
                  <BigButton
                    icon={item.icon}
                    label={armed === item.type ? t('Tap again to confirm') : t(item.label)}
                    tone={item.tone}
                    armed={armed === item.type}
                    disabled={!allowed || action.busy}
                    onClick={() => press(trip, item)}
                  />
                  {why && <p className="px-1 text-center text-xs text-on-surface-variant">{why}</p>}
                </div>
              )
            })}
          </div>
          {nextStep && <p className="text-center text-xs text-on-surface-variant">{t(nextStep)}</p>}

          <div className="flex">
            <BigButton
              icon="warning"
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
