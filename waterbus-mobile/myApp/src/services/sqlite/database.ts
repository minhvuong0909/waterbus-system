/**
 * SQLite Local Database Service
 * Provides offline-first persistence for preloaded trips, bookings, tickets, public keys,
 * and offline check-in event queues.
 * Supports Expo SQLite with an in-memory adapter only for Web/Unit Tests.
 */

const DB_NAME = 'waterbus_scanner.db';
const PUBLIC_KEYS_MIGRATION_TABLE = 'public_keys_legacy_migration';

function isNodeTestEnvironment(): boolean {
  return typeof process !== 'undefined' && process.release?.name === 'node';
}

export interface DatabaseAdapter {
  execAsync(sql: string): Promise<void>;
  runAsync(sql: string, params?: any[]): Promise<{ lastInsertRowId: number; changes: number }>;
  getAllAsync<T = any>(sql: string, params?: any[]): Promise<T[]>;
  getFirstAsync<T = any>(sql: string, params?: any[]): Promise<T | null>;
  withTransactionAsync<T>(callback: () => Promise<T>): Promise<T>;
}

class InMemoryFallbackDatabase implements DatabaseAdapter {
  private tables: Record<string, any[]> = {
    public_keys: [],
    manifest_trips: [],
    manifest_bookings: [],
    manifest_tickets: [],
    offline_checkin_events: [],
    scanner_config: [],
  };

  async execAsync(): Promise<void> {
  }

