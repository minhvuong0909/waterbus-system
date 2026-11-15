import { useEffect, useState } from 'react'
import { Button, ErrorState, Modal, Notice, PageLoader } from '../../components/ui'
import Icon from '../../features/captain/components/CaptainIcon'
import CaptainActions, { blockedReason } from '../../features/captain/components/CaptainActions'
import type { CaptainAction } from '../../features/captain/components/CaptainActions'
import CaptainHeader from '../../features/captain/components/CaptainHeader'
import CaptainMap from '../../features/captain/components/CaptainMap'
import BoatSpeaker from '../../features/captain/components/BoatSpeaker'
import { useCaptainGps } from '../../features/captain/hooks/useCaptainGps'
import { getCaptainAttendance } from '../../features/captain/services/captainService'
import { formatGpsTime } from '../../features/captain/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { getOperationTrips, operateTrip } from '../../features/operations/services/operationsService'
import type { AdminTrip, OperationType } from '../../features/operations/types'
import { getTrackedTrip } from '../../features/tracking/services/trackingService'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { formatClock } from '../../utils/time'

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

/** Chuyến còn việc cho thuyền trưởng: chưa chạy, đang chạy, đã đến bến cuối mà chưa kết thúc. */
const isActiveForCaptain = (status: AdminTrip['status']) =>
  status === 'Scheduled' || status === 'Boarding' || status === 'EnRoute' || status === 'Suspended' || status === 'Arrived'

/** Chuyến hiện tại của tàu: đang chạy trước, rồi tạm ngưng, đang đón khách, đã đến bến cuối, cuối cùng là chuyến sắp tới. */
const DEFAULT_ORDER: AdminTrip['status'][] = ['EnRoute', 'Suspended', 'Boarding', 'Arrived', 'Scheduled']

const clock = (iso?: string | null) => (iso ? formatClock(new Date(iso)) : '—')

