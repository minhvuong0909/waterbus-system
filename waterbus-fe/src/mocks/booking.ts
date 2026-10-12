import type {
  AssistanceOption,
  PassengerCategory,
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
export const DEFAULT_SEAT_ID = 'S01'

export const DEFAULT_PASSENGER: PassengerForm = {
  category: 'adult',
  fullName: 'Nguyen Van An',
  dateOfBirth: '14 / 08 / 1994',
  phoneCode: '+84',
  phone: '908 123 456',
  email: 'an.nguyen@smartwaterbus.vn',
  useAsContact: true,
  assistance: [],
}

export const PASSENGER_CATEGORIES: { id: PassengerCategory; label: string; range: string }[] = [
  { id: 'adult', label: 'Adult', range: '(18–59)' },
  { id: 'senior', label: 'Senior', range: '(60+)' },
  { id: 'child', label: 'Child', range: '(<12)' },
]

export const CATEGORY_LABEL: Record<PassengerCategory, string> = {
  adult: 'Adult (18–59)',
  senior: 'Senior (60+)',
  child: 'Child (<12)',
}

export const ASSISTANCE_OPTIONS: AssistanceOption[] = [
  {
    id: 'mobility',
    icon: 'accessible',
    title: 'Mobility assistance',
    description: 'Ramp and boarding guidance',
  },
  {
    id: 'child',
    icon: 'child_friendly',
    title: 'Travelling with a child',
    description: 'Stroller boarding support',
  },
  {
    id: 'other',
    icon: 'help_outline',
    title: 'Other assistance',
    description: 'General pier assistance',
  },
]

// The one voucher the mock accepts. Any other code is reported as invalid but never blocks checkout.
export const VALID_VOUCHER = { code: 'WATERBUS10', percentOff: 10 }
