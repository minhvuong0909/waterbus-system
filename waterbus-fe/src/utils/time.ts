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

/** "08:30" in the user's local time zone. */
export function formatClock(date: Date): string {
  const hh = String(date.getHours()).padStart(2, '0')
  const mm = String(date.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}
