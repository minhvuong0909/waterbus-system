/** Hiển thị giờ theo Asia/Ho_Chi_Minh (backend trả UTC). */
const TZ = 'Asia/Ho_Chi_Minh';

export const formatVnTime = (iso?: string | null): string =>
  iso ? new Date(iso).toLocaleTimeString('vi-VN', { timeZone: TZ, hour: '2-digit', minute: '2-digit', hour12: false }) : '--:--';

export const formatVnDate = (iso?: string | null): string =>
  iso ? new Date(iso).toLocaleDateString('vi-VN', { timeZone: TZ, day: '2-digit', month: '2-digit', year: 'numeric' }) : '';

export const formatVnWindow = (from?: string | null, to?: string | null): string =>
  from && to ? `${formatVnTime(from)}–${formatVnTime(to)}` : 'chưa mở';
