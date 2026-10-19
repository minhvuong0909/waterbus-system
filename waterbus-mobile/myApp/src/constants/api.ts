/**
 * Cấu hình địa chỉ backend API. Có thể đổi runtime qua Settings (ScannerSettingsScreen) sau;
 * hiện tại là hằng số build-time đọc từ biến môi trường EXPO_PUBLIC_API_BASE_URL.
 */
function developmentHost(): string {
  if (typeof window !== 'undefined' && window.location?.hostname) return window.location.hostname;
  try {
    const constants = require('expo-constants').default;
    const host = constants.expoConfig?.hostUri?.split(':')[0];
    if (host) return host;
  } catch { /* Node test runner. */ }
  return '127.0.0.1';
}

export const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL?.replace(/\/+$/, '') || `http://${developmentHost()}:5000`;

export const API_ENDPOINTS = {
  login: '/api/v1/auth/login',
  assignments: '/api/v1/checkins/assignments/mine',
  manifest: (id: string) => `/api/v1/checkins/manifest/${id}`,
  publicKey: '/api/v1/checkouts/qr-public-key',
  sync: '/api/v1/checkins/sync',
} as const;
