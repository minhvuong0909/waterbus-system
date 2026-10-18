/**
 * Types cho phân công soát vé - khớp MyScannerAssignmentDto ở backend
 * (GET /api/v1/checkins/assignments/mine).
 */
export interface MyScannerAssignment {
  assignmentId: string;
  tripStopCallId: string;
  tripId: string;
  routeName: string;
  stationName: string;
  visitOrder: number;
  tripStatus: string;
  estimatedArrivalTime: string | null;
  deviceId: string;
  deviceCode: string;
  isPrimaryOffline: boolean;
  activeFrom: string;
  activeUntil: string | null;
  checkInOpenAt: string | null;
  checkInCloseAt: string | null;
}
