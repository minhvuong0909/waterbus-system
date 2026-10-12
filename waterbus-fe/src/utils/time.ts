/** addMinutes("08:30", -15) -> "08:15". Wraps around midnight. */
export function addMinutes(time: string, delta: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = (((h * 60 + m + delta) % 1440) + 1440) % 1440
  const hh = String(Math.floor(total / 60)).padStart(2, '0')
  const mm = String(total % 60).padStart(2, '0')
  return `${hh}:${mm}`
}
