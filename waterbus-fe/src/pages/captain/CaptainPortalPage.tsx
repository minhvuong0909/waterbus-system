import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Button, ErrorState, Modal, Notice, PageLoader } from '../../components/ui'
import Icon from '../../features/captain/components/CaptainIcon'
import CaptainHeader from '../../features/captain/components/CaptainHeader'
import CaptainMap from '../../features/captain/components/CaptainMap'
import OnboardPlayerPanel from '../../features/captain/components/OnboardPlayerPanel'
import { useCaptainGps } from '../../features/captain/hooks/useCaptainGps'
import {
  getCaptainAttendance,
  getCaptainBoat,
} from '../../features/captain/services/captainService'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import {
  getOperationTrips,
  operateTrip,
} from '../../features/operations/services/operationsService'
import type { AdminTrip, CaptainActionHint, OperationType } from '../../features/operations/types'
import { getTrackedTrip } from '../../features/tracking/services/trackingService'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { formatClock, formatDateTime } from '../../utils/time'
import { formatGpsTime } from '../../features/captain/utils'

const STATUS_LABEL: Record<AdminTrip['status'], string> = {
  Scheduled: 'Chờ đón khách',
  Boarding: 'Đang đón khách',
  EnRoute: 'Đang hành trình',
  Suspended: 'Chuyến tạm ngưng',
  Arrived: 'Đã đến bến cuối',
  Completed: 'Đã hoàn tất',
  Cancelled: 'Đã hủy',
  Terminated: 'Đã kết thúc sớm',
}
const ACTIONS: { type: OperationType; icon: string; label: string; reason: string }[] = [
  {
    type: 'OpenBoarding',
    icon: 'groups',
    label: 'Mở đón khách',
    reason: 'Thuyền trưởng mở đón khách.',
  },
  {
    type: 'Start',
    icon: 'play_arrow',
    label: 'Khởi hành',
    reason: 'Thuyền trưởng xác nhận khởi hành.',
  },
  {
    type: 'Arrive',
    icon: 'sports_score',
    label: 'Đến bến cuối',
    reason: 'Thuyền trưởng xác nhận đến bến cuối.',
  },
  {
    type: 'Complete',
    icon: 'check_circle',
    label: 'Kết thúc chuyến',
    reason: 'Thuyền trưởng kết thúc chuyến.',
  },
]

/** Chuyến còn việc cho thuyền trưởng: chưa chạy, đang chạy, đã đến bến cuối mà chưa kết thúc. */
const isActiveForCaptain = (status: AdminTrip['status']) =>
  status === 'Scheduled' ||
  status === 'Boarding' ||
  status === 'EnRoute' ||
  status === 'Suspended' ||
  status === 'Arrived'

/** Chuyến được chọn sẵn: đang chạy trước, rồi tạm ngưng, đang đón khách, đã đến bến cuối, cuối cùng là chuyến sắp tới. */
const DEFAULT_ORDER: AdminTrip['status'][] = ['EnRoute', 'Suspended', 'Boarding', 'Arrived', 'Scheduled']

/** Vì sao nút chưa bấm được (mã từ `CaptainOperationRules` của backend); null khi không cần giải thích. */
function blockedReason(hint: CaptainActionHint | undefined): string | null {
  if (!hint || hint.allowed) return null
  switch (hint.reason) {
    case 'NotYet':
      return `Đến ${hint.availableAt ? formatClock(new Date(hint.availableAt)) : '—'} mới mở đón khách.`
    case 'OpenBoardingFirst':
      return 'Cần mở đón khách trước khi khởi hành.'
    case 'NoPosition':
      return 'Đang chờ GPS của tàu. Hãy bật chia sẻ vị trí GPS.'
    case 'NotAtLastStop':
      return 'Tàu chưa tới bến cuối của tuyến.'
    default:
      return null
  }
}
const clock = (iso?: string | null) => (iso ? formatClock(new Date(iso)) : '—')

