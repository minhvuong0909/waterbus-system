import { t } from '../../../i18n'

/** The two optional marks of a new trip, as typed: minutes before its departure. */
export interface TripTiming {
  salesClose: string
  checkInOpen: string
}

export const EMPTY_TIMING: TripTiming = { salesClose: '', checkInOpen: '' }

const optionalMinutes = (value: string, min: number) => {
  const n = Number(value)
  return value.trim() === '' || (Number.isInteger(n) && n >= min && n <= 1440)
}

export function timingErrors(timing: TripTiming) {
  return {
    salesClose: optionalMinutes(timing.salesClose, 0) ? undefined : t('Enter minutes from 0 to 1440.'),
    checkInOpen: optionalMinutes(timing.checkInOpen, 1) ? undefined : t('Enter minutes from 1 to 1440.'),
  }
}

const minutesOrNull = (value: string) => (value.trim() ? Number(value) : null)

/**
 * The marks as the trip-planning requests take them. Left empty, sales close at the departure
 * (BR-TIME-01) and the check-in opening is set later per stop (BR-CFG-01).
 */
export const toTimingInput = (timing: TripTiming) => ({
  salesCloseMinutesBeforeDeparture: minutesOrNull(timing.salesClose),
  checkInOpenMinutesBeforeDeparture: minutesOrNull(timing.checkInOpen),
})
