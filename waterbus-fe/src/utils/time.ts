/** addMinutes("08:30", -15) -> "08:15". Wraps around midnight. */
export function addMinutes(time: string, delta: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = (((h * 60 + m + delta) % 1440) + 1440) % 1440
  const hh = String(Math.floor(total / 60)).padStart(2, '0')
  const mm = String(total % 60).padStart(2, '0')
  return `${hh}:${mm}`
}

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