function Card({
  title,
  icon,
  children,
  className = '',
}: {
  title: string
  icon: string
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`captain-card captain-side-card ${className}`}>
      <h2>
        <Icon name={icon} />
        {title}
      </h2>
      {children}
    </section>
  )
}
function Metric({
  icon,
  label,
  value,
  caption,
  warning = false,
}: {
  icon: string
  label: string
  value: string
  caption: string
  warning?: boolean
}) {
  return (
    <div className={`captain-card captain-metric ${warning ? 'has-warning' : ''}`}>
      <span className="captain-metric-icon">
        <Icon name={icon} />
      </span>
      <div>
        <p>{label}</p>
        <strong>{value}</strong>
        <small>{caption}</small>
      </div>
    </div>
  )
}

function TripDashboard({
  trip,
  version,
  gps,
  reload,
}: {
  trip: AdminTrip
  version: number
  gps: ReturnType<typeof useCaptainGps>
  reload: () => void
}) {
  const { user } = useAuth()
  const detail = useFetch(() => getTrackedTrip(trip.id), [trip.id, version])
  const attendance = useFetch(() => getCaptainAttendance(trip.id), [trip.id, version])
  const boat = useFetch(() => getCaptainBoat(trip.boatId), [trip.boatId])
  const [confirmation, setConfirmation] = useState<(typeof ACTIONS)[number] | null>(null)
  const [sosOpen, setSosOpen] = useState(false)
  const [note, setNote] = useState('')
  const [sosSentAt, setSosSentAt] = useState<string | null>(null)
  const action = useSubmit()
  const stops = (detail.data?.stops ?? []).filter((s) => s.status !== 'Skipped')
  const beforeStart = trip.status === 'Scheduled' || trip.status === 'Boarding'
  const suspended = trip.status === 'Suspended'
  const departed = stops.filter((s) => s.status === 'Departed')
  const nextStop = beforeStart
    ? stops[0]
    : stops.find((s, index) => s.status !== 'Departed' && (index > 0 || departed.length > 0))
  const lastDeparted = departed[departed.length - 1]
  const confirmed = attendance.data?.state === 'Present' || attendance.data?.state === 'Late'
  const attendanceMessage = confirmed
    ? `Admin đã xác nhận có mặt lúc ${clock(attendance.data?.attendance?.recordedAt)}${
        attendance.data?.state === 'Late' ? ' (đến muộn)' : ''
      }`
    : attendance.error
    ? 'Chưa tải được trạng thái điểm danh'
    : attendance.data?.state === 'NotConfigured'
    ? 'Chưa cấu hình hạn điểm danh'
    : attendance.data?.state === 'Missing'
    ? 'Quá hạn có mặt · Chờ Admin xác nhận'
    : attendance.data
    ? `Chờ Admin xác nhận có mặt${
        attendance.data.requiredAt ? ` · Hạn ${clock(attendance.data.requiredAt)}` : ''
      }`
    : 'Đang tải trạng thái điểm danh…'
  const gpsLabel = gps.fresh
    ? gps.sharing
      ? 'Đang chia sẻ'
      : 'Đang nhận vị trí'
    : gps.position
    ? 'Chưa cập nhật'
    : 'Chờ tín hiệu'
  const lastUpdate = gps.position ? formatGpsTime(gps.position.observedAt) : '—'
  const unavailable = !gps.connected || action.busy
  const hintOf = (type: OperationType) => trip.captainActions?.find((h) => h.operation === type)
  const allowedBy = (type: OperationType) => {
    const hint = hintOf(type)
    return hint ? hint.allowed : trip.allowedOperations.includes(type)
  }
  // Lý do của nút kế tiếp trong luồng (Mở đón khách → Khởi hành → Đến bến cuối → Kết thúc), nếu đang bị chặn.
  const nextBlocked = blockedReason(
    hintOf(trip.status === 'Scheduled' ? 'OpenBoarding' : trip.status === 'EnRoute' ? 'Arrive' : 'Start'),
  )
  const nextInstruction = !gps.connected
    ? 'Chờ kết nối lại để thực hiện thao tác.'
    : suspended
    ? 'Chuyến tạm ngưng. Chờ quyết định tiếp tục từ Admin.'
    : attendance.error
    ? 'Chưa xác minh được điểm danh. Thử tải lại trước khi khởi hành.'
    : !confirmed && beforeStart
    ? 'Captain cần được Admin xác nhận có mặt trước khi khởi hành.'
    : trip.status === 'Scheduled'
    ? nextBlocked ?? 'Mở đón khách trước khi khởi hành.'
    : trip.status === 'Boarding'
    ? 'Xác nhận khởi hành khi tàu rời bến.'
    : trip.status === 'Arrived'
    ? 'Kết thúc chuyến khi khách đã xuống hết.'
    : nextBlocked ?? 'Xác nhận đến bến cuối khi tàu đã tới bến cuối của tuyến.'

  const perform = async (type: OperationType, reason: string) => {
    const done = await action.run(() => operateTrip(trip.id, type, reason))
    reload()
    return Boolean(done)
  }

  return (
    <div className="captain-dashboard">
      <div className="captain-main-column">
        <div className="captain-welcome">
          <h1>Xin chào, Captain {user?.fullName ?? ''}</h1>
          <div className="captain-trip-subtitle">
            <span>{trip.boatCode ?? 'Chưa gán tàu'}</span>
            <span aria-hidden="true">•</span>
            <span>{trip.routeName}</span>
            <span
              className={`captain-pill ${beforeStart || suspended ? 'is-warning' : 'is-success'}`}
            >
              {STATUS_LABEL[trip.status]}
            </span>
          </div>
          <p className={`captain-attendance ${confirmed ? 'is-confirmed' : ''}`}>
            <Icon name={confirmed ? 'check_circle' : 'info'} />
            {attendanceMessage}
          </p>
        </div>
        {suspended && (
          <div className="captain-banner" role="status">
            <Icon name="pause_circle" />
            Chuyến tạm ngưng theo quyết định Admin. Chờ hướng dẫn tiếp tục.
          </div>
        )}
        <div className="captain-metrics">
          <Metric
            icon="speed"
            label="Tốc độ tàu"
            value={
              gps.fresh && gps.position?.speedKnots != null
                ? `${gps.position.speedKnots.toFixed(1)} kn`
                : '—'
            }
            caption={gps.fresh ? 'Tốc độ từ GPS của tàu' : 'Chưa có dữ liệu mới'}
          />
          <Metric
            icon="location_on"
            label={beforeStart ? 'Bến khởi hành' : 'Bến tiếp theo'}
            value={nextStop?.stationName ?? '—'}
            caption={
              suspended
                ? 'Chờ cập nhật giờ đến'
                : `Dự kiến ${clock(
                    beforeStart
                      ? nextStop?.estimatedDepartureTime ?? trip.departureTime
                      : nextStop?.estimatedArrivalTime,
                  )}`
            }
          />
          <Metric
            icon="groups"
            label="Khách trên đoạn hiện tại"
            value="—"
            caption={`Chưa có dữ liệu${boat.data ? ` / ${boat.data.totalSeats} chỗ` : ''}`}
          />
          <Metric
            icon={gps.fresh ? 'satellite_alt' : 'location_off'}
            label="Tín hiệu GPS"
            value={gpsLabel}
            caption={`${gps.fresh ? 'Cập nhật' : 'Lần cuối'} ${lastUpdate}`}
            warning={!gps.fresh}
          />
        </div>
        {detail.error && (
          <Notice tone="error">
            Không tải được hành trình.{' '}
            <button type="button" className="underline" onClick={detail.retry}>
              Thử lại
            </button>
          </Notice>
        )}
        <CaptainMap
          key={trip.id}
          stops={stops}
          routeCode={trip.routeCode}
          tripType={trip.tripType}
          boatCode={trip.boatCode ?? ''}
          position={gps.position}
          fresh={gps.fresh}
        />
        <section className="captain-card captain-controls">
          <div className="captain-card-heading">
            <h2>
              <Icon name="settings" />
              Thao tác vận hành
            </h2>
            <label className="captain-gps-toggle">
              <span>Chia sẻ vị trí GPS</span>
              <button
                type="button"
                role="switch"
                aria-label="Chia sẻ vị trí GPS"
                aria-checked={gps.sharing}
                onClick={gps.toggleSharing}
                className={gps.sharing ? 'is-on' : ''}
              >
                <span />
              </button>
            </label>
          </div>
          <div className="captain-control-state">
            <span
              className={`captain-pill ${suspended || beforeStart ? 'is-warning' : 'is-success'}`}
            >
              {STATUS_LABEL[trip.status]}
            </span>
            <span className={`captain-pill ${confirmed ? 'is-success' : 'is-warning'}`}>
              {confirmed ? 'Đã điểm danh' : 'Chờ điểm danh'}
            </span>
            <span className={`captain-pill ${gps.fresh ? 'is-success' : 'is-warning'}`}>
              {gps.fresh ? 'GPS đang cập nhật' : 'GPS chưa cập nhật'}
            </span>
          </div>
          <div className="captain-action-grid">
            {ACTIONS.map((item) => (
              <button
                type="button"
                key={item.type}
                className="captain-action"
                disabled={unavailable || !allowedBy(item.type)}
                title={blockedReason(hintOf(item.type)) ?? undefined}
                onClick={() => {
                  action.clearError()
                  setConfirmation(item)
                }}
              >
                <Icon name={item.icon} />
                {item.label}
              </button>
            ))}
            <button
              type="button"
              className="captain-action captain-sos"
              disabled={unavailable}
              onClick={() => {
                action.clearError()
                setSosOpen(true)
              }}
            >
              <Icon name="e911_emergency" />
              Gửi SOS
            </button>
          </div>
          <p className="captain-instruction">
            <Icon name="info" />
            {nextInstruction}
          </p>
          {gps.error && <Notice tone="error">{gps.error}</Notice>}
          {action.error && <Notice tone="error">{action.error}</Notice>}
        </section>
      </div>
      <aside className="captain-side-column">
        <Card
          title={
            suspended
              ? 'Chuyến tạm ngưng'
              : beforeStart
              ? 'Chuyến sắp khởi hành'
              : 'Chuyến đang vận hành'
          }
          icon="directions_boat"
        >
          <h3 className="captain-route-title">{trip.routeName}</h3>
          <p className="captain-muted">
            {trip.boatCode} ·{' '}
            {trip.tripType === 'Sightseeing' ? 'Chuyến tham quan' : 'Chuyến thường'}
          </p>
          <div className="captain-journey-summary">
            <div>
              <Icon name="location_on" />
              <span>
                <small>{beforeStart ? 'Bến khởi hành' : 'Bến tiếp theo'}</small>
                <strong>{nextStop?.stationName ?? 'Chờ cập nhật'}</strong>
              </span>
            </div>
            <div>
              <Icon name="schedule" />
              <span>
                <small>{beforeStart ? 'Khởi hành dự kiến' : 'Dự kiến đến'}</small>
                <strong>
                  {suspended
                    ? 'Chờ cập nhật'
                    : clock(beforeStart ? trip.departureTime : nextStop?.estimatedArrivalTime)}
                </strong>
              </span>
            </div>
          </div>
          <div className={`captain-journey-progress ${suspended ? 'is-paused' : ''}`}>
            <Icon name={suspended ? 'pause_circle' : 'route'} />
            {suspended
              ? 'Chờ quyết định từ Admin'
              : lastDeparted
              ? `Đã rời bến ${lastDeparted.stationName}`
              : beforeStart
              ? 'Chuẩn bị đón khách tại bến đầu'
              : 'Đang thực hiện hành trình'}
          </div>
          <ol className="captain-stop-list">
            {stops.map((stop) => (
              <li
                key={stop.callId}
                className={`${stop.status === 'Departed' ? 'is-departed' : ''} ${
                  stop.callId === nextStop?.callId ? 'is-next' : ''
                }`}
              >
                <span className="captain-stop-dot">
                  {stop.status === 'Departed' && <Icon name="check" />}
                </span>
                <div>
                  <strong>{stop.stationName}</strong>
                  <small>
                    {stop.status === 'Skipped'
                      ? 'Bỏ qua bến'
                      : stop.status === 'Departed'
                      ? 'Đã rời bến'
                      : stop.callId === nextStop?.callId
                      ? beforeStart
                        ? 'Bến đầu'
                        : 'Bến tiếp theo'
                      : 'Chưa đến'}
                  </small>
                </div>
                <span className="captain-stop-time">
                  Dự kiến <b>{clock(stop.estimatedArrivalTime ?? stop.estimatedDepartureTime)}</b>
                  {suspended && <small>Chưa cập nhật</small>}
                </span>
              </li>
            ))}
          </ol>
          {detail.loading && <p className="captain-muted">Đang tải các bến…</p>}
          <p className="captain-schedule-note">
            Lịch chuyến: {clock(trip.departureTime)} – {clock(trip.arrivalTime)} · Giờ Việt Nam
          </p>
        </Card>
        <Card title="An toàn & kết nối" icon="verified_user">
          <dl className="captain-connection-list">
            <div>
              <dt>
                <span className={`captain-dot ${gps.fresh ? '' : 'is-amber'}`} />
                GPS
              </dt>
              <dd className={`captain-pill ${gps.fresh ? 'is-success' : 'is-warning'}`}>
                {gps.fresh ? 'Đang nhận vị trí' : 'Chưa có tín hiệu mới'}
              </dd>
            </div>
            <div>
              <dt>
                <span className={`captain-dot ${gps.connected ? '' : 'is-amber'}`} />
                Máy chủ
              </dt>
              <dd className={`captain-pill ${gps.connected ? 'is-success' : 'is-warning'}`}>
                {gps.connected ? 'Trực tuyến' : 'Đang kết nối lại'}
              </dd>
            </div>
            <div>
              <dt>
                <Icon name="schedule" />
                GPS cập nhật cuối
              </dt>
              <dd className="captain-tabular">{lastUpdate}</dd>
            </div>
          </dl>
        </Card>
        <Card title="Thông báo" icon="notifications" className="captain-notifications">
          <div className="captain-notice-list" aria-live="polite">
            {!gps.connected && (
              <div className="captain-notification is-warning">
                <Icon name="wifi_off" />
                <span>Mất kết nối máy chủ. Các thao tác và SOS đang tạm khóa.</span>
              </div>
            )}
            {suspended && (
              <div className="captain-notification is-warning">
                <Icon name="pause_circle" />
                <span>Chuyến tạm ngưng · Chờ Admin</span>
              </div>
            )}
            {gps.position && !gps.fresh && (
              <div className="captain-notification is-warning">
                <Icon name="location_off" />
                <span>Đang hiển thị vị trí cuối lúc {lastUpdate}</span>
              </div>
            )}
            <div className={`captain-notification ${confirmed ? 'is-success' : 'is-warning'}`}>
              <Icon name={confirmed ? 'check_circle' : 'info'} />
              <span>{attendanceMessage}</span>
            </div>
            {attendance.error && (
              <button type="button" className="captain-outline" onClick={attendance.retry}>
                Tải lại điểm danh
              </button>
            )}
            {sosSentAt && (
              <div className="captain-notification is-success">
                <Icon name="check_circle" />
                <span>Đã gửi SOS lúc {clock(sosSentAt)}. Chờ Admin xử lý.</span>
              </div>
            )}
            {beforeStart && (
              <div className="captain-notification is-neutral">
                <Icon name="schedule" />
                <span>Khởi hành dự kiến lúc {clock(trip.departureTime)}</span>
              </div>
            )}
          </div>
        </Card>
      </aside>
      {confirmation && (
        <Modal
          title={`Xác nhận ${confirmation.label.toLowerCase()}`}
          onClose={() => {
            if (!action.busy) setConfirmation(null)
          }}
        >
          <p>
            Bạn xác nhận “{confirmation.label}” cho {trip.boatCode} · {trip.routeName}?
          </p>
          <p className="mt-2 text-sm text-on-surface-variant">
            Thao tác sẽ cập nhật trạng thái chuyến và được ghi vào nhật ký vận hành.
          </p>
          {action.error && <Notice tone="error">{action.error}</Notice>}
          <div className="mt-5 flex justify-end gap-3">
            <Button variant="neutral" disabled={action.busy} onClick={() => setConfirmation(null)}>
              Quay lại
            </Button>
            <Button
              disabled={unavailable || !allowedBy(confirmation.type)}
              onClick={async () => {
                if (await perform(confirmation.type, confirmation.reason)) setConfirmation(null)
              }}
            >
              Xác nhận
            </Button>
          </div>
        </Modal>
      )}
      {sosOpen && (
        <Modal
          title="Gửi SOS"
          onClose={() => {
            if (!action.busy) setSosOpen(false)
          }}
        >
          <p>Thông báo khẩn cấp tới Admin. Gửi SOS không tự thay đổi trạng thái chuyến.</p>
          <label className="mt-4 block text-sm font-semibold" htmlFor="sos-note">
            Tình huống đang xảy ra (không bắt buộc)
          </label>
          <textarea
            id="sos-note"
            rows={3}
            maxLength={2000}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            className="mt-2 w-full rounded-lg border border-outline-variant p-3"
          />
          {action.error && <Notice tone="error">{action.error}</Notice>}
          <div className="mt-5 flex justify-end gap-3">
            <Button variant="neutral" disabled={action.busy} onClick={() => setSosOpen(false)}>
              Quay lại
            </Button>
            <Button
              variant="danger"
              disabled={unavailable}
              onClick={async () => {
                if (
                  await perform('SOS', note.trim() || 'Thuyền trưởng báo cáo tình huống khẩn cấp.')
                ) {
                  setSosOpen(false)
                  setNote('')
                  setSosSentAt(new Date().toISOString())
                }
              }}
            >
              Gửi SOS
            </Button>
          </div>
        </Modal>
      )}
    </div>
  )
}

