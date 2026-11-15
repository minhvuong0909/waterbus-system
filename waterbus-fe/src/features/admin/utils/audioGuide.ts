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

/** Kịch bản thuyết minh mẫu chuẩn du lịch cho các di tích, thắng cảnh ven sông Sài Gòn. */
export const POI_SUGGESTIONS: Record<string, string> = {
  'cột cờ thủ ngữ':
    'Kính thưa quý khách, bên mạn phải tàu là Cột cờ Thủ Ngữ và Bến Nhà Rồng. Nơi đây từng là ngã ba kênh Bến Nghé sầm uất, và là di tích lịch sử thiêng liêng, nơi người thanh niên yêu nước Nguyễn Tất Thành đã ra đi tìm đường cứu nước vào năm 1911.',
  'bến nhà rồng':
    'Kính thưa quý khách, bên mạn phải tàu là Cột cờ Thủ Ngữ và Bến Nhà Rồng. Nơi đây từng là ngã ba kênh Bến Nghé sầm uất, và là di tích lịch sử thiêng liêng, nơi người thanh niên yêu nước Nguyễn Tất Thành đã ra đi tìm đường cứu nước vào năm 1911.',
  'cầu ba son':
    'Kính thưa quý khách, phía trước chúng ta là Cầu Ba Son, trước đây mang tên Cầu Thủ Thiêm 2. Cây cầu dây văng hiện đại với thiết kế hình cánh buồm vươn cao, biểu tượng mới kết nối trung tâm Quận 1 với khu đô thị tài chính mới Thủ Thiêm.',
  'thủ thiêm 2':
    'Kính thưa quý khách, phía trước chúng ta là Cầu Ba Son, trước đây mang tên Cầu Thủ Thiêm 2. Cây cầu dây văng hiện đại với thiết kế hình cánh buồm vươn cao, biểu tượng mới kết nối trung tâm Quận 1 với khu đô thị tài chính mới Thủ Thiêm.',
  'landmark 81':
    'Quý khách đang chiêm ngưỡng tòa tháp Landmark 81 vươn cao kiêu hãnh bên bờ sông Sài Gòn. Với độ cao 461,3 mét, đây là tòa nhà cao nhất Việt Nam và là biểu tượng cho sự thịnh vượng, năng động vượt bậc của thành phố.',
  'vinhomes':
    'Quý khách đang chiêm ngưỡng tòa tháp Landmark 81 vươn cao kiêu hãnh bên bờ sông Sài Gòn. Với độ cao 461,3 mét, đây là tòa nhà cao nhất Việt Nam và là biểu tượng cho sự thịnh vượng, năng động vượt bậc của thành phố.',
  'thanh đa':
    'Chào mừng quý khách đến với khúc sông uốn lượn quanh Bán đảo Thanh Đa và Bình Quới. Nơi đây được ví như một lá phổi xanh thanh bình giữa lòng đô thị, với những rặng dừa nước và không gian sinh thái miệt vườn đặc trưng Nam Bộ.',
  'bình quới':
    'Chào mừng quý khách đến với khúc sông uốn lượn quanh Bán đảo Thanh Đa và Bình Quới. Nơi đây được ví như một lá phổi xanh thanh bình giữa lòng đô thị, với những rặng dừa nước và không gian sinh thái miệt vườn đặc trưng Nam Bộ.',
  'bình lợi':
    'Phía trước mắt quý khách là Cầu Bình Lợi, cây cầu đường sắt đầu tiên vượt sông Sài Gòn được xây dựng từ năm 1902. Cây cầu đã chứng kiến hơn một thế kỷ thăng trầm cùng những dấu ấn lịch sử hào hùng của Sài Gòn.',
}

/** Trả về kịch bản gợi ý chuẩn du lịch cho một địa điểm cụ thể. */
export function getSuggestionForPoi(poiName: string): string {
  const lower = poiName.toLowerCase()
  for (const [key, text] of Object.entries(POI_SUGGESTIONS)) {
    if (lower.includes(key)) {
      return text
    }
  }
  return `Kính chào quý khách, tàu đang đi qua khu vực ${poiName}. Kính mời quý khách cùng chiêm ngưỡng cảnh quan tuyệt đẹp đôi bờ sông Sài Gòn.`
}
