/**
 * Cấu hình địa chỉ backend API. Có thể đổi runtime qua Settings (ScannerSettingsScreen) sau;
 * hiện tại là hằng số build-time đọc từ biến môi trường EXPO_PUBLIC_API_BASE_URL.
 */
export const API_BASE_URL =
  process.env.EXPO_PUBLIC_API_BASE_URL?.replace(/\/+$/, '') || 'http://localhost:5000';
