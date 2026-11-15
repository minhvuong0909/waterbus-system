import { t } from '../../i18n'
import type { OperationType, WeatherAlert } from './types'
import type { TripStatus, TripStopDto } from '../trips/types'

/** Các thao tác Admin dùng từ menu chuyến (Start / Arrive / SOS của thuyền trưởng có nút riêng). */
export const DISPATCH_OPERATIONS: OperationType[] = ['Suspend', 'Resume', 'Cancel', 'Terminate', 'Complete']

/** Chuyến đã kết thúc hẳn: không làm gì thêm được. */
export const isFinished = (status: TripStatus): boolean =>
  status === 'Completed' || status === 'Cancelled' || status === 'Terminated'

/** Chỉ bến có khách lên và có giờ tàu rời mới có cửa sổ check-in; bến cuối chỉ cho xuống. */
export const isBoardingStop = (stop: TripStopDto): boolean =>
  stop.canBoard && Boolean(stop.estimatedDepartureTime)

/** Chuyến còn phải chạy hoặc đang chạy: thuyền trưởng còn việc với chuyến này. */
export const isUpcomingOrRunning = (status: TripStatus): boolean =>
  status === 'Scheduled' || status === 'Boarding' || status === 'EnRoute' || status === 'Suspended'

/** Tiêu đề của cảnh báo; cảnh báo thời tiết mang tốc độ gió trong thông điệp. */
export function weatherAlertTitle(alert: WeatherAlert): string {
  if (alert.kind && alert.kind !== 'Weather') return t('Captain emergency report')
  return t('Level {level} weather warning', { level: alert.level })
}

/** Nhãn nút và nhãn nhật ký của từng thao tác. */
export const OPERATION_LABEL: Record<OperationType, string> = {
  OpenBoarding: 'Open boarding',
  Complete: 'Close trip',
  NoAction: 'No action needed',
  Suspend: 'Suspend',
  Resume: 'Resume',
  Cancel: 'Cancel trip',
  Terminate: 'Terminate',
  Start: 'Start',
  Arrive: 'Arrive',
  SOS: 'SOS',
}
