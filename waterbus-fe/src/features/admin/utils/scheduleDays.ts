import { t } from '../../../i18n'

/** Thứ trong tuần theo ISO như backend lưu: 1 = thứ Hai … 7 = Chủ nhật. */
export const WEEK_DAYS = [1, 2, 3, 4, 5, 6, 7] as const

const DAY_NAME = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

export const dayName = (day: number): string => t(DAY_NAME[day - 1])

/** "1,2,5" → [1, 2, 5]; giá trị không phải thứ ISO bị bỏ. */
export function parseDays(daysOfWeek: string): number[] {
  return daysOfWeek
    .split(',')
    .map(Number)
    .filter((day) => Number.isInteger(day) && day >= 1 && day <= 7)
    .sort((a, b) => a - b)
}

/** [5, 1, 2] → "1,2,5": đã sắp xếp và bỏ trùng, đúng dạng `DaysOfWeek` cần. */
export const formatDays = (days: number[]): string =>
  [...new Set(days)].sort((a, b) => a - b).join(',')

/** Ngày chạy bằng chữ: "Hằng ngày", một khoảng như "T2 – T6", hoặc liệt kê từng ngày. */
export function daysLabel(daysOfWeek: string): string {
  const days = parseDays(daysOfWeek)
  if (days.length === 7) return t('Every day')
  const consecutive = days.every((day, i) => i === 0 || day === days[i - 1] + 1)
  if (days.length >= 3 && consecutive) return `${dayName(days[0])} – ${dayName(days[days.length - 1])}`
  return days.map(dayName).join(', ')
}
