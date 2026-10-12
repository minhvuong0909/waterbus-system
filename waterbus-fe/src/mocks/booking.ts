import type {
  AssistanceOption,
  PassengerCategory,
  PassengerForm,
  Seat,
  SeatMap,
  SeatTier,
} from '../features/booking/types'

const OCCUPIED = new Set(['B1', 'D1', 'C2', 'D4', 'A5', 'C6'])
const COLUMNS = ['A', 'B', 'C', 'D'] as const
const ROWS = [1, 2, 3, 4, 5, 6]
/** The aft rows form the VIP lounge, next to the disembarkation gangway. */
const VIP_ROWS = new Set([5, 6])

const seats: Seat[] = ROWS.flatMap((row) =>
  COLUMNS.map((column) => {
    const id = `${column}${row}`
    return {
      id,
      row,
      column,
      status: OCCUPIED.has(id) ? ('occupied' as const) : ('available' as const),
      tier: (VIP_ROWS.has(row) ? 'vip' : 'standard') as SeatTier,
      window: column === 'A' || column === 'D',
    }
  }),
)

export const SEAT_MAP: SeatMap = {
  deckName: 'Main Panoramic Deck',
  vesselModel: 'River Glider Catamaran • High-speed twin hull',
  rows: ROWS,
  seats,
}

/** A standard window seat, so the default booking never carries a surcharge. */
export const DEFAULT_SEAT_ID = 'A2'

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
