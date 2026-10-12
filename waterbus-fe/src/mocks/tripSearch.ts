import type {
  AmenityDetail,
  BoardingStep,
  NetworkRoute,
  PierOption,
  SearchQuery,
  StationDto,
  Trip,
} from '../features/trips/types'
import { toPierOption } from '../features/trips/utils'
import { toIsoDate } from '../utils/time'

// Mirrors the stations seeded by the backend (ApplicationDbContextInitializer), in river order.
export const STATION_SEEDS: StationDto[] = [
  {
    id: 'st01',
    code: 'ST01',
    name: 'Bến Bạch Đằng',
    address: 'Số 10B Tôn Đức Thắng, Phường Bến Nghé, Quận 1',
    latitude: 10.7725,
    longitude: 106.7067,
    orderIndex: 1,
    isActive: true,
  },
  {
    id: 'st02',
    code: 'ST02',
    name: 'Bến Bình An',
    address: 'Đường số 21, Phường Bình An, TP. Thủ Đức',
    latitude: 10.785,
    longitude: 106.721,
    orderIndex: 2,
    isActive: true,
  },
  {
    id: 'st03',
    code: 'ST03',
    name: 'Bến Thanh Đa',
    address: 'Phường 27, Quận Bình Thạnh',
    latitude: 10.8256,
    longitude: 106.7289,
    orderIndex: 3,
    isActive: true,
  },
  {
    id: 'st04',
    code: 'ST04',
    name: 'Bến Hiệp Bình Chánh',
    address: 'Đường số 10, Hiệp Bình Chánh, TP. Thủ Đức',
    latitude: 10.8412,
    longitude: 106.7321,
    orderIndex: 4,
    isActive: true,
  },
  {
    id: 'st05',
    code: 'ST05',
    name: 'Bến Linh Đông',
    address: 'Đường Kha Vạn Cân, Phường Linh Đông, TP. Thủ Đức',
    latitude: 10.8521,
    longitude: 106.7554,
    orderIndex: 5,
    isActive: true,
  },
]

export const PIER_OPTIONS: PierOption[] = STATION_SEEDS.map(toPierOption)

// Pier ids are filled in from the stations at load time (see `getPierOptions`).
export const DEFAULT_SEARCH_QUERY: SearchQuery = {
  tripType: 'one-way',
  originId: '',
  destinationId: '',
  dateId: toIsoDate(new Date()),
  passengers: '1 Adult • Standard',
  passengerNote: 'Main Panoramic Saloon',
}

/** A suggested route between two stations, by station code. */
export interface NetworkRouteSeed extends Omit<NetworkRoute, 'originId' | 'destinationId'> {
  originCode: string
  destinationCode: string
}

// The backend has no route list endpoint yet. This is the one route it seeds (RT01,
// Bạch Đằng - Linh Đông, 45 min, 10.8 km); the fare is the default Trip.BasePrice.
export const NETWORK_ROUTE_SEEDS: NetworkRouteSeed[] = [
  {
    id: 'rt01',
    originCode: 'ST01',
    destinationCode: 'ST05',
    badge: 'Main Corridor',
    summary: '45 min • 10.8 km river route between Bach Dang and Linh Dong',
    fromFareVnd: 15000,
  },
]