function TripDashboard({
  trip,
  upcoming,
  version,
  gps,
  reload,
}: {
  trip: AdminTrip
  /** Số chuyến sắp tới của tàu sau chuyến này (chỉ để báo, thuyền trưởng không chọn chuyến). */
  upcoming: number
  version: number
  gps: ReturnType<typeof useCaptainGps>
  reload: () => void
}) {
  const { user } = useAuth()
  const detail = useFetch(() => getTrackedTrip(trip.id), [trip.id, version])
  const attendance = useFetch(() => getCaptainAttendance(trip.id), [trip.id, version])
  const [confirmation, setConfirmation] = useState<CaptainAction | null>(null)
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

  const confirmed = attendance.data?.state === 'Present' || attendance.data?.state === 'Late'
  const attendanceMessage = confirmed
    ? `Admin đã xác nhận có mặt lúc ${clock(attendance.data?.attendance?.recordedAt)}${attendance.data?.state === 'Late' ? ' (đến muộn)' : ''}`
    : attendance.error
    ? 'Chưa tải được trạng thái điểm danh'
    : attendance.data?.state === 'NotConfigured'
    ? 'Chưa cấu hình hạn điểm danh'
    : attendance.data?.state === 'Missing'
    ? 'Quá hạn có mặt · Chờ Admin xác nhận'
    : attendance.data
    ? `Chờ Admin xác nhận có mặt${attendance.data.requiredAt ? ` · Hạn ${clock(attendance.data.requiredAt)}` : ''}`
    : 'Đang tải trạng thái điểm danh…'

  const locked = !gps.connected || action.busy
  const hintOf = (type: OperationType) => trip.captainActions?.find((h) => h.operation === type)
  const allowedBy = (type: OperationType) => {
    const hint = hintOf(type)
    return hint ? hint.allowed : trip.allowedOperations.includes(type)
  }
  const reasonOf = (type: OperationType) => blockedReason(hintOf(type), (iso) => clock(iso))

  // Một dòng hướng dẫn cho việc kế tiếp.
  const nextInstruction = !gps.connected
    ? 'Mất kết nối máy chủ. Các thao tác và SOS đang tạm khóa.'
    : suspended
    ? 'Chuyến tạm ngưng. Chờ quyết định tiếp tục từ Admin.'
    : attendance.error
    ? 'Chưa xác minh được điểm danh. Thử tải lại trước khi khởi hành.'
    : !confirmed && beforeStart
    ? 'Captain cần được Admin xác nhận có mặt trước khi khởi hành.'
    : trip.status === 'Scheduled'
    ? reasonOf('OpenBoarding') ?? 'Mở đón khách khi khách bắt đầu lên tàu.'
    : trip.status === 'Boarding'
    ? 'Bấm Khởi hành khi tàu rời bến.'
    : trip.status === 'Arrived'
    ? 'Kết thúc chuyến khi khách đã xuống hết.'
    : reasonOf('Arrive') ?? 'Bấm Đến bến cuối khi tàu đã tới bến cuối của tuyến.'

  const perform = async (type: OperationType, reason: string) => {
    const done = await action.run(() => operateTrip(trip.id, type, reason))
    reload()
    return Boolean(done)
  }

  return (
    <div className="cap-main">
        <section className="cap-hero">
          <div className="cap-hero-top">
            <p className="cap-greeting">Xin chào, Captain {user?.fullName ?? ''}</p>
            {upcoming > 0 && <span className="cap-greeting">Còn {upcoming} chuyến tiếp theo của tàu</span>}
          </div>
          <h1>{trip.routeName}</h1>
          <p className="cap-hero-meta">
            <span>{trip.boatCode ?? 'Chưa gán tàu'}</span>
            <span>{trip.tripType === 'Sightseeing' ? 'Chuyến tham quan' : 'Chuyến thường'}</span>
            <span>
              {clock(trip.departureTime)} – {clock(trip.arrivalTime)}
            </span>
            <span className={`captain-pill ${beforeStart || suspended ? 'is-warning' : 'is-success'}`}>{STATUS_LABEL[trip.status]}</span>
          </p>
          <p className={`cap-attendance ${confirmed ? 'is-confirmed' : ''}`}>
            <Icon name={confirmed ? 'check_circle' : 'info'} />
            {attendanceMessage}
            {attendance.error && (
              <button type="button" onClick={attendance.retry}>
                Tải lại
              </button>
            )}
          </p>
          {suspended && (
            <div className="captain-banner" role="status">
              <Icon name="pause_circle" />
              Chuyến tạm ngưng theo quyết định Admin. Chờ hướng dẫn tiếp tục.
            </div>
          )}
        </section>

        <CaptainActions
          trip={trip}
          allowed={allowedBy}
          reasonOf={reasonOf}
          locked={locked}
          onPick={(item) => {
            action.clearError()
            setConfirmation(item)
          }}
          onSos={() => {
            action.clearError()
            setSosOpen(true)
          }}
        />
        <p className="cap-instruction">
          <Icon name="info" />
          {nextInstruction}
        </p>
        {gps.error && <Notice tone="error">{gps.error}</Notice>}
        {action.error && !confirmation && !sosOpen && <Notice tone="error">{action.error}</Notice>}
        {sosSentAt && <Notice tone="success">Đã gửi SOS lúc {clock(sosSentAt)}. Chờ Admin xử lý.</Notice>}

        <div className="cap-mapbar">
          <p>
            <Icon name="location_on" />
            {nextStop ? (
              <>
                {beforeStart ? 'Bến xuất phát' : 'Bến tiếp theo'}: <strong>{nextStop.stationName}</strong> · Dự kiến{' '}
                {clock(beforeStart ? nextStop.estimatedDepartureTime ?? trip.departureTime : nextStop.estimatedArrivalTime)}
              </>
            ) : (
              'Chưa có thông tin bến'
            )}
          </p>
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
        {detail.error && (
          <Notice tone="error">
            Không tải được hành trình.{' '}
            <button type="button" className="underline" onClick={detail.retry}>
              Thử lại
            </button>
          </Notice>
        )}
        {gps.position && !gps.fresh && (
          <p className="cap-stale">Đang hiển thị vị trí cuối lúc {formatGpsTime(gps.position.observedAt)}</p>
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
          <p className="mt-2 text-sm text-on-surface-variant">Thao tác sẽ cập nhật trạng thái chuyến và được ghi vào nhật ký vận hành.</p>
          {action.error && <Notice tone="error">{action.error}</Notice>}
          <div className="mt-5 flex justify-end gap-3">
            <Button variant="neutral" disabled={action.busy} onClick={() => setConfirmation(null)}>
              Quay lại
            </Button>
            <Button
              disabled={locked || !allowedBy(confirmation.type)}
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
            maxLength={500}
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
              disabled={locked}
              onClick={async () => {
                if (await perform('SOS', note.trim() || 'Thuyền trưởng báo cáo tình huống khẩn cấp.')) {
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
  const open = (trips.data ?? []).filter((x) => isActiveForCaptain(x.status))
  // Thuyền trưởng gắn cố định với tàu, Admin phân tàu cho từng chuyến: thuyền trưởng không chọn chuyến mà chỉ thấy chuyến hiện tại
  // của tàu (đang chạy, đang đón khách, chờ kết thúc) hoặc, nếu chưa có, chuyến sắp khởi hành sớm nhất.
  const trip = DEFAULT_ORDER.flatMap((status) => open.filter((x) => x.status === status))[0] ?? open[0]
  const gps = useCaptainGps(trip?.boatId ?? '', reload)
  // Điểm danh do Admin ghi nhận; tải lại định kỳ để đồng bộ với backend.
  useEffect(() => {
    const interval = window.setInterval(reload, 30000)
    return () => window.clearInterval(interval)
  }, [reload])

  return (
    <>
      <CaptainHeader connected={gps.connected} fresh={gps.fresh} />
      <BoatSpeaker />
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
          <TripDashboard key={trip.id} trip={trip} upcoming={open.filter((x) => x.id !== trip.id && x.status === 'Scheduled').length} version={version} gps={gps} reload={reload} />
        )}
      </main>
    </>
  )
}
