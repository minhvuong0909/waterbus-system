import type { Locale } from '../../i18n'

/**
 * Giới thiệu từng bến, khoá theo mã trạm của backend (ST01…ST05). Nội dung do nhóm front-end viết lại
 * từ các trang bến của Saigon Waterbus và kiến thức chung; khoảng cách chỉ mang tính tham khảo.
 * Backend chưa có mô tả bến, nên đây là nội dung biên tập tại chỗ.
 */
export interface NearbyPlace {
  name: Record<Locale, string>
  /** Khoảng cách tới bến: "100 m", "1.5 km", "1–4 km", hoặc `inside` / `opposite`. */
  distance: string
}

export interface PierInfo {
  story: Record<Locale, string>
  nearby: NearbyPlace[]
}

export const PIER_INFO: Record<string, PierInfo> = {
  ST01: {
    story: {
      vi: 'Bến Bạch Đằng nằm bên bờ sông Sài Gòn, trên đường Tôn Đức Thắng, khu bờ sông từng gắn với hoạt động cảng và thương mại của thành phố từ thời Pháp thuộc. Ngày nay đây là điểm khởi đầu của tuyến buýt sông Bạch Đằng – Linh Đông, chạy từ cuối năm 2017, và là cửa ngõ để đi bộ vào trung tâm Quận 1.',
      en: 'Bạch Đằng pier sits on the Saigon River bank along Tôn Đức Thắng street, a waterfront long tied to the city’s port and trade since the French colonial period. Today it is the starting point of the Bạch Đằng – Linh Đông river bus line, running since late 2017, and a gateway on foot to central District 1.',
    },
    nearby: [
      { name: { vi: 'Công viên bến Bạch Đằng', en: 'Bạch Đằng riverside park' }, distance: '100 m' },
      { name: { vi: 'Phố đi bộ Nguyễn Huệ', en: 'Nguyễn Huệ walking street' }, distance: '500 m' },
      { name: { vi: 'Nhà hát Thành phố', en: 'Saigon Opera House' }, distance: '600 m' },
      { name: { vi: 'Chợ Bến Thành', en: 'Bến Thành market' }, distance: '1.5 km' },
    ],
  },
  ST02: {
    story: {
      vi: 'Bến Bình An nằm trên đường số 21, phường Bình An, ở bờ đối diện với trung tâm Quận 1 (khu vực trước đây thuộc Quận 2, nay thuộc TP. Thủ Đức). Từ bến nhìn sang là toà Landmark 81, và ngay trong khuôn viên bến có quán cà phê nhìn ra sông.',
      en: 'Bình An pier is on street No. 21 in Bình An ward, on the opposite bank from central District 1 (an area that used to be District 2 and now belongs to Thủ Đức City). Landmark 81 rises across the water, and a café inside the pier grounds looks out over the river.',
    },
    nearby: [
      { name: { vi: 'Quán cà phê Katinat trong bến', en: 'Katinat café in the pier' }, distance: 'inside' },
      { name: { vi: 'Thảo Điền Pearl', en: 'Thảo Điền Pearl mall' }, distance: '900 m' },
      { name: { vi: 'Pháp viện Minh Đăng Quang', en: 'Minh Đăng Quang Dharma Institute' }, distance: '2.5 km' },
      { name: { vi: 'Landmark 81', en: 'Landmark 81' }, distance: '3 km' },
    ],
  },
  ST03: {
    story: {
      vi: 'Bến Thanh Đa nằm trên bán đảo Thanh Đa, quận Bình Thạnh, nơi sông Sài Gòn uốn một khúc cua lớn tạo thành vùng đất xanh yên tĩnh giữa thành phố. Khu vực này nổi tiếng với các khu du lịch sinh thái Bình Quới và món cháo vịt Thanh Đa.',
      en: 'Thanh Đa pier is on the Thanh Đa peninsula in Bình Thạnh district, where the Saigon River makes a wide bend around a quiet, green stretch of the city. The area is known for the Bình Quới eco-tourism villages and for Thanh Đa duck porridge.',
    },
    nearby: [
      { name: { vi: 'Nhà hàng Mars Venus trong bến', en: 'Mars Venus restaurant in the pier' }, distance: 'inside' },
      { name: { vi: 'Cháo vịt Thanh Đa', en: 'Thanh Đa duck porridge' }, distance: '500 m' },
      { name: { vi: 'Đặc sản Trảng Bàng Hoàng Ty', en: 'Hoàng Ty Trảng Bàng specialties' }, distance: '1 km' },
      { name: { vi: 'Khu du lịch Bình Quới', en: 'Bình Quới tourist area' }, distance: '1–4 km' },
    ],
  },
  ST04: {
    story: {
      vi: 'Bến Hiệp Bình Chánh nằm trên đường số 10, bờ phía Thủ Đức của sông Sài Gòn. Quanh bến là những quán ăn sân vườn sát mép sông, và tuyến xe buýt số 44 nối bến với các khu dân cư lân cận.',
      en: 'Hiệp Bình Chánh pier is on street No. 10, on the Thủ Đức side of the Saigon River. Around it are garden restaurants right at the water’s edge, and bus route 44 connects the pier with nearby neighbourhoods.',
    },
    nearby: [
      { name: { vi: 'Bánh xèo tôm nhảy Thanh Diệu', en: 'Thanh Diệu sizzling pancakes' }, distance: 'opposite' },
      { name: { vi: 'Nhà hàng Bên Sông', en: 'Bên Sông restaurant' }, distance: '300 m' },
      { name: { vi: 'Giga Mall', en: 'Giga Mall' }, distance: '1.8 km' },
    ],
  },
  ST05: {
    story: {
      vi: 'Bến Linh Đông là điểm cuối của tuyến, ở cuối đường số 36, phường Linh Đông, TP. Thủ Đức. Cạnh bến là cửa hàng nước mắm Liên Thành, thương hiệu được giới thiệu là có hơn 100 năm tuổi.',
      en: 'Linh Đông pier is the end of the line, at the end of street No. 36 in Linh Đông ward, Thủ Đức City. Next to it stands the Liên Thành fish sauce shop, a brand described as more than a century old.',
    },
    nearby: [
      { name: { vi: 'Cửa hàng nước mắm Liên Thành', en: 'Liên Thành fish sauce shop' }, distance: '40 m' },
      { name: { vi: 'Vincom Thủ Đức', en: 'Vincom Thủ Đức' }, distance: '3.3 km' },
      { name: { vi: 'Chùa Một Cột Thủ Đức', en: 'Thủ Đức One-Pillar Pagoda' }, distance: '3.5 km' },
    ],
  },
}
