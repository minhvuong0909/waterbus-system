/** A landmark of the database (`GET /audioguides/pois`, BE `AudioGuidePoiResponse`). */
export interface NarrationPoi {
  poiId: string
  name: string
  hasActiveRoutePoi: boolean
  audioGuideId: string | null
  audioAssetUri: string | null
  isPublished: boolean
}

/** Azure neural voices for Vietnamese (`AzureSpeech` voice names). */
export const NARRATION_VOICES = [
  { id: 'vi-VN-HoaiMyNeural', label: 'Female voice (Hoài My)' },
  { id: 'vi-VN-NamMinhNeural', label: 'Male voice (Nam Minh)' },
] as const

export type NarrationVoice = (typeof NARRATION_VOICES)[number]['id']

/** Azure TTS accepts up to 10,000 characters per script (`SynthesizeAudioCommandValidator`). */
export const MAX_NARRATION_LENGTH = 10_000

/**
 * Kịch bản thuyết minh mẫu chuẩn du lịch cho các di tích, thắng cảnh ven sông Sài Gòn.
 * Mỗi kịch bản phải nằm trong giới hạn ký tự của ngân sách thời gian một chặng
 * (xem MAX_NARRATION_CHARACTERS) để Admin xuất bản được ngay sau khi bấm "Dùng gợi ý này".
 */
export const POI_SUGGESTIONS: Record<string, string> = {
  'cột cờ thủ ngữ':
    'Kính thưa quý khách, bên mạn phải tàu là Cột cờ Thủ Ngữ và Bến Nhà Rồng, nơi Bác Hồ từng ra đi tìm đường cứu nước năm 1911.',
  'bến nhà rồng':
    'Kính thưa quý khách, bên mạn phải tàu là Cột cờ Thủ Ngữ và Bến Nhà Rồng, nơi Bác Hồ từng ra đi tìm đường cứu nước năm 1911.',
  'cầu ba son':
    'Phía trước chúng ta là Cầu Ba Son, cây cầu dây văng hình cánh buồm nối trung tâm Quận 1 với Thủ Thiêm.',
  'thủ thiêm 2':
    'Phía trước chúng ta là Cầu Ba Son, cây cầu dây văng hình cánh buồm nối trung tâm Quận 1 với Thủ Thiêm.',
  'landmark 81':
    'Quý khách đang chiêm ngưỡng tòa tháp Landmark 81, cao 461,3 mét, biểu tượng cho sự thịnh vượng của Sài Gòn.',
  'vinhomes':
    'Quý khách đang chiêm ngưỡng tòa tháp Landmark 81, cao 461,3 mét, biểu tượng cho sự thịnh vượng của Sài Gòn.',
  'thanh đa':
    'Chào mừng quý khách đến với khúc sông uốn lượn quanh Bán đảo Thanh Đa và Bình Quới xanh mát.',
  'bình quới':
    'Chào mừng quý khách đến với khúc sông uốn lượn quanh Bán đảo Thanh Đa và Bình Quới xanh mát.',
  'bình lợi':
    'Phía trước mắt quý khách là Cầu Bình Lợi, cây cầu đường sắt đầu tiên vượt sông Sài Gòn, xây năm 1902.',
}

/** Giới hạn ký tự của kịch bản thuyết minh, khớp với ngân sách thời gian một chặng của BE. */
export const MAX_NARRATION_CHARACTERS = 140

/** Trả về kịch bản gợi ý chuẩn du lịch cho một địa điểm cụ thể. */
export function getSuggestionForPoi(poiName: string): string {
  const lower = poiName.toLowerCase()
  for (const [key, text] of Object.entries(POI_SUGGESTIONS)) {
    if (lower.includes(key)) {
      return text
    }
  }
  const fallback = `Kính chào quý khách, tàu đang đi qua ${poiName}. Mời quý khách chiêm ngưỡng hai bờ sông Sài Gòn.`
  return fallback.length <= MAX_NARRATION_CHARACTERS
    ? fallback
    : `Kính chào quý khách, tàu đang đi qua ${poiName.slice(0, 60)}. Mời quý khách chiêm ngưỡng hai bờ sông Sài Gòn.`.slice(
        0,
        MAX_NARRATION_CHARACTERS,
      )
}
