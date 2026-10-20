import { USE_MOCK } from '../../../constant/env'
import * as api from './adminService.api'
import * as mock from './adminService.mock'

// Mọi màn Admin/Captain gọi qua file này. Dữ liệu thật nằm ở adminService.api.ts, dữ liệu giả
// (chỉ khi VITE_USE_MOCK=true) ở adminService.mock.ts; hai file có cùng tên hàm và chữ ký.
const service: typeof mock = USE_MOCK ? mock : api

export const {
  getStations,
  createStation,
  updateStation,
  deleteStation,
  getRoutes,
  getBoats,
  createBoat,
  deleteBoat,
  getFares,
  saveFares,
  schedulesSupported,
  getSchedules,
  createSchedule,
  setScheduleActive,
  generateTrips,
  getTrips,
  assignBoat,
  getWeatherAlerts,
  getAlertsWithTrips,
  closeAlert,
  getOperationTrips,
  getOperationLogs,
  operateTrip,
  getFleetPositions,
} = service
