import type { HomeData } from '../features/home/types'
import sunsetImg from '../assets/images/home/story-sunset.jpg'
import heritageImg from '../assets/images/home/story-heritage.jpg'
import ecoImg from '../assets/images/home/story-eco.jpg'

export const HOME_DATA: HomeData = {
  heroCues: [
    {
      icon: 'radar',
      title: 'Real-time tracking',
      subtitle: 'Know where you are',
      iconClass: 'text-sky-aqua',
    },
    {
      icon: 'wb_twilight',
      title: 'Scenic journeys',
      subtitle: 'See a different side',
      iconClass: 'text-coral-glow',
    },
    {
      icon: 'confirmation_number',
      title: 'Easy booking',
      subtitle: 'Fast & secure',
      iconClass: 'text-sky-aqua',
    },
    {
      icon: 'energy_savings_leaf',
      title: 'Sustainable travel',
      subtitle: 'Greener future',
      iconClass: 'text-secondary-fixed',
    },
  ],
  routeFilters: [
    { id: 'all', label: 'All Routes' },
    { id: 'line1', label: 'Line 1 (Express)' },
    { id: 'line2', label: 'Line 2 (Heritage)' },
    { id: 'sunset', label: 'Sunset Twilight' },
  ],
  stories: [
    {
      id: 'sunset',
      image: sunsetImg,
      badge: 'Golden Hour Special',
      badgeIcon: 'wb_sunny',
      badgeIconClass: 'text-coral-glow',
      fromPriceVnd: 45000,
      durationMins: 50,
      location: 'Bach Dang Pier',
      title: 'Twilight Sunset at Thu Thiem',
      description:
        'Watch the sunset cast copper light across Saigon’s highest skyscrapers, accompanied by bilingual ambient audio commentary.',
      trailingIcon: 'headphones',
      trailingIconClass: 'text-coral-glow',
    },
    {
      id: 'heritage',
      image: heritageImg,
      badge: 'Heritage & History',
      badgeIcon: 'museum',
      badgeIconClass: 'text-sky-aqua',
      fromPriceVnd: 35000,
      durationMins: 40,
      location: 'Cho Lon Wharf',
      title: 'Historic Waterfront & Colonial Wharfs',
      description:
        'Trace the 300-year maritime lineage of Gia Dinh, sailing past historic customs houses, ancient timber storehouses, and boat communities.',
      trailingIcon: 'auto_stories',
      trailingIconClass: 'text-teal-flow',
    },
    {
      id: 'eco',
      image: ecoImg,
      badge: 'Nature & Escapes',
      badgeIcon: 'forest',
      badgeIconClass: 'text-teal-flow',
      fromPriceVnd: 40000,
      durationMins: 65,
      location: 'Binh Quoi Pier',
      title: 'Eco-Corridor to Thanh Da Peninsula',
      description:
        'Escape into serene natural waterways framed by water palm groves and tranquil fishing hamlets tucked right inside city limits.',
      trailingIcon: 'nature_people',
      trailingIconClass: 'text-secondary',
    },
  ],
  steps: [
    {
      number: '01',
      title: 'Plan Journey',
      description: 'Pick your departure station, destination pier, and preferred schedule.',
      numberClass: 'bg-surface text-deep-river border border-outline-variant/30',
    },
    {
      number: '02',
      title: 'Choose Seat',
      description: 'Select panoramic window saloon seats or breezy open-deck viewing benches.',
      numberClass: 'bg-surface text-teal-flow border border-outline-variant/30',
    },
    {
      number: '03',
      title: 'Pay Securely',
      description: 'Instant digital payment via MoMo, VNPay, or card with zero surcharges.',
      numberClass: 'bg-surface text-sky-aqua border border-outline-variant/30',
    },
    {
      number: '04',
      title: 'Board with QR',
      description: 'Scan your e-ticket barcode at turnstiles and step aboard directly.',
      numberClass: 'bg-teal-flow text-on-primary',
    },
  ],
}
