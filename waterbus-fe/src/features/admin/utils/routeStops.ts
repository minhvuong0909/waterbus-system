import { t } from '../../../i18n'
import type { RouteStopInput, ServiceType } from '../types'

/**
 * Lỗi của danh sách bến dừng, hoặc `undefined` khi hợp lệ. Tuyến Thường: mỗi bến một lần, bến cuối khác bến đầu
 * (BR-ROUTE-06); tuyến ngắm cảnh được đi vòng về bến xuất phát.
 */
export function stopsError(stops: RouteStopInput[], serviceType: ServiceType): string | undefined {
  if (!stops.every((stop) => stop.stationId)) return t('Choose a station for every stop.')
  if (serviceType === 'Regular') {
    if (stops.length > 1 && stops[0].stationId === stops[stops.length - 1].stationId) {
      return t('A regular route must end at a different station than it starts.')
    }
    if (new Set(stops.map((stop) => stop.stationId)).size !== stops.length) {
      return t('A regular route calls at each station only once.')
    }
  }
  return undefined
}
