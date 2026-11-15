import { combineFetches, useFetch, useReload } from '../../../hooks'
import { getOpenIncidents, getOperationLogs, getOperationTrips } from '../../operations/services/operationsService'
import { useFleetHub } from '../../operations/hooks/useFleetHub'
import { pendingCountOf, sortAlerts } from '../utils/alertUtils'
import { useAutoRefresh } from './useAutoRefresh'

/**
 * Dữ liệu của trang Sự cố: chuyến vận hành (để biết thao tác được phép), nhật ký thao tác và các sự cố đang mở.
 * Sự cố mới, SOS và mọi lần đổi trạng thái chuyến (do Admin khác hoặc thuyền trưởng) tự hiện qua hub; ngoài ra
 * đồng bộ REST mỗi phút. `version` đổi mỗi khi có tải lại, để các thẻ cảnh báo đang mở tải lại danh sách chuyến.
 */
export function useIncidentBoard() {
  const [tripVersion, reloadTrips] = useReload()
  const [alertVersion, reloadAlerts] = useReload()
  const trips = useFetch(getOperationTrips, [tripVersion])
  const logs = useFetch(getOperationLogs, [tripVersion])
  // Tải mọi sự cố mở (số chuyến chờ quyết định tính trên toàn bộ, không theo bộ lọc hiển thị). Chỉ là danh sách
  // tóm tắt: chuyến của từng sự cố được AlertCard tải khi Admin mở sự cố đó.
  const alerts = useFetch(() => getOpenIncidents(), [alertVersion])

  const reloadAll = () => {
    reloadTrips()
    reloadAlerts()
  }
  useFleetHub({ onIncident: reloadAlerts, onTripStatus: reloadAll, onReconnected: reloadAll })
  useAutoRefresh(reloadAll, trips.fetching || logs.fetching || alerts.fetching)

  const openAlerts = alerts.data ? sortAlerts(alerts.data) : undefined
  return {
    state: combineFetches(trips, logs, alerts),
    trips: trips.data,
    logs: logs.data,
    alerts: openAlerts,
    waitingTrips: openAlerts?.reduce((sum, alert) => sum + pendingCountOf(alert), 0) ?? 0,
    // Hai bộ đếm chỉ tăng nên tổng cũng chỉ tăng: đổi khi một trong hai đổi.
    version: tripVersion + alertVersion,
    reloadAlerts,
    reloadAll,
  }
}
