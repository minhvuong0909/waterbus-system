import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat, BoatSeatInventory, SeatInput } from '../types'
import { setActive } from './catalogRequests'

export const getBoat = (boatId: string) => apiRequest<AdminBoat>(API_ENDPOINTS.boat(boatId))

/** `GET /boats/{boatId}/seats`: ghế của tàu và sơ đồ ghế đã đủ đến đâu. */
export const getBoatSeats = (boatId: string) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeats(boatId))

/** `POST /boats/{boatId}/seats`: thêm một loạt ghế; một ghế sai thì cả loạt bị từ chối. */
export const addBoatSeats = (boatId: string, seats: SeatInput[]) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeats(boatId), { method: 'POST', body: { seats } })

export const updateBoatSeat = (boatId: string, seatId: string, input: SeatInput) =>
  apiRequest<BoatSeatInventory>(API_ENDPOINTS.boatSeat(boatId, seatId), { method: 'PUT', body: input })

/** Ghế đang giữ hoặc đã bán trên chuyến chưa kết thúc thì không tắt được (409). */
export const setBoatSeatActive = (boatId: string, seatId: string, active: boolean) =>
  setActive<BoatSeatInventory>(API_ENDPOINTS.boatSeat(boatId, seatId), active)
