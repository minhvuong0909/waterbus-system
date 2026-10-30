import { t } from '../../i18n'
import type { AdminTripStatus, OperationType, WeatherAlert } from './types'

/** Operations the admin applies from the trip menu (the captain's Start / Arrive / SOS have their own buttons). */
export const DISPATCH_OPERATIONS: OperationType[] = ['Suspend', 'Resume', 'Cancel', 'Terminate', 'Complete']

/** The trip is over for good: nothing more can be done with it. */
export const isFinished = (status: AdminTripStatus): boolean =>
  status === 'Completed' || status === 'Cancelled' || status === 'Terminated'

/** The trip still has to run or is running: the captain has something to do with it. */
export const isUpcomingOrRunning = (status: AdminTripStatus): boolean =>
  status === 'Scheduled' || status === 'Boarding' || status === 'EnRoute' || status === 'Suspended'

/** Heading of an alert; a weather alert carries the wind speed in its message. */
export function weatherAlertTitle(alert: WeatherAlert): string {
  if (alert.kind && alert.kind !== 'Weather') return t('Captain emergency report')
  return t('Level {level} weather warning', { level: alert.level })
}