export const TRIPS: Trip[] = [
  {
    id: 'wb-01',
    kind: 'transit',
    vesselCode: 'WB-01',
    vesselName: 'Hydrofoil Catamaran',
    lineLabel: 'Line 1 Express',
    highlight: { icon: 'bolt', label: 'RECOMMENDED • FASTEST' },
    variant: 'featured',
    departTime: '08:30',
    arriveTime: '08:42',
    originGate: 'Gate 2',
    destinationGate: 'Pontoon A',
    durationMins: 12,
    crossingKind: 'Direct',
    direct: true,
    crossingCaption: 'Express crossing',
    seatsCount: 28,
    band: 'morning',
    amenities: [
      { icon: 'ac_unit', label: 'Air-Conditioned' },
      { icon: 'visibility', label: 'Panoramic Windows' },
      { icon: 'accessible', label: 'Step-Free Gangway' },
    ],
    fareVnd: 15000,
    fareLabel: 'Standard Fare',
  },
  {
    id: 'wb-03',
    kind: 'transit',
    vesselCode: 'WB-03',
    vesselName: 'Catamaran Cruiser',
    lineLabel: 'Express Line 1',
    tagline: 'Commuter Priority',
    variant: 'default',
    departTime: '09:00',
    arriveTime: '09:12',
    originGate: 'Gate 1',
    destinationGate: 'Pontoon A',
    durationMins: 12,
    crossingKind: 'Direct',
    direct: true,
    crossingCaption: 'Regular crossing',
    seatsCount: 54,
    band: 'morning',
    amenities: [
      { icon: 'ac_unit', label: 'Air-Conditioned' },
      { icon: 'deck', label: 'Open Observation Deck' },
      { icon: 'wifi', label: 'High-Speed Wi-Fi' },
    ],
    fareVnd: 15000,
    fareLabel: 'Standard Fare',
  },
  {
    id: 'wb-02',
    kind: 'transit',
    vesselCode: 'WB-02',
    vesselName: 'River Transit Vessel',
    lineLabel: 'Regular River Line 1',
    tagline: 'Scenic River Pace',
    variant: 'default',
    departTime: '09:30',
    arriveTime: '09:48',
    originGate: 'Gate 2',
    destinationGate: 'Pontoon B',
    durationMins: 18,
    crossingKind: 'Leisure Speed',
    direct: false,
    crossingCaption: 'Scenic glide',
    seatsCount: 42,
    band: 'morning',
    amenities: [
      { icon: 'air', label: 'Open Deck Breeze' },
      { icon: 'visibility', label: 'Panoramic Windows' },
      { icon: 'local_cafe', label: 'Fresh Drinks Kiosk' },
    ],
    fareVnd: 15000,
    fareLabel: 'Standard Fare',
  },
  {
    id: 'wb-05',
    kind: 'transit',
    vesselCode: 'WB-05',
    vesselName: 'Aero Catamaran',
    lineLabel: 'Express Line 1',
    tagline: 'Quiet Work Zone',
    variant: 'default',
    departTime: '10:15',
    arriveTime: '10:27',
    originGate: 'Gate 1',
    destinationGate: 'Pontoon A',
    durationMins: 12,
    crossingKind: 'Direct',
    direct: true,
    crossingCaption: 'Direct crossing',
    seatsCount: 36,
    band: 'morning',
    amenities: [
      { icon: 'ac_unit', label: 'Air-Conditioned' },
      { icon: 'visibility', label: 'Panoramic Windows' },
      { icon: 'accessible', label: 'Step-Free Gangway' },
    ],
    fareVnd: 15000,
    fareLabel: 'Standard Fare',
  },
  {
    id: 'wb-07',
    kind: 'sightseeing',
    vesselCode: 'WB-07',
    vesselName: 'Sunset Heritage Cruise',
    lineLabel: 'Heritage Route',
    highlight: { icon: 'wb_twilight', label: 'SUNSET SPECIAL • GOLDEN HOUR' },
    variant: 'sunset',
    departTime: '17:30',
    arriveTime: '18:15',
    originGate: 'Heritage Berth',
    destinationGate: 'Pontoon A',
    durationMins: 45,
    crossingKind: 'Landmark Loop',
    direct: false,
    crossingCaption: 'Includes Ba Son Bridge view',
    seatsCount: 12,
    seatsLow: true,
    band: 'sunset',
    amenities: [
      { icon: 'local_bar', label: 'Complimentary Herbal Tea' },
      { icon: 'headphones', label: 'Historical Audio Tour' },
      { icon: 'deck', label: 'Sunset Skyline Deck' },
    ],
    fareVnd: 45000,
    fareLabel: 'Experience Fare',
  },
]

export const ONBOARD_AMENITIES: AmenityDetail[] = [
  {
    icon: 'ac_unit',
    title: 'Air-Conditioned Cabin',
    description: 'Comfortable indoor temperature with river views.',
  },
  {
    icon: 'chair',
    title: 'Comfortable Seating',
    description: 'Ergonomic transit seating with scenic river views.',
  },
  {
    icon: 'luggage',
    title: 'Luggage Space',
    description: 'Dedicated space for carry-on luggage and personal items.',
  },
  {
    icon: 'accessible_forward',
    title: 'Step-Free Access',
    description: 'Ramps and accessibility support for all passengers.',
  },
]

export const BOARDING_STEPS: BoardingStep[] = [
  { title: 'Arrive at Pier', description: 'Arrive at {origin} before departure time.' },
  {
    title: 'Present QR Ticket',
    description: 'Display your Smart Waterbus QR e-ticket on your mobile screen.',
  },
  {
    title: 'Verify & Board',
    description: 'Staff verifies your ticket at the pier gate prior to vessel boarding.',
  },
]
