/**
 * Đọc số từ ô nhập một cách chặt: `Number()` nhận cả "1e3", "0x10", "Infinity", " " hay "" (=0) nên không dùng cho
 * ô nhập của người dùng. Trả về `null` khi chuỗi không đúng dạng số hoặc nằm ngoài khoảng.
 */
const DIGITS = /^\d{1,15}$/
const DECIMAL = /^\d{1,15}(\.\d+)?$/
const SIGNED_DECIMAL = /^-?\d{1,15}(\.\d+)?$/

/** Số nguyên không âm, ví dụ "45". Không nhận dấu, số mũ, dấu cách hay số thập phân. */
export function parseWhole(value: string, min: number, max: number): number | null {
  const text = value.trim()
  if (!DIGITS.test(text)) return null
  const n = Number(text)
  return n >= min && n <= max ? n : null
}

/** Số thập phân không âm với dấu chấm ("12.5"), tối đa `decimals` chữ số sau dấu chấm. */
export function parseDecimal(value: string, min: number, max: number, decimals: number): number | null {
  const text = value.trim()
  if (!DECIMAL.test(text) || (text.split('.')[1]?.length ?? 0) > decimals) return null
  const n = Number(text)
  return n >= min && n <= max ? n : null
}

/** Như `parseDecimal` nhưng nhận số âm, dùng cho toạ độ. */
export function parseSignedDecimal(value: string, min: number, max: number, decimals: number): number | null {
  const text = value.trim()
  if (!SIGNED_DECIMAL.test(text) || (text.split('.')[1]?.length ?? 0) > decimals) return null
  const n = Number(text)
  return n >= min && n <= max ? n : null
}

/** Mã nghiệp vụ (bến, tàu, tuyến, hạng ghế, ghế): chữ, số, '.', '_', '-' — không dấu cách hay dấu tiếng Việt. */
export const isCode = (value: string) => /^[A-Za-z0-9._-]+$/.test(value.trim())

/** Mã đã có trong danh sách (không phân biệt hoa thường). */
export const codeTaken = (value: string, others: readonly { code: string }[]) =>
  others.some((other) => other.code.trim().toUpperCase() === value.trim().toUpperCase())

/** Tên người có ít nhất một chữ cái (chặn "123", "@@@"). */
export const hasLetter = (value: string) => /\p{L}/u.test(value)

/** Email, tên và mã tối đa 150 ký tự (cột của backend). */
export const MAX_NAME_LENGTH = 150

/** Email đúng dạng `a@b.c`, không dấu cách, tối đa 150 ký tự. */
export const isEmail = (value: string) =>
  value.trim().length <= MAX_NAME_LENGTH && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())

/** Họ tên: có chữ cái, tối đa 150 ký tự. */
export const isPersonName = (value: string) => hasLetter(value) && value.trim().length <= MAX_NAME_LENGTH

/** Số điện thoại tài khoản theo backend: 6–20 ký tự gồm số, `+`, `(`, `)`, `-` và dấu cách; phải có ít nhất 6 chữ số. */
export const isAccountPhone = (value: string) =>
  /^[0-9+()\-\s]{6,20}$/.test(value.trim()) && value.replace(/\D/g, '').length >= 6

/** Mật khẩu mới theo cấu hình Identity của backend: ≥ 8 ký tự, có chữ hoa, chữ thường và chữ số; tối đa 100. */
export const isNewPassword = (value: string) =>
  value.length >= 8 && value.length <= 100 && /[a-z]/.test(value) && /[A-Z]/.test(value) && /\d/.test(value)

/** Ngày dạng `YYYY-MM-DD` hoặc `YYYY-MM-DDTHH:mm` có năm hợp lý (ô ngày của trình duyệt nhận cả năm 0001 hay 275760). */
export const MIN_YEAR = 2000
export const MAX_YEAR = 2100
export function hasSaneYear(value: string): boolean {
  const year = Number(value.slice(0, 4))
  return value.length >= 10 && year >= MIN_YEAR && year <= MAX_YEAR
}
