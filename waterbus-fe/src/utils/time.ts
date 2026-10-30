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

/** "07:30:15" in Vietnam time, for live data that changes every few seconds. */
export function formatClockWithSeconds(iso: string): string {
  return new Date(iso).toLocaleTimeString(numberLocale(), {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}
