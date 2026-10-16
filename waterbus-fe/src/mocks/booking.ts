import type {
  PassengerForm,
  Seat,
  SeatCategory,
  SeatMap,
} from '../features/booking/types'

const COLUMNS = ['A', 'B', 'C', 'D'] as const

// Same layout the backend seeds for boat SWB-01 (ApplicationDbContextInitializer):
// 12 front cabin, 36 standard and 12 outdoor seats, four per row, with these fare multipliers.
const CABINS: { prefix: string; category: SeatCategory; count: number; priceMultiplier: number }[] =
  [
    { prefix: 'F', category: 'front', count: 12, priceMultiplier: 1.2 },
    { prefix: 'S', category: 'standard', count: 36, priceMultiplier: 1.0 },
    { prefix: 'O', category: 'outdoor', count: 12, priceMultiplier: 1.1 },
  ]

const SOLD = new Set(['F02', 'F07', 'S03', 'S14', 'S15', 'S22', 'S30', 'O05'])
const HELD = new Set(['S08', 'O10'])

const seats: Seat[] = CABINS.flatMap(({ prefix, category, count, priceMultiplier }) =>
  Array.from({ length: count }, (_, index) => {
    const code = `${prefix}${String(index + 1).padStart(2, '0')}`
    const column = COLUMNS[index % 4]
    return {
      id: code,
      code,
      category,
      row: Math.floor(index / 4) + 1,
      column,
      priceMultiplier,
      status: SOLD.has(code) ? ('sold' as const) : HELD.has(code) ? ('held' as const) : ('available' as const),
      window: column === 'A' || column === 'D',
    }
  }),
)

export const SEAT_MAP: SeatMap = {
  vesselName: 'Saigon Waterbus 01',
  seats,
}

/** A standard window seat, so the default booking never carries a surcharge. */
export const DEFAULT_SEAT_IDS = ['S01']

export const DEFAULT_PASSENGER: PassengerForm = {
  fullName: 'Nguyen Van An',
  phoneCode: '+84',
  phone: '908 123 456',
  email: 'an.nguyen@smartwaterbus.vn',
  useAsContact: true,
}
