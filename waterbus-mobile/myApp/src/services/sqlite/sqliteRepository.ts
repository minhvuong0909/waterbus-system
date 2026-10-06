/**
 * SQLite Local Repository for Staff Scanner
 * Handles fast local manifest persistence, ticket check-in atomic transactions,
 * and offline sync event queues.
 */

import { getDatabase } from './database';
import {
  PreloadManifest,
  Booking,
  Ticket,
  Trip,
  CheckInEvent,
  PublicKeyInfo,
  ManifestStats,
} from '../../types/scanner';

function generateUUID(): string {
  // RFC4122 v4 UUID generator without native deps
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export class SqliteRepository {
  /**
   * Save preloaded manifest to SQLite (trips, bookings, tickets, public key)
   */
  static async saveManifest(manifest: PreloadManifest): Promise<void> {
    const db = await getDatabase();

    await db.withTransactionAsync(async () => {
      // 1. Save or update Public Key
      if (manifest.publicKey) {
        await db.runAsync(
          `INSERT OR REPLACE INTO public_keys (kid, algorithm, public_key_pem, valid_from, valid_to, is_active)
           VALUES (?, ?, ?, ?, ?, ?)`,
          [
            manifest.publicKey.kid,
            manifest.publicKey.algorithm,
            manifest.publicKey.publicKeyPem,
            manifest.publicKey.validFrom,
            manifest.publicKey.validTo,
            manifest.publicKey.isActive ? 1 : 0,
          ]
        );
      }

      // 2. Save trips
      for (const trip of manifest.trips) {
        await db.runAsync(
          `INSERT OR REPLACE INTO manifest_trips (
            id, trip_code, route_name, trip_type, boat_name,
            departure_station_id, departure_station_name,
            arrival_station_id, arrival_station_name,
            departure_time, arrival_time, sales_close_at, status
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
          [
            trip.id,
            trip.tripCode,
            trip.routeName,
            trip.tripType,
            trip.boatName,
            trip.departureStationId,
            trip.departureStationName,
            trip.arrivalStationId,
            trip.arrivalStationName,
            trip.departureTime,
            trip.arrivalTime || null,
            trip.salesCloseAt || null,
            trip.status,
          ]
        );
      }

      // 3. Save bookings & tickets
      for (const booking of manifest.bookings) {
        await db.runAsync(
          `INSERT OR REPLACE INTO manifest_bookings (
            id, public_booking_id, booking_code, trip_id,
            customer_name, customer_phone, total_amount,
            qr_credential_version, status
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
          [
            booking.id,
            booking.publicBookingId,
            booking.bookingCode,
            booking.tripId,
            booking.customerName,
            booking.customerPhone || null,
            booking.totalAmount,
            booking.qrCredentialVersion || 1,
            booking.status,
          ]
        );

        for (const ticket of booking.tickets) {
          await db.runAsync(
            `INSERT OR REPLACE INTO manifest_tickets (
              id, booking_id, ticket_number, seat_code, seat_class,
              passenger_name, status, boarding_status, checked_in_at,
              checked_in_by_staff_id, face_fare, paid_fare
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
            [
              ticket.id,
              ticket.bookingId,
              ticket.ticketNumber,
              ticket.seatCode,
              ticket.seatClass,
              ticket.passengerName,
              ticket.status,
              ticket.boardingStatus,
              ticket.checkedInAt || null,
              ticket.checkedInByStaffId || null,
              ticket.faceFare || 0,
              ticket.paidFare || 0,
            ]
          );
        }
      }

      // Record last preload timestamp
      await db.runAsync(
        `INSERT OR REPLACE INTO scanner_config (key, value) VALUES ('last_preload_at', ?)`,
        [new Date().toISOString()]
      );
      await db.runAsync(
        `INSERT OR REPLACE INTO scanner_config (key, value) VALUES ('station_id', ?)`,
        [manifest.stationId]
      );
    });
  }

  /**
   * Ultra-fast lookup of Booking by publicBookingId (from QR code)
   */
  static async getBookingByPublicId(publicBookingId: string): Promise<Booking | null> {
    const db = await getDatabase();

    const bookingRow = await db.getFirstAsync<any>(
      `SELECT * FROM manifest_bookings WHERE public_booking_id = ?`,
      [publicBookingId]
    );

    if (!bookingRow) {
      return null;
    }

    // Fetch associated trip
    const tripRow = await db.getFirstAsync<any>(
      `SELECT * FROM manifest_trips WHERE id = ?`,
      [bookingRow.trip_id]
    );

    // Fetch tickets
    const ticketRows = await db.getAllAsync<any>(
      `SELECT * FROM manifest_tickets WHERE booking_id = ? ORDER BY seat_code ASC`,
      [bookingRow.id]
    );

    const tickets: Ticket[] = ticketRows.map((r) => ({
      id: r.id,
      bookingId: r.booking_id,
      ticketNumber: r.ticket_number,
      seatCode: r.seat_code,
      seatClass: r.seat_class,
      passengerName: r.passenger_name,
      status: r.status,
      boardingStatus: r.boarding_status,
      checkedInAt: r.checked_in_at,
      checkedInByStaffId: r.checked_in_by_staff_id,
      faceFare: r.face_fare,
      paidFare: r.paid_fare,
    }));

    let trip: Trip | undefined = undefined;
    if (tripRow) {
      trip = {
        id: tripRow.id,
        tripCode: tripRow.trip_code,
        routeName: tripRow.route_name,
        tripType: tripRow.trip_type,
        boatName: tripRow.boat_name,
        departureStationId: tripRow.departure_station_id,
        departureStationName: tripRow.departure_station_name,
        arrivalStationId: tripRow.arrival_station_id,
        arrivalStationName: tripRow.arrival_station_name,
        departureTime: tripRow.departure_time,
        arrivalTime: tripRow.arrival_time,
        salesCloseAt: tripRow.sales_close_at,
        status: tripRow.status,
      };
    }

    return {
      id: bookingRow.id,
      publicBookingId: bookingRow.public_booking_id,
      bookingCode: bookingRow.booking_code,
      tripId: bookingRow.trip_id,
      customerName: bookingRow.customer_name,
      customerPhone: bookingRow.customer_phone,
      totalAmount: bookingRow.total_amount,
      qrCredentialVersion: bookingRow.qr_credential_version,
      status: bookingRow.status,
      tickets,
      trip,
    };
  }

  /**
   * Search by bookingCode or ticketNumber (Fallback manual search)
   */
  static async searchBookingOrTicket(query: string): Promise<Booking | null> {
    const trimmed = query.trim().toUpperCase();
    const db = await getDatabase();

    // Check by publicBookingId
    let booking = await this.getBookingByPublicId(trimmed);
    if (booking) return booking;

    // Check by booking_code
    const bookingRow = await db.getFirstAsync<any>(
      `SELECT public_booking_id FROM manifest_bookings WHERE booking_code = ?`,
      [trimmed]
    );
    if (bookingRow) {
      return await this.getBookingByPublicId(bookingRow.public_booking_id);
    }

    // Check by ticket_number
    const ticketRow = await db.getFirstAsync<any>(
      `SELECT booking_id FROM manifest_tickets WHERE ticket_number = ?`,
      [trimmed]
    );
    if (ticketRow) {
      const bRow = await db.getFirstAsync<any>(
        `SELECT public_booking_id FROM manifest_bookings WHERE id = ?`,
        [ticketRow.booking_id]
      );
      if (bRow) {
        return await this.getBookingByPublicId(bRow.public_booking_id);
      }
    }

    return null;
  }

  /**
   * Perform atomic check-in on selected tickets and insert into offline queue (BR-OFF-03, BR-OFF-05)
   */
  static async checkInTickets(params: {
    ticketIds: string[];
    bookingId: string;
    tripId: string;
    tripStopCallId: string;
    staffAccountId: string;
    staffName: string;
    scannerDeviceId: string;
    notes?: string;
  }): Promise<{ checkedInCount: number; events: CheckInEvent[] }> {
    const db = await getDatabase();
    const nowIso = new Date().toISOString();
    const generatedEvents: CheckInEvent[] = [];

    await db.withTransactionAsync(async () => {
      for (const ticketId of params.ticketIds) {
        // Fetch ticket details
        const ticketRow = await db.getFirstAsync<any>(
          `SELECT * FROM manifest_tickets WHERE id = ?`,
          [ticketId]
        );

        if (!ticketRow) continue;

        // Update ticket in local DB
        await db.runAsync(
          `UPDATE manifest_tickets 
           SET boarding_status = 'CheckedIn', 
               checked_in_at = ?, 
               checked_in_by_staff_id = ? 
           WHERE id = ?`,
          [nowIso, params.staffAccountId, ticketId]
        );

        // Create CheckInEvent with unique client_event_id (UUID v4)
        const clientEventId = generateUUID();
        const eventId = generateUUID();

        await db.runAsync(
          `INSERT INTO offline_checkin_events (
            id, client_event_id, ticket_id, ticket_number, passenger_name,
            seat_code, booking_id, trip_id, trip_stop_call_id, staff_account_id,
            staff_name, scanner_device_id, occurred_at_device, outcome,
            sync_status, retry_count, notes
          ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'Success', 'Pending', 0, ?)`,
          [
            eventId,
            clientEventId,
            ticketId,
            ticketRow.ticket_number,
            ticketRow.passenger_name,
            ticketRow.seat_code,
            params.bookingId,
            params.tripId,
            params.tripStopCallId,
            params.staffAccountId,
            params.staffName,
            params.scannerDeviceId,
            nowIso,
            params.notes || null,
          ]
        );

        generatedEvents.push({
          id: eventId,
          clientEventId,
          ticketId,
          ticketNumber: ticketRow.ticket_number,
          passengerName: ticketRow.passenger_name,
          seatCode: ticketRow.seat_code,
          bookingId: params.bookingId,
          tripId: params.tripId,
          tripStopCallId: params.tripStopCallId,
          staffAccountId: params.staffAccountId,
          staffName: params.staffName,
          scannerDeviceId: params.scannerDeviceId,
          occurredAtDevice: nowIso,
          outcome: 'Success',
          syncStatus: 'Pending',
          retryCount: 0,
          notes: params.notes,
        });
      }
    });

    return {
      checkedInCount: generatedEvents.length,
      events: generatedEvents,
    };
  }

  /**
   * Get pending offline check-in events for batch sync
   */
  static async getPendingEvents(limit = 50): Promise<CheckInEvent[]> {
    const db = await getDatabase();
    const rows = await db.getAllAsync<any>(
      `SELECT * FROM offline_checkin_events 
       WHERE sync_status = 'Pending' 
       ORDER BY occurred_at_device ASC 
       LIMIT ?`,
      [limit]
    );

    return rows.map((r) => ({
      id: r.id,
      clientEventId: r.client_event_id,
      ticketId: r.ticket_id,
      ticketNumber: r.ticket_number,
      passengerName: r.passenger_name,
      seatCode: r.seat_code,
      bookingId: r.booking_id,
      tripId: r.trip_id,
      tripStopCallId: r.trip_stop_call_id,
      staffAccountId: r.staff_account_id,
      staffName: r.staff_name,
      scannerDeviceId: r.scanner_device_id,
      occurredAtDevice: r.occurred_at_device,
      receivedAtServer: r.received_at_server,
      outcome: r.outcome,
      syncStatus: r.sync_status,
      retryCount: r.retry_count,
      lastSyncAttemptAt: r.last_sync_attempt_at,
      errorMessage: r.error_message,
      notes: r.notes,
    }));
  }

  /**
   * Mark events as Synced upon server acknowledgment
   */
  static async markEventsSynced(
    updates: { clientEventId: string; receivedAtServer?: string }[]
  ): Promise<void> {
    const db = await getDatabase();
    const nowIso = new Date().toISOString();

    await db.withTransactionAsync(async () => {
      for (const update of updates) {
        await db.runAsync(
          `UPDATE offline_checkin_events 
           SET sync_status = 'Synced', 
               received_at_server = ?,
               last_sync_attempt_at = ? 
           WHERE client_event_id = ?`,
          [update.receivedAtServer || nowIso, nowIso, update.clientEventId]
        );
      }
    });
  }

  /**
   * Record sync failure / retry attempt
   */
  static async recordSyncFailure(
    clientEventIds: string[],
    errorMessage: string
  ): Promise<void> {
    const db = await getDatabase();
    const nowIso = new Date().toISOString();

    for (const cEventId of clientEventIds) {
      await db.runAsync(
        `UPDATE offline_checkin_events 
         SET retry_count = retry_count + 1, 
             last_sync_attempt_at = ?, 
             error_message = ? 
         WHERE client_event_id = ?`,
        [nowIso, errorMessage, cEventId]
      );
    }
  }

  /**
   * Mark an event as Conflict (e.g. checked-in on another device earlier)
   */
  static async markEventConflict(
    clientEventId: string,
    reason: string
  ): Promise<void> {
    const db = await getDatabase();
    const nowIso = new Date().toISOString();

    await db.runAsync(
      `UPDATE offline_checkin_events 
       SET sync_status = 'Conflict', 
           outcome = 'Conflict',
           last_sync_attempt_at = ?, 
           error_message = ? 
       WHERE client_event_id = ?`,
      [nowIso, reason, clientEventId]
    );
  }

  /**
   * Get active public verification key
   */
  static async getActivePublicKey(kid?: string): Promise<PublicKeyInfo | null> {
    const db = await getDatabase();
    let row: any = null;

    if (kid) {
      row = await db.getFirstAsync<any>(
        `SELECT * FROM public_keys WHERE kid = ? AND is_active = 1`,
        [kid]
      );
    } else {
      row = await db.getFirstAsync<any>(
        `SELECT * FROM public_keys WHERE is_active = 1 ORDER BY valid_from DESC LIMIT 1`
      );
    }

    if (!row) return null;

    return {
      kid: row.kid,
      algorithm: row.algorithm,
      publicKeyPem: row.public_key_pem,
      validFrom: row.valid_from,
      validTo: row.valid_to,
      isActive: row.is_active === 1,
    };
  }

  /**
   * Get local cache statistics for Staff UI
   */
  static async getManifestStats(): Promise<ManifestStats> {
    const db = await getDatabase();

    const tripRows = await db.getAllAsync<any>(`SELECT id FROM manifest_trips`);
    const bookingRows = await db.getAllAsync<any>(`SELECT id FROM manifest_bookings`);
    const ticketRows = await db.getAllAsync<any>(`SELECT id, boarding_status FROM manifest_tickets`);
    const pendingRows = await db.getAllAsync<any>(
      `SELECT id FROM offline_checkin_events WHERE sync_status = 'Pending'`
    );

    const lastPreloadConfig = await db.getFirstAsync<any>(
      `SELECT value FROM scanner_config WHERE key = 'last_preload_at'`
    );

    const checkedInCount = ticketRows.filter((t) => t.boarding_status === 'CheckedIn').length;

    return {
      tripCount: tripRows.length,
      bookingCount: bookingRows.length,
      ticketCount: ticketRows.length,
      checkedInCount,
      pendingSyncCount: pendingRows.length,
      lastPreloadAt: lastPreloadConfig ? lastPreloadConfig.value : null,
      databaseSizeKb: Math.round(
        (tripRows.length * 0.5 + bookingRows.length * 0.8 + ticketRows.length * 0.4 + pendingRows.length * 0.6) * 10
      ) / 10,
    };
  }

  /**
   * Clear all local tables (Reset Cache)
   */
  static async clearCache(): Promise<void> {
    const db = await getDatabase();
    await db.execAsync(`
      DELETE FROM manifest_tickets;
      DELETE FROM manifest_bookings;
      DELETE FROM manifest_trips;
      DELETE FROM offline_checkin_events;
      DELETE FROM scanner_config;
    `);
  }
}

