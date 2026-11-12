import { numberLocale } from '../i18n'
/** Local calendar date as `YYYY-MM-DD` (not UTC, so "today" matches the user's wall clock). */
export function toIsoDate(date: Date): string {
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const dd = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${mm}-${dd}`
}

/** Calendar date in Vietnam as `YYYY-MM-DD` for an instant, whatever the device time zone. */
export function toVietnamDate(date: Date): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(date)
}

/** Giờ hiển thị tại Việt Nam, độc lập múi giờ thiết bị. */
export function formatClock(date: Date): string {
  return new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' }).format(date)
}

/** Minutes since midnight of an "HH:mm" clock time. */
export function minutesOfClock(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

/**
 * Minutes since midnight in Vietnam for an instant. Giờ chuyến là giờ Việt Nam, nên so với giờ máy
 * (`getHours`) sẽ sai khi máy ở múi giờ khác (vd máy chủ CI chạy UTC).
 */
export function vietnamMinutesOfDay(date: Date): number {
  return minutesOfClock(formatClock(date))
}

/** "Oct 12, 07:30" in Vietnam time and the active language. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(numberLocale(), {
    timeZone: 'Asia/Ho_Chi_Minh',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** "Oct 12, 2026" in Vietnam time and the active language. */
export function formatLongDate(iso: string): string {
  return new Date(iso).toLocaleDateString(numberLocale(), {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

/** "07:30:15" in Vietnam time, for live data that changes every few seconds. */
export function formatClockWithSeconds(iso: string): string {
  return new Date(iso).toLocaleTimeString(numberLocale(), {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

/** Value of an `<input type="datetime-local">` (`YYYY-MM-DDTHH:mm`) showing `iso` in Vietnam time. */
export function toVietnamDateTimeInput(iso: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(iso))
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
}

/** UTC ISO instant of a `datetime-local` value read as Vietnam time (UTC+7, no daylight saving). */
export function fromVietnamDateTimeInput(value: string): string {
  return new Date(`${value}:00+07:00`).toISOString()
}
