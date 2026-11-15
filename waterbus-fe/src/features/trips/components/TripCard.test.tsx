import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { toTrip } from '../utils'
import type { PierOption, TripDto } from '../types'
import TripCard from './TripCard'

const pier = (id: string, name: string) => ({ id, name }) as PierOption
const A = pier('a', 'Bach Dang')
const B = pier('b', 'Thanh Da')
const C = pier('c', 'Linh Dong')

const dto = (tripType: string): TripDto => ({
  id: 'a1b2c3d4-0000-0000-0000-000000000000',
  routeName: 'R',
  departureStationName: 'Bach Dang',
  arrivalStationName: 'Thanh Da',
  departureTime: '2099-01-01T01:00:00Z',
  arrivalTime: '2099-01-01T02:00:00Z',
  durationMinutes: 30,
  tripType,
  basePrice: 15000,
  availableSeatsCount: 5,
  stops: [
    { stationId: 'a', stationName: 'Bach Dang', orderIndex: 0 },
    { stationId: 'b', stationName: 'Thanh Da', orderIndex: 1 },
    { stationId: 'c', stationName: 'Linh Dong', orderIndex: 2 },
  ],
})

const renderCard = (type: string, origin: PierOption, destination: PierOption) =>
  render(
    <MemoryRouter>
      <TripCard trip={toTrip(dto(type))} origin={origin} destination={destination} onSelect={() => undefined} />
    </MemoryRouter>,
  )

afterEach(cleanup)

describe('TripCard route note', () => {
  it('says a ticket covers part of the trip when the piers are not both ends', () => {
    renderCard('Commuter', A, B)
    expect(screen.getByText(/covers part of the/)).toBeTruthy()
  })

  it('shows no note when the ticket runs the whole route', () => {
    renderCard('Commuter', A, C)
    expect(screen.queryByText(/covers part of the/)).toBeNull()
  })

  it('explains that a sightseeing tour runs its whole route', () => {
    renderCard('Sightseeing', A, C)
    expect(screen.getByText(/Whole route/)).toBeTruthy()
  })
})
