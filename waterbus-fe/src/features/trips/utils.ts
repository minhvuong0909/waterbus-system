import { t } from '../../i18n'
import type { DateOption, Trip } from './types'

/** "Today, Dec 16" for today's date, otherwise "Tue, Dec 17". `date` must already be translated. */
export function formatDateLabel(date: DateOption): string {
  return date.isToday
    ? t('Today, {label}', { label: date.label })
    : t('{weekday}, {label}', { weekday: date.weekday, label: date.label })
}

/** "28 Seats Left", or "54 Seats Available" when plenty remain. */
export function seatsLabel(trip: Pick<Trip, 'seatsCount' | 'seatsLow'>): string {
  if (trip.seatsLow) return t('Limited: Only {count} Seats Left', { count: trip.seatsCount })
  return trip.seatsCount >= 50
    ? t('{count} Seats Available', { count: trip.seatsCount })
    : t('{count} Seats Left', { count: trip.seatsCount })
}
