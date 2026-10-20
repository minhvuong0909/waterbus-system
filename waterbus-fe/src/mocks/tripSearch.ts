import type {
  AmenityDetail,
  BoardingStep,
  NetworkRoute,
  PierOption,
  SearchQuery,
  StationDto,
  Trip,
  TripDto,
} from '../features/trips/types'
import { toPierOption, toTrip } from '../features/trips/utils'
import { toIsoDate } from '../utils/time'

// Mirrors the stations seeded by the backend (ApplicationDbContextInitializer), in river order.
export const STATION_SEEDS: StationDto[] = [
  {
    id: 'st01',
    code: 'ST01',
    name: 'Bến Bạch Đằng',
    address: 'Số 10B Tôn Đức Thắng, Phường Bến Nghé, Quận 1',
    latitude: 10.7752301,
    longitude: 106.7072821,
    orderIndex: 1,
    isActive: true,
  },
  {
    id: 'st02',
    code: 'ST02',
    name: 'Bến Bình An',
    address: 'Đường số 21, Phường Bình An, TP. Thủ Đức',
    latitude: 10.79736,
    longitude: 106.7283885,
    orderIndex: 2,
    isActive: true,
  },
  {
    id: 'st03',
    code: 'ST03',
    name: 'Bến Thanh Đa',
    address: 'Phường 27, Quận Bình Thạnh',
    latitude: 10.8196062,
    longitude: 106.7166367,
    orderIndex: 3,
    isActive: true,
  },
  {
    id: 'st04',
    code: 'ST04',
    name: 'Bến Hiệp Bình Chánh',
    address: 'Đường số 10, Hiệp Bình Chánh, TP. Thủ Đức',
    latitude: 10.8270491,
    longitude: 106.7316735,
    orderIndex: 4,
    isActive: true,
  },
  {
    id: 'st05',
    code: 'ST05',
    name: 'Bến Linh Đông',
    address: 'Đường Kha Vạn Cân, Phường Linh Đông, TP. Thủ Đức',
    latitude: 10.8349605,
    longitude: 106.7464066,
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

/** Thời gian qua sông của cả hành lang (5 bến, 4 chặng) theo RT01. */
const FULL_CORRIDOR_MINUTES = 45

/** Ngày `dateId` (yyyy-mm-dd) theo giờ máy; mặc định là hôm nay. */
function dayOf(dateId?: string): Date {
  const [y, m, d] = (dateId ?? '').split('-').map(Number)
  return y && m && d ? new Date(y, m - 1, d) : new Date()
}

/** A trip of `day` at `hour:minute` as the backend's TripDto describes it (boat SWB-01, route RT01). */
function tripSeed(
  n: number,
  day: Date,
  time: string,
  tripType: TripDto['tripType'],
  basePrice: number,
  availableSeatsCount: number,
  from: StationDto,
  to: StationDto,
): TripDto {
  const [hours, minutes] = time.split(':').map(Number)
  const departure = new Date(day)
  departure.setHours(hours, minutes, 0, 0)
  const hops = Math.abs(from.orderIndex - to.orderIndex)
  const durationMinutes = Math.round((hops * FULL_CORRIDOR_MINUTES) / 4)
  // Ngày + cặp bến ở nhóm cuối để chuyến đi và chuyến về (khứ hồi) không trùng id.
  const stamp = `${day.getFullYear()}${String(day.getMonth() + 1).padStart(2, '0')}${String(day.getDate()).padStart(2, '0')}`
  return {
    // The first 8 characters of the id are the trip reference shown in the UI, so each one differs.
    id: `7f3a000${n}-0000-4000-8000-${stamp}${from.orderIndex}${to.orderIndex}0${n}`,
    routeName: 'Bạch Đằng - Linh Đông',
    departureStationName: from.name,
    arrivalStationName: to.name,
    departureTime: departure.toISOString(),
    arrivalTime: new Date(departure.getTime() + durationMinutes * 60_000).toISOString(),
    durationMinutes,
    tripType,
    basePrice,
    availableSeatsCount,
  }
}

/**
 * Mock trips for the chosen day and piers, in the same shape as the real search results. Built on
 * each call (not once at load) so the texts follow the active language. The 15,000 base price is
 * the backend default (Trip.BasePrice); the sightseeing price follows the demo fare settings.
 *
 * Phục vụ thử giao diện: số chỗ thay đổi theo ngày (có chuyến hết chỗ, sắp hết, còn nhiều), và
 * mọi cặp bến trên hành lang đều có chuyến (đi xuôi hay ngược), kể cả bến trung gian. Chuyến
 * tham quan chỉ chạy cả hành lang, không bán đoạn lên/xuống tuỳ ý.
 */
export function buildTrips(
  query?: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>,
): Trip[] {
  const day = dayOf(query?.dateId)
  const find = (id?: string) => STATION_SEEDS.find((s) => s.id === id)
  const first = STATION_SEEDS[0]
  const last = STATION_SEEDS[STATION_SEEDS.length - 1]
  const from = find(query?.originId) ?? first
  let to = find(query?.destinationId) ?? last
  if (to.id === from.id) to = from.id === first.id ? last : first

  // Mỗi ngày một bộ số chỗ khác nhau; hôm nay giữ nguyên bộ gốc.
  const today = new Date()
  const offset = Math.round((day.getTime() - new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()) / 86_400_000)
  const seatSets = [
    [54, 42, 36, 8, 12],
    [60, 0, 24, 3, 0],
    [48, 30, 0, 14, 20],
    [60, 58, 52, 40, 6],
  ]
  const seats = seatSets[((offset % seatSets.length) + seatSets.length) % seatSets.length]
  const corridor = Math.abs(from.orderIndex - to.orderIndex) === STATION_SEEDS.length - 1

  const trips = [
    tripSeed(1, day, '07:00', 'Commuter', 15000, seats[0], from, to),
    tripSeed(2, day, '09:00', 'Commuter', 15000, seats[1], from, to),
    tripSeed(3, day, '12:00', 'Commuter', 15000, seats[2], from, to),
    tripSeed(4, day, '15:30', 'Commuter', 15000, seats[3], from, to),
  ]
  if (corridor) trips.push(tripSeed(5, day, '17:30', 'Sightseeing', 45000, seats[4], from, to))
  return trips.map(toTrip)
}

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
