import { formatClock } from '../../utils/time'

/** Đồng hồ GPS 24 giờ, không phụ thuộc ngôn ngữ hoặc múi giờ của thiết bị. */
export const formatGpsTime = (iso: string) => {
  const date = new Date(iso)
  return `${formatClock(date)}:${String(date.getUTCSeconds()).padStart(2, '0')}`
}