  async runAsync(sql: string, params: any[] = []): Promise<{ lastInsertRowId: number; changes: number }> {
    const trimmed = sql.trim().toUpperCase();
    if (trimmed.startsWith('INSERT INTO PUBLIC_KEYS') || trimmed.startsWith('INSERT OR REPLACE INTO PUBLIC_KEYS')) {
      const [kid, algo, pem, from, to, active] = params;
      const filtered = this.tables.public_keys.filter(k => k.kid !== kid);
      filtered.push({ kid, algorithm: algo, public_key_pem: pem, valid_from: from, valid_to: to, is_active: active });
      this.tables.public_keys = filtered;
      return { lastInsertRowId: 1, changes: 1 };
    }
    if (trimmed.startsWith('INSERT INTO MANIFEST_TRIPS') || trimmed.startsWith('INSERT OR REPLACE INTO MANIFEST_TRIPS')) {
      const [id, code, rName, tType, bName, depId, depName, arrId, arrName, depTime, arrTime, salesClose, status] = params;
      const filtered = this.tables.manifest_trips.filter(t => t.id !== id);
      filtered.push({
        id, trip_code: code, route_name: rName, trip_type: tType, boat_name: bName,
        departure_station_id: depId, departure_station_name: depName,
        arrival_station_id: arrId, arrival_station_name: arrName,
        departure_time: depTime, arrival_time: arrTime, sales_close_at: salesClose, status
      });
      this.tables.manifest_trips = filtered;
      return { lastInsertRowId: 1, changes: 1 };
    }
    if (trimmed.startsWith('INSERT INTO MANIFEST_BOOKINGS') || trimmed.startsWith('INSERT OR REPLACE INTO MANIFEST_BOOKINGS')) {
      const [id, pbid, code, tripId, cName, cPhone, amount, ver, status] = params;
      const filtered = this.tables.manifest_bookings.filter(b => b.id !== id);
      filtered.push({
        id, public_booking_id: pbid, booking_code: code, trip_id: tripId,
        customer_name: cName, customer_phone: cPhone, total_amount: amount,
        qr_credential_version: ver, status
      });
      this.tables.manifest_bookings = filtered;
      return { lastInsertRowId: 1, changes: 1 };
    }
    if (trimmed.startsWith('INSERT INTO MANIFEST_TICKETS') || trimmed.startsWith('INSERT OR REPLACE INTO MANIFEST_TICKETS')) {
      const [id, bId, tNum, sCode, sClass, pName, status, bStatus, cAt, cStaff, fFare, pFare] = params;
      const filtered = this.tables.manifest_tickets.filter(t => t.id !== id);
      filtered.push({
        id, booking_id: bId, ticket_number: tNum, seat_code: sCode, seat_class: sClass,
        passenger_name: pName, status, boarding_status: bStatus, checked_in_at: cAt,
        checked_in_by_staff_id: cStaff, face_fare: fFare, paid_fare: pFare
      });
      this.tables.manifest_tickets = filtered;
      return { lastInsertRowId: 1, changes: 1 };
    }
    if (trimmed.includes('INSERT INTO OFFLINE_CHECKIN_EVENTS') || trimmed.includes('INSERT OR REPLACE INTO OFFLINE_CHECKIN_EVENTS')) {
      const [id, cEventId, tId, tNum, pName, sCode, bId, trId, tscId, sAccId, sName, devId, occAt, notes] = params;
      const filtered = this.tables.offline_checkin_events.filter(e => e.client_event_id !== cEventId);
      filtered.push({
        id,
        client_event_id: cEventId,
        ticket_id: tId,
        ticket_number: tNum,
        passenger_name: pName,
        seat_code: sCode,
        booking_id: bId,
        trip_id: trId,
        trip_stop_call_id: tscId,
        staff_account_id: sAccId,
        staff_name: sName,
        scanner_device_id: devId,
        occurred_at_device: occAt,
        received_at_server: null,
        outcome: 'Success',
        sync_status: 'Pending',
        retry_count: 0,
        last_sync_attempt_at: null,
        error_message: null,
        notes: notes || null,
      });
      this.tables.offline_checkin_events = filtered;
      return { lastInsertRowId: 1, changes: 1 };
    }
    const normalized = trimmed.replace(/\s+/g, ' ');
    if (normalized.includes('UPDATE MANIFEST_TICKETS SET BOARDING_STATUS')) {
      const [cAt, cStaff, ticketId] = params;
      const ticket = this.tables.manifest_tickets.find(t => t.id === ticketId);
      if (ticket) {
        ticket.boarding_status = 'CheckedIn';
        ticket.checked_in_at = cAt;
        ticket.checked_in_by_staff_id = cStaff;
        return { lastInsertRowId: 0, changes: 1 };
      }
      return { lastInsertRowId: 0, changes: 0 };
    }
    if (normalized.includes("UPDATE OFFLINE_CHECKIN_EVENTS SET SYNC_STATUS = 'CONFLICT'")) {
      // markEventConflict - phải tách khỏi nhánh markEventsSynced bên dưới, nếu không sẽ bị
      // nhánh đó bắt nhầm trước (cùng bắt đầu bằng "SET SYNC_STATUS") và đánh sai thành Synced.
      const [lAtt, errMsg, cEventId] = params;
      const event = this.tables.offline_checkin_events.find(e => e.client_event_id === cEventId);
      if (event) {
        event.sync_status = 'Conflict';
        event.outcome = 'Conflict';
        event.last_sync_attempt_at = lAtt;
        event.error_message = errMsg;
        return { lastInsertRowId: 0, changes: 1 };
      }
      return { lastInsertRowId: 0, changes: 0 };
    }
    if (normalized.includes('UPDATE OFFLINE_CHECKIN_EVENTS SET SYNC_STATUS')) {
      const [recAt, lAtt, cEventId] = params;
      const targetId = cEventId || params[params.length - 1];
      const event = this.tables.offline_checkin_events.find(e => e.client_event_id === targetId);
      if (event) {
        event.sync_status = 'Synced';
        event.received_at_server = recAt;
        event.last_sync_attempt_at = lAtt;
        return { lastInsertRowId: 0, changes: 1 };
      }
      return { lastInsertRowId: 0, changes: 0 };
    }
    if (normalized.includes('UPDATE OFFLINE_CHECKIN_EVENTS SET RETRY_COUNT')) {
      // recordSyncFailure - trước đây KHÔNG được xử lý ở fallback in-memory: lỗi sync không bao
      // giờ thực sự được ghi lại (retry_count/error_message) khi chạy ngoài native (web/script test).
      const [lAtt, errMsg, cEventId] = params;
      const event = this.tables.offline_checkin_events.find(e => e.client_event_id === cEventId);
      if (event) {
        event.retry_count = (event.retry_count || 0) + 1;
        event.last_sync_attempt_at = lAtt;
        event.error_message = errMsg;
        return { lastInsertRowId: 0, changes: 1 };
      }
      return { lastInsertRowId: 0, changes: 0 };
    }
    if (trimmed.startsWith('DELETE FROM')) {
      for (const key of Object.keys(this.tables)) {
        if (trimmed.includes(key.toUpperCase())) {
          const count = this.tables[key].length;
          this.tables[key] = [];
          return { lastInsertRowId: 0, changes: count };
        }
      }
    }
    return { lastInsertRowId: 0, changes: 0 };
  }

