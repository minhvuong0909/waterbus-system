import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat, BoatSeatInventory, SeatInput } from '../types'
import { setActive } from './catalogRequests'

export const getBoat = (boatId: string) => apiRequest<AdminBoat>(API_ENDPOINTS.boat(boatId))

/** `GET /boats/{boatId}/seats`: the seats of the boat and how complete its seat plan is. */
export const getBoatSeats = (boatId: string) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeats(boatId))

/** `POST /boats/{boatId}/seats`: adds a batch of seats; one invalid seat rejects the whole batch. */
export const addBoatSeats = (boatId: string, seats: SeatInput[]) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeats(boatId), { method: 'POST', body: { seats } })

export const updateBoatSeat = (boatId: string, seatId: string, input: SeatInput) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeat(boatId, seatId), { method: 'PUT', body: input })

/** A seat held or sold on a trip that has not ended cannot be switched off (409). */
export const setBoatSeatActive = (boatId: string, seatId: string, active: boolean) =>
  setActive<BoatSeatInventory>(API_ENDPOINTS.boatSeat(boatId, seatId), active)
