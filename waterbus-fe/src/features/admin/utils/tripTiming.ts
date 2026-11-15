import { t } from '../../../i18n'

/** Hai mốc tuỳ chọn của chuyến mới, như Admin nhập: số phút trước giờ khởi hành. */
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
 * Các mốc ở dạng request lập chuyến cần. Để trống thì đóng bán đúng giờ khởi hành (BR-TIME-01) và giờ mở check-in
 * được đặt sau theo từng bến (BR-CFG-01).
 */
export const toTimingInput = (timing: TripTiming) => ({
  salesCloseMinutesBeforeDeparture: minutesOrNull(timing.salesClose),
  checkInOpenMinutesBeforeDeparture: minutesOrNull(timing.checkInOpen),
})
