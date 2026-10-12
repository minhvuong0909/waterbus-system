import baSonImg from '../assets/images/explore/ba-son.jpg'
import landmarkImg from '../assets/images/explore/landmark-park.jpg'
import vesselImg from '../assets/images/vessel.jpg'
import waterwayImg from '../assets/images/waterway.jpg'
import ecoImg from '../assets/images/home/story-eco.jpg'
import heritageImg from '../assets/images/home/story-heritage.jpg'
import sunsetImg from '../assets/images/home/story-sunset.jpg'
import type { ExploreData } from '../features/explore/types'

export const EXPLORE_DATA: ExploreData = {
  languages: ['EN', 'VI', 'FR', 'JP', 'KR'],
  categories: ['Skyline', 'Heritage', 'Culture', 'Riverside'],
  journeys: [
    {
      id: 'sunset-skyline',
      image: sunsetImg,
      eyebrow: 'Scenic Route • Golden Hour',
      tags: ['Scenic Route', 'Skyline Views'],
      title: 'Saigon Sunset & Skyline Voyage',
      description:
        'Glide past illuminated bridges and towering downtown landmarks as golden dusk illuminates the central river corridor.',
      route: 'Bach Dang Pier ➔ Thu Thiem',
    },
    {
      id: 'colonial-wharves',
      image: heritageImg,
      eyebrow: 'Heritage Route • Culture',
      tags: ['Heritage Stories', 'Architecture'],
      title: 'Colonial Wharves & Living Heritage',
      description:
        'Discover the storied waterfront, historic customs houses, and lush riverside promenades.',
      route: 'Bach Dang Pier ➔ Binh An Pier',
    },
    {
      id: 'twilight-bridges',
      image: waterwayImg,
      eyebrow: 'Twilight Route • Skyline',
      tags: ['Skyline Views', 'Audio Stories'],
      title: 'Twilight Reflections & Bridge Lights',
      description:
        'Experience the city’s architectural skyline from the panoramic catamaran observation deck under an illuminated evening sky.',
      route: 'Central Riverfront ➔ Thu Thiem Curve',
    },
  ],
  modes: [
    {
      id: 'transit',
      eyebrow: 'Commuter Line',
      title: 'Regular Transit',
      subtitle: 'Point-to-Point River Mobility',
      icon: 'directions_boat',
      chip: 'Commuter Line',
      features: [
        'Point-to-point river travel between central piers',
        'Live vessel tracking and arrival updates',
        'Standard passenger journey with seamless ticketing',
      ],
      cta: 'View Commuter Timetable',
    },
    {
      id: 'sightseeing',
      eyebrow: 'Curated Tourism',
      title: 'Sightseeing Experience',
      subtitle: 'Curated River Discovery & Storytelling',
      icon: 'photo_camera',
      chip: 'Curated Tourism',
      features: [
        'Curated river routes designed for leisure and exploration',
        'Waterfront landmarks and points of interest along the route',
        'River stories and audio experience during your journey',
        'Panoramic outdoor viewing deck access',
      ],
      cta: 'Explore Sightseeing Journeys',
    },
  ],
  landmarks: [
    {
      id: 'ba-son',
      image: baSonImg,
      category: 'Heritage',
      routeTag: 'Central Corridor',
      title: 'Ba Son Waterfront',
      description:
        'Modernized waterfront promenade blending maritime heritage with contemporary public space.',
    },
    {
      id: 'thu-thiem',
      image: vesselImg,
      category: 'Skyline',
      routeTag: 'Line 1 Crossing',
      title: 'Thu Thiem Promenade',
      description:
        'Vibrant civic waterfront offering unobstructed panoramic views of the city skyline.',
    },
    {
      id: 'customs-house',
      image: ecoImg,
      category: 'Culture',
      routeTag: 'Heritage Route',
      title: 'Old Customs House',
      description:
        'Historic riverfront landmark reflecting the storied port heritage along the water.',
    },
    {
      id: 'landmark-park',
      image: landmarkImg,
      category: 'Riverside',
      routeTag: 'North Corridor',
      title: 'Landmark River Park',
      description:
        'Expansive contemporary waterfront parkland connecting urban transit with scenic walking paths.',
    },
  ],
  episode: {
    id: 'ep-03-explore',
    eyebrow: 'Ep 03 • Ba Son Waterfront',
    title: 'The Awakening Riverfront',
    subtitle: 'Heritage, waterfront engineering, and dawn of modern Saigon',
    durationSec: 200,
    startAtSec: 74,
    thumbnail: baSonImg,
  },
  pins: [
    {
      id: 'bach-dang',
      icon: 'directions_boat',
      title: 'Bach Dang Pier',
      subtitle: 'Departure Hub',
      x: 9,
      y: 76,
      emphasis: 'hub',
    },
    {
      id: 'customs',
      icon: 'account_balance',
      title: 'Old Customs House',
      x: 22,
      y: 56,
      emphasis: 'poi',
    },
    {
      id: 'thu-thiem',
      icon: 'anchor',
      title: 'Thu Thiem Pier',
      subtitle: 'Skyline Promenade',
      x: 34,
      y: 50,
      emphasis: 'stop',
    },
    {
      id: 'ba-son',
      icon: 'architecture',
      title: 'Ba Son Waterfront',
      x: 50,
      y: 39,
      emphasis: 'poi',
    },
    {
      id: 'binh-an',
      icon: 'directions_boat',
      title: 'Binh An Pier',
      subtitle: 'Heritage Basin',
      x: 82,
      y: 14,
      emphasis: 'hub',
    },
  ],
}
