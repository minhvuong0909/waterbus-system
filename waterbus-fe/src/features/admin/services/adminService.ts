// Mọi màn Admin/Captain gọi qua file này; dữ liệu thật nằm ở adminService.api.ts.
export {
  getStations,
  createStation,
  updateStation,
  deleteStation,
  getRoutes,
  getBoats,
  createBoat,
  deleteBoat,
  getTrips,
  getWeatherAlerts,
  getAlertsWithTrips,
  closeAlert,
  getOperationTrips,
  getOperationLogs,
  operateTrip,
} from './adminService.api'
