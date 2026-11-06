/**
 * SQLite Local Repository for Staff Scanner
 * Handles fast local manifest persistence, ticket check-in atomic transactions,
 * and offline sync event queues.
 */

import { getDatabase } from './database';
import { getSessionEpoch } from '../auth/sessionEpoch';
import {
  PreloadManifest,
  Booking,
  Ticket,
  Trip,
  CheckInEvent,
  PublicKeyInfo,
  ManifestStats,
} from '../../types/scanner';
import { MyScannerAssignment } from '../../types/checkin';

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
   * Save preloaded manifest to SQLite (trips, bookings, tickets, public key).
   * Review PR #57 (3rd round, High - "Serialize manifest-writing operations and logout purge so
   * they cannot interleave incorrectly"): dùng withExclusiveTransactionAsync (không phải
   * withTransactionAsync, vốn KHÔNG exclusive trên native) - bảo đảm lần ghi này không thể chen
   * lẫn ở mức SQL với purgeManifestForLogout/clearCache đang chạy đồng thời.
   * Review PR #57 (4th round, High - "Close the remaining epoch-check-to-SQL-write race"):
   * syncService.preloadManifest kiểm tra epoch TRƯỚC khi gọi hàm này, nhưng có 1 await thật (native
   * bridge mở exclusive transaction) giữa lúc kiểm tra và lúc CHỮ KÝ GHI thực sự chạy - logout có
   * thể bump epoch + purge xong HOÀN TOÀN trong khoảng hở đó, rồi lần ghi này (đã pass check cũ)
   * mới thật sự chạy và ghi đè dữ liệu đã bị purge. Kiểm tra lại epoch LẦN NỮA làm việc ĐẦU TIÊN
   * BÊN TRONG transaction exclusive (cùng khoá mà purgeManifestForLogout cũng dùng) - nếu logout
   * đã bump epoch tại thời điểm giao dịch này THỰC SỰ cầm được khoá, throw để KHÔNG ghi gì cả (dù
   * có thắng cuộc giành khoá trước purge thì cũng không sao - purge chạy sau vẫn xoá sạch; chỉ nguy
   * hiểm khi purge đã xong TRƯỚC, nên check này mới có ý nghĩa).
   */
  static async saveManifest(manifest: PreloadManifest, expectedEpoch?: number): Promise<void> {
    const db = await getDatabase();

    await db.withExclusiveTransactionAsync(async (txn) => {
      if (expectedEpoch !== undefined && getSessionEpoch() !== expectedEpoch) {
        throw new Error('Phiên đăng nhập đã kết thúc trong lúc chờ ghi SQLite - bỏ qua, không ghi gì.');
      }

      // 1. Save or update Public Key
      if (manifest.publicKey) {
        await txn.runAsync(
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
        await txn.runAsync(
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
        await txn.runAsync(
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
          await txn.runAsync(
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
      await txn.runAsync(
        `INSERT OR REPLACE INTO scanner_config (key, value) VALUES ('last_preload_at', ?)`,
        [new Date().toISOString()]
      );
      await txn.runAsync(
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
    const conflictRows = await db.getAllAsync<any>(
      `SELECT id FROM offline_checkin_events WHERE sync_status = 'Conflict'`
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
      conflictSyncCount: conflictRows.length,
      lastPreloadAt: lastPreloadConfig ? lastPreloadConfig.value : null,
      databaseSizeKb: Math.round(
        (tripRows.length * 0.5 + bookingRows.length * 0.8 + ticketRows.length * 0.4 + pendingRows.length * 0.6) * 10
      ) / 10,
    };
  }

  /**
   * Clear all local tables (Reset Cache), nhưng CHỈ khi không còn event Pending/Conflict (MOB-05).
   * Review PR #57 (Medium, rồi High ở lần review thứ 2 - "Cache deletion is not fully isolated"):
   * trước đây useScannerStore.clearCache đọc count Pending/Conflict rồi mới gọi hàm xoá ở 2 bước
   * riêng (2 lượt await khác nhau) - 1 sự kiện mới có thể được ghi xen giữa lúc đọc count và lúc
   * DELETE thực sự chạy. Gộp đếm + xoá vào 1 `withTransactionAsync` (bản trước) đã thu hẹp cửa sổ
   * đó ở mức code của hàm này, nhưng reviewer chỉ đúng: `withTransactionAsync` của expo-sqlite
   * KHÔNG exclusive - 1 lệnh ghi khác NGOÀI function này (vd checkInTickets) vẫn có thể chen vào
   * giữa các câu lệnh bên trong transaction này trên native (xem ghi chú ở DatabaseAdapter). Đổi
   * sang `withExclusiveTransactionAsync` thật - mọi câu lệnh bên trong PHẢI chạy trên `txn` (tham
   * số callback), không phải `db` ngoài, theo đúng yêu cầu của expo-sqlite.
   * db.execAsync với chuỗi multi-statement là NO-OP trên InMemoryFallbackDatabase (web/test - xem
   * database.ts) - dùng từng runAsync riêng để chạy đúng trên CẢ native lẫn fallback.
   */
  static async clearCache(): Promise<{ cleared: boolean; pendingSyncCount: number; conflictSyncCount: number }> {
    const db = await getDatabase();
    return await db.withExclusiveTransactionAsync(async (txn) => {
      const pendingRows = await txn.getAllAsync<any>(
        `SELECT id FROM offline_checkin_events WHERE sync_status = 'Pending'`
      );
      const conflictRows = await txn.getAllAsync<any>(
        `SELECT id FROM offline_checkin_events WHERE sync_status = 'Conflict'`
      );
      if (pendingRows.length > 0 || conflictRows.length > 0) {
        return { cleared: false, pendingSyncCount: pendingRows.length, conflictSyncCount: conflictRows.length };
      }

      await txn.runAsync(`DELETE FROM manifest_tickets`);
      await txn.runAsync(`DELETE FROM manifest_bookings`);
      await txn.runAsync(`DELETE FROM manifest_trips`);
      await txn.runAsync(`DELETE FROM offline_checkin_events`);
      await txn.runAsync(`DELETE FROM scanner_config`);
      return { cleared: true, pendingSyncCount: 0, conflictSyncCount: 0 };
    });
  }

  /**
   * Xoá dữ liệu khách hàng (manifest) khỏi SQLite khi Staff đăng xuất (MOB-06), để Staff kế
   * tiếp đăng nhập trên cùng thiết bị không còn tra cứu được tên/SĐT khách của ca trước (không chỉ
   * ẩn trên 1 màn hình lịch sử - manifest_bookings/tickets còn tồn tại thì QR scan/tra tay bình
   * thường của Staff kế tiếp vẫn đọc được). KHÔNG xoá offline_checkin_events hay active_assignment
   * ở đây - lịch sử Pending/Conflict phải giữ nguyên để đối soát dù đổi tài khoản (các field cần
   * hiển thị đã denormalize sẵn trong chính bảng events, không cần tới bảng manifest).
   * Dùng withExclusiveTransactionAsync (không phải withTransactionAsync, KHÔNG exclusive trên
   * native) - như saveManifest/clearCache, để lần xoá này không thể chen lẫn ở mức SQL với 1 lần
   * preload đang ghi dữ liệu đồng thời (review PR #57, 3rd round, High).
   */
  static async purgeManifestForLogout(): Promise<void> {
    const db = await getDatabase();
    await db.withExclusiveTransactionAsync(async (txn) => {
      await txn.runAsync(`DELETE FROM manifest_tickets`);
      await txn.runAsync(`DELETE FROM manifest_bookings`);
      await txn.runAsync(`DELETE FROM manifest_trips`);
    });
  }

  /**
   * Đánh dấu/gỡ cờ "còn 1 lần purge manifest đăng xuất bị lỗi chưa xử lý xong" (Review PR #57 -
   * Logout privacy). useAuthStore.logout thử lại purge 1 lần tại chỗ; nếu vẫn lỗi (vd đĩa đang bị
   * khoá), đặt cờ này để lần đăng nhập/khởi động kế tiếp trên CHÍNH thiết bị này bắt buộc thử xoá
   * lại trước khi tin tưởng dữ liệu manifest hiện có thuộc về Staff mới hay vẫn là rác của ca cũ.
   */
  static async setPendingManifestPurge(pending: boolean): Promise<void> {
    const db = await getDatabase();
    if (pending) {
      // Giá trị PHẢI truyền qua tham số `?` (như mọi INSERT scanner_config khác trong file này),
      // không viết literal ('1') ngay trong câu SQL - InMemoryFallbackDatabase.runAsync (database.ts)
      // chỉ nhận diện đúng mẫu "VALUES ('key', ?)" qua regex; viết literal khiến regex không khớp
      // và lệnh ghi trở thành no-op IM LẶNG trên môi trường web/test (bug thật, bắt được qua Test
      // Group 9 của verify-staff-scanner.ts).
      await db.runAsync(`INSERT OR REPLACE INTO scanner_config (key, value) VALUES ('pending_manifest_purge', ?)`, ['1']);
    } else {
      await db.runAsync(`DELETE FROM scanner_config WHERE key = 'pending_manifest_purge'`);
    }
  }

  static async isManifestPurgePending(): Promise<boolean> {
    const db = await getDatabase();
    const row = await db.getFirstAsync<any>(`SELECT value FROM scanner_config WHERE key = 'pending_manifest_purge'`);
    return !!row?.value;
  }

  /**
   * Lưu phân công đang hoạt động (MOB-02) để app restart khi offline vẫn soát vé được tiếp,
   * không chỉ dựa vào state Zustand trong RAM (mất ngay khi app bị OS kill). Gắn kèm staffId để
   * Staff khác đăng nhập trên cùng máy không bị kế thừa nhầm phân công của người trước.
   * Review PR #57 (4th round, High - "Also cover a stale saveActiveAssignment() completion after
   * logout"): cùng pattern re-check epoch BÊN TRONG exclusive transaction như saveManifest - nếu
   * logout đã bump epoch + clearActiveAssignment xong TRƯỚC KHI giao dịch này thực sự cầm được
   * khoá, throw để không ghi đè lại 1 phân công đã bị logout dọn sạch.
   */
  static async saveActiveAssignment(
    assignment: MyScannerAssignment, staffId: string, expectedEpoch?: number
  ): Promise<void> {
    const db = await getDatabase();
    await db.withExclusiveTransactionAsync(async (txn) => {
      if (expectedEpoch !== undefined && getSessionEpoch() !== expectedEpoch) {
        throw new Error('Phiên đăng nhập đã kết thúc trong lúc chờ ghi SQLite - bỏ qua, không ghi gì.');
      }
      await txn.runAsync(
        `INSERT OR REPLACE INTO scanner_config (key, value) VALUES ('active_assignment', ?)`,
        [JSON.stringify({ staffId, assignment })]
      );
    });
  }

  /** Khôi phục phân công đã lưu, chỉ khi đúng staffId hiện đang đăng nhập (MOB-02, MOB-06). */
  static async getActiveAssignment(staffId: string): Promise<MyScannerAssignment | null> {
    const db = await getDatabase();
    const row = await db.getFirstAsync<any>(
      `SELECT value FROM scanner_config WHERE key = 'active_assignment'`
    );
    if (!row?.value) return null;
    try {
      const parsed = JSON.parse(row.value);
      if (parsed.staffId !== staffId) return null;
      return parsed.assignment as MyScannerAssignment;
    } catch {
      return null;
    }
  }

  // Exclusive transaction (không phải runAsync trần) - khớp với saveActiveAssignment, để 2 thao
  // tác ghi/xoá cùng 1 key không thể chen lẫn ở mức SQL với nhau (review PR #57, 4th round).
  static async clearActiveAssignment(): Promise<void> {
    const db = await getDatabase();
    await db.withExclusiveTransactionAsync(async (txn) => {
      await txn.runAsync(`DELETE FROM scanner_config WHERE key = 'active_assignment'`);
    });
  }
}

