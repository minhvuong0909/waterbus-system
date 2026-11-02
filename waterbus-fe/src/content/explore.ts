import baSonImg from '../assets/images/explore/ba-son.jpg'
import landmarkImg from '../assets/images/explore/landmark-park.jpg'
import ecoImg from '../assets/images/home/story-eco.jpg'
import heritageImg from '../assets/images/home/story-heritage.jpg'
import type { ExploreData } from '../features/explore/types'

/**
 * Các địa danh (POI) có thuyết minh trên chuyến ngắm cảnh, khớp đúng 5 POI backend seed
 * (`ApplicationDbContextInitializer.SeedPointsOfInterestAsync`). Backend chưa có API POI công khai
 * (`GET /audioguides/pois` chỉ cho Admin), nên tên và mô tả được chép lại ở đây; khi có API thì đọc
 * từ API và bỏ danh sách này. Văn bản nguồn tiếng Anh, bản tiếng Việt ở i18n/dictionaries/vi.ts.
 */
export const EXPLORE_DATA: ExploreData = {
  categories: ['Heritage', 'Skyline', 'Riverside'],
  landmarks: [
    {
      id: 'thu-ngu-nha-rong',
      image: heritageImg,
      category: 'Heritage',
      title: 'Thủ Ngữ Flag Tower & Nhà Rồng Wharf',
      description:
        'A long-standing historic site where the Bến Nghé canal meets the Saigon River, from which Hồ Chí Minh left in 1911 to look for a way to save the country.',
    },
    {
      id: 'ba-son-bridge',
      image: baSonImg,
      category: 'Skyline',
      title: 'Ba Son Bridge (Thủ Thiêm 2 Bridge)',
      description:
        'A modern, sail-shaped cable-stayed bridge linking central District 1 with the new Thủ Thiêm urban area.',
    },
    {
      id: 'landmark-81',
      image: landmarkImg,
      category: 'Skyline',
      title: 'Landmark 81 & Vinhomes Central Park',
      description:
        'The tallest building in Vietnam (461.3 m), a symbol of the prosperity and energy of Ho Chi Minh City.',
    },
    {
      id: 'thanh-da-binh-quoi',
      image: ecoImg,
      category: 'Riverside',
      title: 'Thanh Đa Peninsula & Bình Quới',
      description: 'A peaceful green area wrapped by a winding branch of the Saigon River.',
    },
    {
      id: 'binh-loi-bridge',
      // Chưa có ảnh đúng cầu Bình Lợi: thẻ hiện ô trung tính thay vì ảnh nơi khác.
      category: 'Heritage',
      title: 'Historic Bình Lợi Bridge',
      description:
        'The first railway bridge across the Saigon River, built in 1902, a witness to more than a century of the city’s history.',
    },
  ],
}
