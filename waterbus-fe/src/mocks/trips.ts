import type { CorridorMap, Departure, Pier, Vessel } from '../features/trips/types'

export const PIERS: Pier[] = [
  {
    id: 'bach-dang',
    name: 'Bach Dang Pier',
    subtitle: 'Central D1 Terminal',
    x: 9,
    y: 10,
    kind: 'hub',
  },
  {
    id: 'thu-thiem',
    name: 'Thu Thiem Pier',
    subtitle: 'District 2 Waterfront',
    x: 32,
    y: 35,
    kind: 'stop',
  },
  { id: 'van-thanh', name: 'Van Thanh', subtitle: 'Park Interchange', x: 62, y: 57, kind: 'stop' },
  { id: 'binh-an', name: 'Binh An Pier', subtitle: 'Binh Thanh', x: 74, y: 70, kind: 'stop' },
  {
    id: 'linh-dong',
    name: 'Linh Dong Pier',
    subtitle: 'Thu Duc Terminus',
    x: 78,
    y: 84,
    kind: 'terminus',
  },
]

export const NEXT_DEPARTURES: Departure[] = [
  {
    id: 'dep-0800',
    tripId: 'wb-01',
    time: '08:00',
    status: 'On time',
    statusKind: 'ontime',
    kind: 'transit',
    from: 'Bach Dang',
    to: 'Thu Thiem',
    vesselCode: 'WB-01',
    details: ['12 min voyage', 'Direct'],
    line: 'direct',
    priceVnd: 15000,
  },
  {
    id: 'dep-0830',
    tripId: 'wb-02',
    time: '08:30',
    status: 'Boarding',
    statusKind: 'boarding',
    kind: 'transit',
    from: 'Bach Dang',
    to: 'Binh An',
    vesselCode: 'WB-02',
    details: ['24 min express', 'Line 1'],
    line: 'line1',
    priceVnd: 25000,
  },
  {
    id: 'dep-0915',
    tripId: 'wb-03',
    time: '09:15',
    status: 'Scheduled',
    statusKind: 'scheduled',
    kind: 'transit',
    from: 'Bach Dang',
    to: 'Linh Dong',
    vesselCode: 'WB-03',
    details: ['42 min full run', '5 Piers'],
    line: 'line1',
    priceVnd: 35000,
  },
  {
    id: 'dep-1730',
    tripId: 'wb-07',
    time: '17:30',
    status: 'Twilight',
    statusKind: 'twilight',
    kind: 'sightseeing',
    from: 'Sunset Heritage Cruise',
    details: ['60 min scenic', 'Audio Guide Included'],
    line: 'sunset',
    priceVnd: 45000,
  },
]

export const CORRIDOR_VESSEL: Vessel = {
  code: 'WB-03',
  status: 'On time',
  speedKmh: 42,
  nextPier: 'Thu Thiem',
  x: 50.7,
  y: 44.5,
}

export const CORRIDOR_MAP: CorridorMap = { piers: PIERS, vessel: CORRIDOR_VESSEL }
