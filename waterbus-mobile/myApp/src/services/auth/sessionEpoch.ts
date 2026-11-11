/**
 * Review PR #57 (3rd round, High - "In-flight manifest preload can repopulate passenger data
 * after logout"): số thế hệ (epoch) của phiên đăng nhập hiện tại trên THIẾT BỊ.
 *
 * Tăng lên mỗi khi logout bắt đầu (useAuthStore.logout, dòng đồng bộ ĐẦU TIÊN, trước mọi await)
 * và mỗi khi 1 phiên mới xác thực xong (useAuthStore.login). Một tác vụ bất đồng bộ dài hơi như
 * preload manifest (2 request mạng + ghi SQLite) phải chụp epoch lúc BẮT ĐẦU rồi tự so sánh lại
 * NGAY TRƯỚC khi ghi dữ liệu - nếu epoch đã đổi (nghĩa là logout/đăng nhập khác đã xảy ra trong
 * lúc nó đang chờ mạng), phải coi phiên của nó đã chết và bỏ kết quả, không ghi gì vào SQLite hay
 * state dùng chung của thiết bị.
 *
 * Đặt ở module riêng (không phải trong useAuthStore.ts) để useScannerStore/syncService import
 * được mà không tạo vòng phụ thuộc với useAuthStore (useAuthStore đã import useScannerStore).
 */
let currentEpoch = 0;

export function bumpSessionEpoch(): number {
  currentEpoch += 1;
  return currentEpoch;
}

export function getSessionEpoch(): number {
  return currentEpoch;
}
