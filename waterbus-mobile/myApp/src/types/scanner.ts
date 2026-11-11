/**
 * Domain types & interfaces for Staff Scanner Mobile App
 * SMART WATERBUS SYSTEM (BR v2.0 FINAL & ERD Alignment)
 */

export type TripType = 'Commuter' | 'Sightseeing';
export type TicketStatus = 'Valid' | 'CheckedIn' | 'Cancelled' | 'Refunded';
export type BoardingStatus = 'NotCheckedIn' | 'CheckedIn' | 'NoShow';
export type SyncStatus = 'Pending' | 'Synced' | 'Conflict';
export type CheckInOutcome = 'Success' | 'AlreadyCheckedIn' | 'InvalidStop' | 'TicketInvalid' | 'Conflict';

export interface Station {
  id: string;
  code: string;
  name: string;
  orderIndex: number;
  latitude?: number;
  longitude?: number;
}

export interface Trip {
  id: string;
  tripCode: string;
  routeName: string;
  tripType: TripType;
  boatName: string;
  departureStationId: string;
  departureStationName: string;
  arrivalStationId: string;
  arrivalStationName: string;
  departureTime: string; // ISO 8601
  arrivalTime?: string;
  salesCloseAt?: string;
  status: string; // Scheduled, Boarding, EnRoute, Completed, etc.
}

export interface Ticket {
  id: string;
  bookingId: string;
  ticketNumber: string;
  seatCode: string;
  seatClass: string; // FrontCabin, Standard, Outdoor
  passengerName: string;
  status: TicketStatus;
  boardingStatus: BoardingStatus;
  checkedInAt?: string | null;
  checkedInByStaffId?: string | null;
  faceFare?: number;
  paidFare?: number;
}

export interface Booking {
  id: string;
  publicBookingId: string; // Non-sensitive public identifier in QR (BR-QR-02)
  bookingCode: string;
  tripId: string;
  customerName: string;
  customerPhone?: string;
  totalAmount: number;
  qrCredentialVersion: number;
  status: string; // Confirmed, Completed, Cancelled
  tickets: Ticket[];
  trip?: Trip;
}

export interface PublicKeyInfo {
  kid: string;
  algorithm: 'RS256' | 'HS256' | 'ES256';
  publicKeyPem: string;
  validFrom: string;
  validTo: string;
  isActive: boolean;
}

export interface PreloadManifest {
  stationId: string;
  stationName: string;
  generatedAt: string;
  publicKey: PublicKeyInfo;
  trips: Trip[];
  bookings: Booking[];
}

export interface CheckInEvent {
  id: string;
  clientEventId: string; // UUID v4 deduplication key (BR-OFF-03)
  ticketId: string;
  ticketNumber: string;
  passengerName: string;
  seatCode: string;
  bookingId: string;
  tripId: string;
  tripStopCallId: string;
  staffAccountId: string;
  staffName: string;
  scannerDeviceId: string;
  occurredAtDevice: string; // ISO 8601
  receivedAtServer?: string;
  outcome: CheckInOutcome;
  syncStatus: SyncStatus;
  retryCount: number;
  lastSyncAttemptAt?: string;
  errorMessage?: string;
  notes?: string;
}

export interface OfflineSyncBatch {
  scannerDeviceId: string;
  staffAccountId: string;
  batchTimestamp: string;
  events: CheckInEvent[];
}

export interface SyncBatchResponse {
  batchId: string;
  successCount: number;
  conflictCount: number;
  processedEvents: {
    clientEventId: string;
    outcome: CheckInOutcome;
    syncStatus: SyncStatus;
    message?: string;
  }[];
}

export interface QrPayload {
  pbid: string; // publicBookingId
  ver: number;  // qrCredentialVersion
  tid?: string; // tripId
  iat?: number; // issuedAt timestamp in seconds
  exp?: number; // expiration timestamp in seconds
  kid?: string; // key id
  sig?: string; // signature
}

export interface QrVerificationResult {
  isValid: boolean;
  publicBookingId?: string;
  version?: number;
  tripId?: string;
  error?: string;
  rawPayload?: any;
  verifiedOffline: boolean;
  algorithmUsed?: string;
}

export interface ManifestStats {
  tripCount: number;
  bookingCount: number;
  ticketCount: number;
  checkedInCount: number;
  pendingSyncCount: number;
  lastPreloadAt: string | null;
  databaseSizeKb: number;
}

export interface ScannerDeviceInfo {
  deviceId: string;
  deviceCode: string;
  staffId: string;
  staffName: string;
  stationId: string;
  stationName: string;
  isPrimaryOffline: boolean;
  forceOfflineMode: boolean;
}

