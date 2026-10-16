import { getLocale, numberLocale } from '../i18n'

/** 15000 -> "15,000" (English) or "15.000" (Vietnamese). */
export function formatNumber(amount: number): string {
  return amount.toLocaleString(numberLocale())
}

/** 15000 -> "15,000 VND" (both languages). */
export function formatVndSuffix(amount: number): string {
  return `${formatNumber(amount)} VND`
}

/** 15000 -> "VND 15,000" in English, "15.000 VND" in Vietnamese. */
export function formatVnd(amount: number): string {
  return getLocale() === 'vi' ? formatVndSuffix(amount) : `VND ${formatNumber(amount)}`
}
