import type { StopAnnouncementEvent } from '../../operations/hooks/useFleetHub'

/** Loại bỏ tiền tố "Bến " lặp để tránh đọc "bến Bến Thủ Thiêm" */
export function cleanStationName(name?: string | null): string {
  if (!name) return ''
  return name.replace(/^Bến\s+/i, '').trim()
}

/** Tạo câu thông báo trạm bến bằng tiếng Việt tự nhiên, chuẩn xác */
export function getAnnouncementSentence(event: StopAnnouncementEvent): string {
  const eventType = event.type || event.eventType || 'departing'
  let customTexts: Record<string, string> = {}
  try {
    const saved = localStorage.getItem('waterbus_announcement_custom_texts')
    if (saved) customTexts = JSON.parse(saved)
  } catch { /* ignore */ }

  const stationRaw = event.stationName || 'Bến tàu'
  const station = cleanStationName(stationRaw) || stationRaw
  const nextRaw = event.nextStationName ?? ''
  const nextStation = cleanStationName(nextRaw)

  if (eventType === 'departing') {
    if (!nextStation) {
      // Rời bến cuối cùng (không có bến tiếp theo)
      return `Tàu đang rời bến ${station}. Đây là chặng cuối của hành trình. Xin cảm ơn quý khách đã đồng hành cùng tàu buýt sông.`
    }
    const raw =
      customTexts['departing'] ||
      'Tàu đang rời bến {Bến hiện tại}, điểm đến tiếp theo: Bến {Bến kế tiếp}.'
    return raw
      .replace(/{Bến hiện tại}/g, station)
      .replace(/{Bến kế tiếp}/g, nextStation)
  }

  if (eventType === 'near') {
    const nearStation = nextStation || station
    const raw =
      customTexts['near'] ||
      'Tàu sắp đến bến {Bến kế tiếp}. Quý khách vui lòng chuẩn bị tư trang hành lý.'
    return raw
      .replace(/{Bến kế tiếp}/g, nearStation)
      .replace(/{Bến hiện tại}/g, nearStation)
  }

  // arrived
  const raw =
    customTexts['arrived_mid'] ||
    'Tàu đã đến bến {Bến hiện tại}. Tàu dừng tại bến để quý khách lên xuống.'
  return raw.replace(/{Bến hiện tại}/g, station).replace(/{Bến cuối}/g, station)
}