  async getAllAsync<T = any>(sql: string, params: any[] = []): Promise<T[]> {
    const trimmed = sql.trim().toUpperCase();
    if (trimmed.includes('FROM MANIFEST_BOOKINGS') && trimmed.includes('PUBLIC_BOOKING_ID =')) {
      const pbid = params[0];
      return this.tables.manifest_bookings.filter(b => b.public_booking_id === pbid) as any;
    }
    if (trimmed.includes('FROM MANIFEST_TICKETS') && trimmed.includes('BOOKING_ID =')) {
      const bId = params[0];
      return this.tables.manifest_tickets.filter(t => t.booking_id === bId) as any;
    }
    if (trimmed.includes('FROM OFFLINE_CHECKIN_EVENTS')) {
      if (trimmed.includes("SYNC_STATUS = 'PENDING'") || (trimmed.includes('SYNC_STATUS = ?') && params[0] === 'Pending')) {
        return this.tables.offline_checkin_events.filter(e => e.sync_status === 'Pending') as any;
      }
      return [...this.tables.offline_checkin_events] as any;
    }
    if (trimmed.includes('FROM MANIFEST_TRIPS')) {
      return [...this.tables.manifest_trips] as any;
    }
    if (trimmed.includes('FROM MANIFEST_BOOKINGS')) {
      return [...this.tables.manifest_bookings] as any;
    }
    if (trimmed.includes('FROM MANIFEST_TICKETS')) {
      return [...this.tables.manifest_tickets] as any;
    }
    if (trimmed.includes('FROM PUBLIC_KEYS')) {
      // Phải lọc đúng theo WHERE thật của getActivePublicKey() - từng trả CẢ BẢNG không lọc, khiến
      // mọi lookup theo kid cụ thể (kể cả kid KHÔNG tồn tại) vẫn âm thầm trả về key đầu tiên trong
      // bảng, che mất chính lỗ hổng "kid không khớp vẫn được verify bằng key khác" mà regression
      // test ở scripts/verify-staff-scanner.ts cần bắt được (môi trường Node/test chỉ có fallback
      // in-memory này - không có SQLite thật để tự lọc đúng WHERE).
      let rows = [...this.tables.public_keys];
      if (trimmed.includes('KID = ?')) {
        const kid = params[0];
        rows = rows.filter((r) => r.kid === kid);
      }
      if (trimmed.includes('IS_ACTIVE = 1')) {
        rows = rows.filter((r) => r.is_active === 1);
      }
      if (trimmed.includes('ORDER BY VALID_FROM DESC')) {
        rows = rows.slice().sort((a, b) => String(b.valid_from).localeCompare(String(a.valid_from)));
      }
      return rows as any;
    }
    return [];
  }

  async getFirstAsync<T = any>(sql: string, params: any[] = []): Promise<T | null> {
    const rows = await this.getAllAsync<T>(sql, params);
    return rows.length > 0 ? rows[0] : null;
  }

