/**
 * Quy tắc giao diện của khu điều hành (dùng các hằng dưới đây thay vì tự viết lại lớp Tailwind):
 * - Khung (Panel, bảng, bộ lọc, thẻ số liệu, thẻ cảnh báo): `SURFACE_CLASS`, bo `rounded-2xl`, viền /30.
 * - Ô bên trong khung (danh sách con, ghi chú) và ô nhập: bo `rounded-xl`; ô con dùng `INSET_CLASS`.
 * - Nút: `Button` (bo tròn hẳn, dùng chung toàn site). Nhãn trạng thái: `rounded-full`.
 * - Viền khung và đường chia: `outline-variant/30`. Ô nhập, ô chọn: `/70` để đủ tương phản.
 * - Nhãn và tiêu đề không viết hoa toàn bộ.
 */
export const SURFACE_CLASS = 'rounded-2xl border border-outline-variant/30 bg-white shadow-card'

/** Ô con nằm trong một khung: danh sách chuyến của cảnh báo, danh sách vé… */
export const INSET_CLASS = 'rounded-xl border border-outline-variant/30'

/** Kiểu ô nhập của khu điều hành. */
export const INPUT_CLASS =
  'min-h-[42px] w-full rounded-xl border border-outline-variant/70 bg-white px-3 py-2 text-sm text-deep-river transition-colors focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15 disabled:bg-surface-container-low'

/** Ô đánh dấu (checkbox) của khu điều hành. */
export const CHECKBOX_CLASS = 'h-4 w-4 rounded border-outline-variant/70 text-teal-flow focus:ring-teal-flow'

/** Thêm vào nhãn của ô bắt buộc: hiện dấu (*) bằng CSS nên không lẫn vào chữ của nhãn. */
export const REQUIRED_LABEL_CLASS = "after:ml-0.5 after:text-coral-glow after:content-['*']"
