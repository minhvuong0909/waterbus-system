import { t } from '../../../i18n'

/** ISO weekdays as the backend stores them: 1 = Monday … 7 = Sunday. */
export const WEEK_DAYS = [1, 2, 3, 4, 5, 6, 7] as const

const DAY_NAME = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

export const dayName = (day: number): string => t(DAY_NAME[day - 1])

/** "1,2,5" → [1, 2, 5]; anything that is not an ISO weekday is dropped. */
export function parseDays(daysOfWeek: string): number[] {
  return daysOfWeek
    .split(',')
    .map(Number)
    .filter((day) => Number.isInteger(day) && day >= 1 && day <= 7)
    .sort((a, b) => a - b)
}

/** [5, 1, 2] → "1,2,5": sorted and without repeats, as `DaysOfWeek` expects. */
export const formatDays = (days: number[]): string =>
  [...new Set(days)].sort((a, b) => a - b).join(',')

/** Operating days in words: "Every day", a range such as "Mon – Fri", or the days listed. */
export function daysLabel(daysOfWeek: string): string {
  const days = parseDays(daysOfWeek)
  if (days.length === 7) return t('Every day')
  const consecutive = days.every((day, i) => i === 0 || day === days[i - 1] + 1)
  if (days.length >= 3 && consecutive) return `${dayName(days[0])} – ${dayName(days[days.length - 1])}`
  return days.map(dayName).join(', ')
}
