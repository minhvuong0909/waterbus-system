import { Link } from 'react-router-dom'
import { ErrorState, Icon, PageHeader, PageLoader, Panel, StatCard } from '../../components/ui'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { getOpenIncidents, getOperationTrips } from '../../features/operations/services/operationsService'
import { countReconciliationToDo, listPendingRefunds } from '../../features/admin/services/refundService'
import { attentionItems } from '../../features/admin/utils/attention'
import type { AttentionItem } from '../../features/admin/utils/attention'
import { pendingCountOf } from '../../features/admin/utils/alertUtils'
import { formatDateTime, toVietnamDate } from '../../utils/time'
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
  const trips = useFetch(getOperationTrips, [version])
  const alerts = useFetch(() => getOpenIncidents(), [version])
  const pendingRefunds = useFetch(countOf(() => listPendingRefunds(1, 1)), [version])
  const reconcile = useFetch(() => countReconciliationToDo().catch(() => undefined), [version])
  // Số đếm, việc cần xử lý và các chuyến sắp tới đổi theo trạng thái chuyến và cảnh báo mới ngay khi có.
  useFleetHub(
    { onTripStatus: reload, onIncident: reload, onReconnected: reload },
  )
  // Mốc "bây giờ" cập nhật mỗi phút, để "hôm nay" và "sắp tới" tự đổi khi để trang mở lâu.
  const now = useNow(60_000)
  const state = combineFetches(trips, alerts)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !alerts.data) return <PageLoader />

  // "Hôm nay" theo giờ Việt Nam, không theo múi giờ của máy đang mở trang.
  const today = toVietnamDate(new Date(now))
  const todays = trips.data.filter((x) => toVietnamDate(new Date(x.departureTime)) === today)
  // Chuyến đang đón khách/đang chạy, hoặc theo lịch chưa tới giờ khởi hành. Không gồm chuyến đã cập bến/kết thúc,
  // chuyến theo lịch đã quá giờ, hay chuyến tạm ngưng (việc của "Cần xử lý").
  const upcoming = trips.data
    .filter((x) => x.status === 'Boarding' || x.status === 'EnRoute' || (x.status === 'Scheduled' && Date.parse(x.departureTime) >= now))
    .slice(0, 5)
  const pendingDecisions = alerts.data.reduce((sum, alert) => sum + pendingCountOf(alert), 0)
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
        <StatCard
          label="On the river"
          value={trips.data.filter((x) => x.status === 'EnRoute').length}
          to={ROUTES.adminRadar}
        />
        <StatCard
          label="Open alerts"
          value={alerts.data.length}
          to={ROUTES.adminIncidents}
          attention
          hint={pendingDecisions > 0 ? '{count} trips awaiting your decision' : undefined}
          hintCount={pendingDecisions}
        />
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
                    <span className="min-w-0 flex-1 truncate text-on-surface-variant">{x.routeName}</span>
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
