import type { CachedScannerContext } from './scannerCredentials';

/** Thời điểm theo đồng hồ máy chủ, ngoại suy từ lần tải gần nhất của ngữ cảnh. */
export const serverNow = (context: CachedScannerContext, deviceNow = Date.now()) =>
  context.serverTime + deviceNow - context.deviceTime;

/**
 * Chọn TripStopCall đã được máy chủ cấp cho chuyến của booking. TripId trong QR chỉ dùng để ĐỊNH TUYẾN
 * giữa các ngữ cảnh đã cấp quyền; không tự cấp quyền (không có ngữ cảnh ⇒ null).
 * Nhiều điểm dừng cùng chuyến tại bến (tour vòng): ưu tiên cái đang trong cửa sổ soát, rồi cái mở sớm nhất.
 */
export function pickContextForTrip(
  contexts: CachedScannerContext[], tripId: string, deviceNow = Date.now(),
): CachedScannerContext | null {
  const candidates = contexts.filter((context) => context.assignment.tripId === tripId);
  const open = (c: CachedScannerContext) => {
    const at = serverNow(c, deviceNow);
    const { checkInOpenAt: from, checkInCloseAt: to } = c.assignment;
    return !!from && !!to && at >= Date.parse(from) && at <= Date.parse(to);
  };
  return candidates.sort((a, b) =>
    Number(open(b)) - Number(open(a)) ||
    Date.parse(a.assignment.checkInOpenAt ?? '') - Date.parse(b.assignment.checkInOpenAt ?? ''))[0] ?? null;
}
