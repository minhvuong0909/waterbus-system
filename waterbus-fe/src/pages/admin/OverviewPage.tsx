import { Link } from 'react-router-dom'
import { ErrorState, Icon, PageHeader, PageLoader, Panel, StatCard } from '../../components/ui'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { getOpenIncidents, getOperationTrips } from '../../features/operations/services/operationsService'
import { countReconciliationToDo, listPendingRefunds } from '../../features/admin/services/refundService'
import { attentionItems } from '../../features/admin/utils/attention'
import type { AttentionItem } from '../../features/admin/utils/attention'
import { formatDateTime, toVietnamDate, vietnamDayStartIso } from '../../utils/time'
import { combineFetches, useDocumentTitle, useFetch, useNow, useReload } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

/** Một việc cần xử lý: số lượng, mô tả và đường tới đúng chỗ xử lý. */
function AttentionRow({ item }: { item: AttentionItem }) {
  const urgent = item.tone === 'urgent'
  return (
    <li>
      <Link
        to={item.to}
        className="flex min-h-[52px] items-center gap-3 py-3 text-sm transition-colors hover:text-teal-flow"
      >
        <span
          className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${
            urgent ? 'bg-coral-glow/10 text-coral-glow' : 'bg-surface-container text-on-surface-variant'
          }`}
        >
          <Icon name={item.icon} className="text-xl" />
        </span>
        <span className="min-w-0 flex-1 font-medium text-deep-river">{t(item.label, { count: item.count })}</span>
        <span className="inline-flex shrink-0 items-center gap-1 font-semibold text-teal-flow">
          {t(item.action)}
          <Icon name="arrow_forward" className="text-base" />
        </span>
      </Link>
    </li>
  )
}

/** Số dòng của một danh sách hoàn tiền; `undefined` khi không đọc được để Tổng quan vẫn hiện phần còn lại. */
const countOf = (load: () => Promise<{ totalCount: number }>) => () =>
  load()
    .then((page) => page.totalCount)
    .catch(() => undefined)

export default function OverviewPage() {
  useDocumentTitle('Overview')
  const [version, reload] = useReload()
  // Mốc "bây giờ" cập nhật mỗi phút, để "hôm nay" và "sắp tới" tự đổi khi để trang mở lâu.
  const now = useNow(60_000)
  // Lấy chuyến từ 00:00 hôm nay (giờ VN), không dùng mặc định "12 giờ trước" của backend: buổi tối mặc định đó làm
  // mất các chuyến buổi sáng nên "Chuyến hôm nay" bị đếm thiếu. Đổi ngày thì tải lại.
  const todayKey = toVietnamDate(new Date(now))
  const trips = useFetch(() => {
    const from = vietnamDayStartIso(new Date(now))
    // Từ đầu hôm nay tới 7 ngày sau hôm nay (backend cho tối đa 31 ngày).
    return getOperationTrips({ from, to: new Date(Date.parse(from) + 8 * 86_400_000).toISOString() })
  }, [version, todayKey])
  const alerts = useFetch(() => getOpenIncidents(), [version])
  const pendingRefunds = useFetch(countOf(() => listPendingRefunds(1, 1)), [version])
  const reconcile = useFetch(() => countReconciliationToDo().catch(() => undefined), [version])
  // Số đếm, việc cần xử lý và các chuyến sắp tới đổi theo trạng thái chuyến và cảnh báo mới ngay khi có.
  useFleetHub(
    { onTripStatus: reload, onIncident: reload, onReconnected: reload },
  )
  const state = combineFetches(trips, alerts)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !alerts.data) return <PageLoader />

  // "Hôm nay" theo giờ Việt Nam, không theo múi giờ của máy đang mở trang.
  const today = toVietnamDate(new Date(now))
  // Chuyến đã huỷ/kết thúc sớm không còn chạy hôm nay nên không tính vào "Chuyến hôm nay".
  const todays = trips.data.filter(
    (x) => toVietnamDate(new Date(x.departureTime)) === today && x.status !== 'Cancelled' && x.status !== 'Terminated',
  )
  const active = trips.data.filter((x) => x.status === 'Boarding' || x.status === 'EnRoute').length
  // Chuyến đang đón khách/đang chạy, hoặc theo lịch chưa tới giờ khởi hành. Không gồm chuyến đã cập bến/kết thúc,
  // chuyến theo lịch đã quá giờ, hay chuyến tạm ngưng (việc của "Cần xử lý").
  const upcoming = trips.data
    .filter((x) => x.status === 'Boarding' || x.status === 'EnRoute' || (x.status === 'Scheduled' && Date.parse(x.departureTime) >= now))
    .sort((a, b) => Date.parse(a.departureTime) - Date.parse(b.departureTime))
    .slice(0, 5)
  const todo = attentionItems({
    trips: trips.data,
    alerts: alerts.data,
    pendingRefunds: pendingRefunds.data,
    reconcile: reconcile.data,
  })

  return (
    <>
      <PageHeader title="Overview" description="What is running today and what needs attention." />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        <StatCard label="Trips today" value={todays.length} to={ROUTES.adminTrips} />
        <StatCard label="Boarding or on the river" value={active} to={ROUTES.adminRadar} />
        <StatCard label="Open alerts" value={alerts.data.length} to={ROUTES.adminIncidents} attention />
      </div>
      <div className="mt-4 grid gap-4 xl:grid-cols-2">
        <Panel title="Needs your attention">
          {todo.length === 0 ? (
            <p className="flex items-center gap-2 text-sm text-on-surface-variant">
              <Icon name="check_circle" className="text-xl text-teal-flow" />
              {t('Nothing is waiting for you.')}
            </p>
          ) : (
            <ul className="divide-y divide-outline-variant/30">
              {todo.map((item) => (
                <AttentionRow key={item.key} item={item} />
              ))}
            </ul>
          )}
        </Panel>
        <Panel title="Next trips">
          {upcoming.length === 0 ? (
            <p className="text-sm text-on-surface-variant">
              {t('No trips are scheduled or running.')}
            </p>
          ) : (
            <ul className="divide-y divide-outline-variant/30">
              {upcoming.map((x) => (
                <li key={x.id}>
                  <Link
                    to={ROUTES.adminTrips}
                    className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 py-3 text-sm transition-colors hover:text-teal-flow"
                  >
                    <span className="font-semibold text-deep-river">{formatDateTime(x.departureTime)}</span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-deep-river">{x.routeName}</span>
                      <span className={`block text-xs ${x.boatId ? 'text-on-surface-variant' : 'font-medium text-coral-glow'}`}>
                        {x.boatId ? x.boatCode : t('No boat yet')}
                      </span>
                    </span>
                    <TripStatusBadge status={x.status} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </>
  )
}
