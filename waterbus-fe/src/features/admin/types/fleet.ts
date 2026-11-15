import type { AdminBoat } from '../../operations/types'
import type { StationDto } from '../../trips/types'

export type { AdminBoat }

/** Bến như backend trả về (`GET /api/v1/stations`). */
export type AdminStation = StationDto

export type NewStation = Pick<
  AdminStation,
  'code' | 'name' | 'address' | 'latitude' | 'longitude' | 'orderIndex'
>

export type NewBoat = Pick<AdminBoat, 'code' | 'name' | 'registrationNumber' | 'manufacturer' | 'yearBuilt' | 'totalSeats'>