  async withTransactionAsync<T>(callback: () => Promise<T>): Promise<T> {
    return await callback();
  }
}

let dbInstance: DatabaseAdapter | null = null;
let dbInitialization: Promise<DatabaseAdapter> | null = null;

export async function getDatabase(): Promise<DatabaseAdapter> {
  if (dbInstance) return dbInstance;
  if (dbInitialization) return dbInitialization;

  dbInitialization = initializeDatabase();
  try {
    dbInstance = await dbInitialization;
    return dbInstance;
  } finally {
    dbInitialization = null;
  }
}

async function initializeDatabase(): Promise<DatabaseAdapter> {
  if (isNodeTestEnvironment()) {
    return createInMemoryDatabase();
  }

  let platformOS: string;
  try {
    const { Platform } = await import('react-native');
    platformOS = Platform.OS;
  } catch (error) {
    throw new Error(
      `Không thể xác định nền tảng để khởi tạo SQLite offline; không sử dụng bộ nhớ tạm. Chi tiết: ${getErrorMessage(error)}`
    );
  }

  if (platformOS === 'web') {
    return createInMemoryDatabase();
  }
  if (platformOS !== 'android' && platformOS !== 'ios') {
    throw new Error(`SQLite offline không hỗ trợ nền tảng "${platformOS || 'không xác định'}".`);
  }

  let nativeDb: import('expo-sqlite').SQLiteDatabase;
  try {
    const SQLite = await import('./sqliteDriver');
    nativeDb = await SQLite.openDatabaseAsync(DB_NAME);
  } catch (error) {
    throw new Error(
      `Không thể mở cơ sở dữ liệu offline "${DB_NAME}". Ứng dụng sẽ không chuyển sang bộ nhớ tạm để tránh mất dữ liệu check-in. ` +
      `Chi tiết: ${getErrorMessage(error)}`
    );
  }

  const adapter: DatabaseAdapter = {
    execAsync: (sql: string) => nativeDb.execAsync(sql),
    runAsync: (sql: string, params?: any[]) => nativeDb.runAsync(sql, ...(params || [])),
    getAllAsync: <T = any>(sql: string, params?: any[]) => nativeDb.getAllAsync<T>(sql, ...(params || [])),
    getFirstAsync: <T = any>(sql: string, params?: any[]) => nativeDb.getFirstAsync<T>(sql, ...(params || [])),
    withTransactionAsync: async <T>(callback: () => Promise<T>): Promise<T> => {
      let result: T;
      await nativeDb.withTransactionAsync(async () => {
        result = await callback();
      });
      return result!;
    },
  };

  try {
    await initSchema(adapter);
    return adapter;
  } catch (error) {
    throw new Error(
      `Không thể nâng cấp cơ sở dữ liệu offline "${DB_NAME}". Dữ liệu hiện có được giữ nguyên; ` +
      `không xóa hoặc cài lại ứng dụng. Chi tiết: ${getErrorMessage(error)}`
    );
  }
}

async function createInMemoryDatabase(): Promise<DatabaseAdapter> {
  console.log('[SQLite] Running in Web/Test environment — using InMemory SQLite adapter');
  const fallback = new InMemoryFallbackDatabase();
  await initSchema(fallback);
  return fallback;
}

/**
 * Initialize database tables & indexes for ultra-fast lookups
 */
