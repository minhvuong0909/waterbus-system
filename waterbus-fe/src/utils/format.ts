/** Money is always written the Vietnamese way (AGENTS.md), whatever the interface language. */
const MONEY_LOCALE = 'vi-VN'

/** 15000 -> "15.000" */
export function formatNumber(amount: number): string {
  return amount.toLocaleString(MONEY_LOCALE)
}

/** 15000 -> "15.000 VND" */
export function formatVndSuffix(amount: number): string {
  return `${formatNumber(amount)} VND`
}

/** 15000 -> "15.000 VND" in both languages. */
export function formatVnd(amount: number): string {
  return formatVndSuffix(amount)
}
