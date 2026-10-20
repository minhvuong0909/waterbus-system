import baSonImg from '../assets/images/explore/ba-son.jpg'
import landmarkImg from '../assets/images/explore/landmark-park.jpg'
import waterwayImg from '../assets/images/waterway.jpg'
import heritageImg from '../assets/images/home/story-heritage.jpg'
import sunsetImg from '../assets/images/home/story-sunset.jpg'
import type { ExploreData } from '../features/explore/types'

export const EXPLORE_DATA: ExploreData = {
  categories: ['Skyline', 'Heritage', 'Culture', 'Riverside'],
  journeys: [
    {
      id: 'sunset-skyline',
      image: sunsetImg,
      tags: ['Scenic Route', 'Skyline Views'],
      title: 'Saigon Sunset & Skyline Voyage',
      description:
        'Glide past illuminated bridges and towering downtown landmarks as golden dusk illuminates the central river corridor.',
      route: 'Bach Dang Pier ➔ Linh Dong Pier',
    },
    {
      id: 'colonial-wharves',
      image: heritageImg,
      tags: ['Heritage Stories', 'Architecture'],
      title: 'Colonial Wharves & Living Heritage',
      description:
        'Discover the storied waterfront, historic customs houses, and lush riverside promenades.',
      route: 'Bach Dang Pier ➔ Linh Dong Pier',
    },
    {
      id: 'twilight-bridges',
      image: waterwayImg,
      tags: ['Skyline Views', 'Audio Stories'],
      title: 'Twilight Reflections & Bridge Lights',
      description:
        'Experience the city’s architectural skyline from the panoramic catamaran observation deck under an illuminated evening sky.',
      route: 'Bach Dang Pier ➔ Linh Dong Pier',
    },
  ],
  landmarks: [
    {
      id: 'ba-son',
      image: baSonImg,
      category: 'Heritage',
      title: 'Ba Son Waterfront',
      description:
        'Modernized waterfront promenade blending maritime heritage with contemporary public space.',
    },
    {
      id: 'thu-thiem',
      // No photo of Thu Thiem yet: the card shows a neutral tile instead of an unrelated picture.
      category: 'Skyline',
      title: 'Thu Thiem Promenade',
      description:
        'Vibrant civic waterfront offering unobstructed panoramic views of the city skyline.',
    },
    {
      id: 'customs-house',
      // No photo of the customs house yet (the previous one showed a palm canal).
      category: 'Culture',
      title: 'Old Customs House',
      description:
        'Historic riverfront landmark reflecting the storied port heritage along the water.',
    },
    {
      id: 'landmark-park',
      image: landmarkImg,
      category: 'Riverside',
      title: 'Landmark River Park',
      description:
        'Expansive contemporary waterfront parkland connecting urban transit with scenic walking paths.',
    },
  ],
}