async function initSchema(db: DatabaseAdapter): Promise<void> {
  const schemaSql = `
    PRAGMA journal_mode = WAL;

    -- Bảng lưu Public Key ký số QR (BR-QR-02, BR-OFF-01)
    CREATE TABLE IF NOT EXISTS public_keys (
      kid TEXT PRIMARY KEY,
      algorithm TEXT NOT NULL,
      public_key_pem TEXT NOT NULL,
      valid_from TEXT,
      valid_to TEXT,
      is_active INTEGER NOT NULL DEFAULT 1
    );

    -- Bảng lưu manifest chuyến đi (Trip)
    CREATE TABLE IF NOT EXISTS manifest_trips (
      id TEXT PRIMARY KEY,
      trip_code TEXT NOT NULL,
      route_name TEXT NOT NULL,
      trip_type TEXT NOT NULL,
      boat_name TEXT NOT NULL,
      departure_station_id TEXT NOT NULL,
      departure_station_name TEXT NOT NULL,
      arrival_station_id TEXT NOT NULL,
      arrival_station_name TEXT NOT NULL,
      departure_time TEXT NOT NULL,
      arrival_time TEXT,
      sales_close_at TEXT,
      status TEXT NOT NULL
    );

    -- Bảng lưu manifest đơn đặt vé (Booking)
    CREATE TABLE IF NOT EXISTS manifest_bookings (
      id TEXT PRIMARY KEY,
      public_booking_id TEXT NOT NULL UNIQUE,
      booking_code TEXT NOT NULL,
      trip_id TEXT NOT NULL,
      customer_name TEXT NOT NULL,
      customer_phone TEXT,
      total_amount REAL NOT NULL,
      qr_credential_version INTEGER NOT NULL DEFAULT 1,
      status TEXT NOT NULL
    );

    -- Bảng lưu manifest từng vé (Ticket) thuộc Booking
    CREATE TABLE IF NOT EXISTS manifest_tickets (
      id TEXT PRIMARY KEY,
      booking_id TEXT NOT NULL,
      ticket_number TEXT NOT NULL UNIQUE,
      seat_code TEXT NOT NULL,
      seat_class TEXT NOT NULL,
      passenger_name TEXT NOT NULL,
      status TEXT NOT NULL,
      boarding_status TEXT NOT NULL DEFAULT 'NotCheckedIn',
      checked_in_at TEXT,
      checked_in_by_staff_id TEXT,
      face_fare REAL NOT NULL DEFAULT 0,
      paid_fare REAL NOT NULL DEFAULT 0,
      FOREIGN KEY (booking_id) REFERENCES manifest_bookings (id) ON DELETE CASCADE
    );

    -- Bảng lưu sự kiện Check-in ngoại tuyến chờ đồng bộ (BR-OFF-03, CheckInEvent)
    CREATE TABLE IF NOT EXISTS offline_checkin_events (
      id TEXT PRIMARY KEY,
      client_event_id TEXT NOT NULL UNIQUE,
      ticket_id TEXT NOT NULL,
      ticket_number TEXT NOT NULL,
      passenger_name TEXT NOT NULL,
      seat_code TEXT NOT NULL,
      booking_id TEXT NOT NULL,
      trip_id TEXT NOT NULL,
      trip_stop_call_id TEXT NOT NULL,
      staff_account_id TEXT NOT NULL,
      staff_name TEXT NOT NULL,
      scanner_device_id TEXT NOT NULL,
      occurred_at_device TEXT NOT NULL,
      received_at_server TEXT,
      outcome TEXT NOT NULL,
      sync_status TEXT NOT NULL DEFAULT 'Pending',
      retry_count INTEGER NOT NULL DEFAULT 0,
      last_sync_attempt_at TEXT,
      error_message TEXT,
      notes TEXT
    );

    -- Bảng lưu cấu hình & trạng thái thiết bị scanner
    CREATE TABLE IF NOT EXISTS scanner_config (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );

    -- CHỈ MỤC TỐC ĐỘ CAO CHO TRA CỨU TẠI BẾN (< 5ms)
    CREATE INDEX IF NOT EXISTS idx_booking_public_id ON manifest_bookings(public_booking_id);
    CREATE INDEX IF NOT EXISTS idx_ticket_booking_id ON manifest_tickets(booking_id);
    CREATE INDEX IF NOT EXISTS idx_ticket_number ON manifest_tickets(ticket_number);
    CREATE INDEX IF NOT EXISTS idx_ticket_boarding_status ON manifest_tickets(boarding_status);
    CREATE INDEX IF NOT EXISTS idx_checkin_sync_status ON offline_checkin_events(sync_status);
    CREATE INDEX IF NOT EXISTS idx_checkin_client_event_id ON offline_checkin_events(client_event_id);
  `;

  await db.execAsync(schemaSql);
  await migratePublicKeysSchema(db);
}

