import type { ManageBooking, Ticket, TicketWallet } from '../features/tickets/types'

const NEXT: Ticket = {
  id: 'tkt-wb01-a2',
  tripCode: 'WB-01',
  lineName: 'Smart Waterbus • Line 1 Catamaran',
  routeTag: 'Central Route',
  bookingRef: 'WB-2025-0842-A2',
  ticketCode: 'TKT-WB01-0842-A2',
  status: 'Confirmed',
  departTime: '08:30',
  departPier: 'Bach Dang Pier',
  departShort: 'Bach Dang',
  departDistrict: 'District 1 Central',
  arriveTime: '08:42',
  arrivePier: 'Thu Thiem Pier',
  arriveShort: 'Thu Thiem',
  arriveDistrict: 'District 2 Waterfront',
  durationMins: 12,
  dateLabel: 'Dec 15, 2025',
  dateShort: 'Dec 15',
  passenger: 'Nguyen Van An',
  seat: 'Seat A2',
  seatNote: 'Window View • Main Deck',
  windowSeat: true,
  completed: false,
  fareVnd: 15000,
  vesselNote: 'River Catamaran WB-01',
  gate: 'Gate 02',
}

const LATER: Ticket[] = [
  {
    ...NEXT,
    id: 'tkt-wb04-b4',
    tripCode: 'WB-04',
    routeTag: 'Return Route',
    bookingRef: 'WB-2025-0850-B4',
    ticketCode: 'TKT-WB04-0850-B4',
    departTime: '17:45',
    departPier: 'Thu Thiem Pier',
    departShort: 'Thu Thiem',
    departDistrict: 'District 2',
    arriveTime: '17:57',
    arrivePier: 'Bach Dang Pier',
    arriveShort: 'Bach Dang',
    arriveDistrict: 'District 1',
    seat: 'Seat B4',
    seatNote: 'Aisle • Main Deck',
    windowSeat: false,
    vesselNote: 'River Catamaran WB-04',
  },
  {
    ...NEXT,
    id: 'tkt-wb08-c1',
    tripCode: 'WB-08',
    routeTag: 'North Corridor',
    bookingRef: 'WB-2025-0861-C1',
    ticketCode: 'TKT-WB08-0861-C1',
    dateLabel: 'Dec 18, 2025',
    dateShort: 'Dec 18',
    departTime: '09:15',
    departDistrict: 'District 1',
    arriveTime: '09:35',
    arrivePier: 'Binh An Pier',
    arriveShort: 'Binh An',
    arriveDistrict: 'Thu Duc City',
    durationMins: 20,
    seat: 'Seat C1',
    seatNote: 'Aisle • Main Deck',
    windowSeat: false,
    vesselNote: 'River Catamaran WB-08',
  },
]

const PAST: Ticket[] = [
  {
    ...NEXT,
    id: 'tkt-past-a1',
    status: 'Completed',
    completed: true,
    bookingRef: 'WB-2025-0790-A1',
    ticketCode: 'TKT-WB01-0790-A1',
    dateLabel: 'Dec 10, 2025',
    dateShort: 'Dec 10',
    seat: 'Seat A1',
    seatNote: 'Window View • Main Deck',
  },
  {
    ...NEXT,
    id: 'tkt-past-b2',
    status: 'Completed',
    completed: true,
    windowSeat: false,
    tripCode: 'WB-03',
    bookingRef: 'WB-2025-0791-B2',
    ticketCode: 'TKT-WB03-0791-B2',
    dateLabel: 'Dec 10, 2025',
    dateShort: 'Dec 10',
    departTime: '18:00',
    departPier: 'Thu Thiem Pier',
    departShort: 'Thu Thiem',
    departDistrict: 'District 2',
    arriveTime: '18:12',
    arrivePier: 'Bach Dang Pier',
    arriveShort: 'Bach Dang',
    arriveDistrict: 'District 1',
    seat: 'Seat B2',
    seatNote: 'Aisle • Main Deck',
  },
]

export const TICKET_WALLET: TicketWallet = { next: NEXT, later: LATER, past: PAST }

export const ALL_TICKETS: Ticket[] = [NEXT, ...LATER, ...PAST]

export const MANAGE_BOOKING: ManageBooking = {
  bookingCode: 'SWB-8942-01',
  ticket: NEXT,
  voucherCredit: [
    { code: 'WELCOME-RIVER', description: 'Welcome credit for first-time riders', valueVnd: 10000 },
  ],
}

// Canned assistant replies. Keys match the quick-action chips.
export const ASSISTANT_REPLIES: Record<string, string> = {
  'Change Trip':
    'I can help you review available options. Try "View Available Trips" to pick another departure today.',
  'Refund Help':
    'Refunds are available up to 30 minutes before departure. Use "Request Refund" and confirm to submit.',
  'Explain Voucher':
    'Vouchers and credits are applied at checkout. You currently have one welcome credit on this booking.',
  'Trip Status':
    'Vessel WB-01 is operating on schedule. Boarding opens 10 minutes before departure.',
}

export const ASSISTANT_FALLBACK =
  'Thanks for your question. A Pier Concierge can confirm the details; actions always need your confirmation.'
