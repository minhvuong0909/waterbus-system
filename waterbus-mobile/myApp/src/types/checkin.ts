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

/** Khớp ManifestTicketDto (GetScannerManifestQuery). */
export interface ManifestTicketResponse {
  ticketId: string;
  ticketCode: string;
  passengerName: string;
  seatCode: string;
  seatClass: string;
  boardingStatus: string;
  paidFare: number;
}

/** Khớp ManifestBookingDto - PublicBookingId là khoá tra cứu khi quét QR (BR-QR-02). */
export interface ManifestBookingResponse {
  bookingId: string;
  publicBookingId: string;
  bookingCode: string;
  tripId: string;
  customerName: string;
  customerPhone: string | null;
  quotedTotal: number;
  qrCredentialVersion: number;
  status: string;
  tickets: ManifestTicketResponse[];
}

/** Khớp ManifestTripDto. */
export interface ManifestTripResponse {
  tripId: string;
  routeName: string;
  boatName: string;
  departureStationName: string;
  arrivalStationName: string;
  departureTime: string;
  arrivalTime: string | null;
  salesCloseAt: string | null;
  tripType: string;
  status: string;
}

/** Khớp ScannerManifestDto (GET /api/v1/checkins/manifest/{tripStopCallId}). */
export interface ScannerManifestResponse {
  tripStopCallId: string;
  stationName: string;
  checkInOpenAt: string | null;
  checkInCloseAt: string | null;
  trip: ManifestTripResponse;
  bookings: ManifestBookingResponse[];
  generatedAt: string;
}

/** Khớp CheckInEventInput (POST /api/v1/checkins/sync). */
export interface CheckInEventSyncInput {
  ticketId: string;
  tripStopCallId: string;
  clientEventId: string;
  occurredAtDevice: string;
  note?: string | null;
}

/** Khớp CheckInEventSyncResult. */
export interface CheckInEventSyncResult {
  clientEventId: string;
  outcome: string;
  wasAlreadySynced: boolean;
  error: string | null;
}

/** Khớp SyncCheckInEventsResultDto. */
export interface SyncCheckInEventsResponse {
  results: CheckInEventSyncResult[];
}

/** Khớp BookingQrPublicKey (GET /api/v1/checkouts/qr-public-key). */
export interface QrPublicKeyResponse {
  kid: string;
  algorithm: string;
  publicKeyPem: string;
}