/**
 * Migrate known legacy public-key column names in place, preserving every key row.
 * The table swap runs transactionally; unsupported schemas fail without dropping the source.
 */
export async function migratePublicKeysSchema(db: DatabaseAdapter): Promise<void> {
  const columns = await db.getAllAsync<{ name: string; notnull: number }>('PRAGMA table_info(public_keys)');
  if (columns.length === 0) return;

  const columnNames = new Set(columns.map((column) => column.name));
  const currentColumns = ['kid', 'algorithm', 'public_key_pem', 'valid_from', 'valid_to', 'is_active'];
  const hasCurrentColumns = currentColumns.every((column) => columnNames.has(column));
  const hasRequiredValidityWindow = columns.some(
    (column) => (column.name === 'valid_from' || column.name === 'valid_to') && column.notnull === 1
  );
  if (hasCurrentColumns && !hasRequiredValidityWindow) return;

  const migrationTable = await db.getFirstAsync<{ name: string }>(
    `SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?`,
    [PUBLIC_KEYS_MIGRATION_TABLE]
  );
  if (migrationTable) {
    throw new Error(`Tồn tại bảng migration ${PUBLIC_KEYS_MIGRATION_TABLE}; cần kiểm tra thủ công trước khi tiếp tục.`);
  }

  const expressions: Record<string, string | null> = {
    kid: firstColumn(columnNames, ['kid', 'key_id', 'keyId', 'id']),
    algorithm: firstColumn(columnNames, ['algorithm', 'alg']) || "'RS256'",
    public_key_pem: firstColumn(columnNames, ['public_key_pem', 'publicKeyPem', 'public_key', 'publicKey', 'pem']),
    valid_from: firstColumn(columnNames, ['valid_from', 'validFrom']) || "'1970-01-01T00:00:00.000Z'",
    valid_to: firstColumn(columnNames, ['valid_to', 'validTo']) || "'9999-12-31T23:59:59.999Z'",
    is_active: firstColumn(columnNames, ['is_active', 'isActive', 'active']) || '1',
  };

  if (!expressions.kid || !expressions.public_key_pem) {
    throw new Error(
      'Schema public_keys không được hỗ trợ; cần có định danh khóa (kid/key_id/id) và nội dung public key.'
    );
  }

  await db.withTransactionAsync(async () => {
    await db.execAsync(`ALTER TABLE public_keys RENAME TO ${PUBLIC_KEYS_MIGRATION_TABLE}`);
    await db.execAsync(`
      CREATE TABLE public_keys (
        kid TEXT PRIMARY KEY,
        algorithm TEXT NOT NULL,
        public_key_pem TEXT NOT NULL,
        valid_from TEXT,
        valid_to TEXT,
        is_active INTEGER NOT NULL DEFAULT 1
      )
    `);
    await db.execAsync(`
      INSERT INTO public_keys (kid, algorithm, public_key_pem, valid_from, valid_to, is_active)
      SELECT ${expressions.kid}, ${expressions.algorithm}, ${expressions.public_key_pem},
             ${expressions.valid_from}, ${expressions.valid_to}, ${expressions.is_active}
      FROM ${PUBLIC_KEYS_MIGRATION_TABLE}
    `);
    await db.execAsync(`DROP TABLE ${PUBLIC_KEYS_MIGRATION_TABLE}`);
  });
}

function firstColumn(columnNames: Set<string>, candidates: string[]): string | null {
  const column = candidates.find((candidate) => columnNames.has(candidate));
  return column ? `"${column}"` : null;
}

function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