export default function CaptainPortalPage() {
  useDocumentTitle('Trung tâm vận hành tàu')
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const open = (trips.data ?? []).filter((x) => isActiveForCaptain(x.status))
  const trip =
    open.find((x) => x.id === selectedId) ??
    DEFAULT_ORDER.flatMap((status) => open.filter((x) => x.status === status))[0] ??
    open[0]
  const gps = useCaptainGps(trip?.boatId ?? '', reload)
  // Điểm danh do Admin ghi nhận; tải lại định kỳ và khi reconnect để đồng bộ với backend.
  useEffect(() => {
    const interval = window.setInterval(reload, 30000)
    return () => window.clearInterval(interval)
  }, [reload])
  return (
    <>
      <CaptainHeader connected={gps.connected} fresh={gps.fresh} />
      <main className="captain-content">
        {trips.error && trips.data && (
          <div className="captain-banner" role="status">
            <Icon name="sync_problem" />
            Chưa đồng bộ được dữ liệu chuyến. Đang giữ dữ liệu lần trước.
            <button type="button" onClick={reload}>
              Thử lại
            </button>
          </div>
        )}
        {trips.error && !trips.data ? (
          <ErrorState onRetry={trips.retry} />
        ) : !trips.data ? (
          <PageLoader />
        ) : !trip ? (
          <div className="captain-card captain-empty">
            <Icon name="directions_boat" />
            <h1>Chưa có chuyến cần vận hành</h1>
            <p>Các chuyến của tàu bạn từ 12 giờ trước đến 7 ngày tới sẽ hiển thị tại đây.</p>
            <Button variant="secondary" onClick={reload}>
              Tải lại chuyến
            </Button>
          </div>
        ) : (
          <>
            <div className="captain-trip-picker">
              <label htmlFor="captain-trip">
                <Icon name="directions_boat" />
                Chuyến được phân công
              </label>
              <select
                id="captain-trip"
                value={trip.id}
                onChange={(e) => setSelectedId(e.target.value)}
              >
                {open.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.boatCode} · {formatDateTime(x.departureTime)} – {clock(x.arrivalTime)} ·{' '}
                    {x.routeName}
                  </option>
                ))}
              </select>
            </div>
            <OnboardPlayerPanel />
            <TripDashboard key={trip.id} trip={trip} version={version} gps={gps} reload={reload} />
          </>
        )}
        <footer className="captain-footer">
          SMART WATERBUS <span>•</span> Vận hành an toàn, hành trình thông suốt
        </footer>
      </main>
    </>
  )
}
