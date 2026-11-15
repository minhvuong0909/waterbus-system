/**
 * Kiểm chứng nhiều chuyến tại một bến, chọn vé khi soát một phần, và giả lập ngoại tuyến.
 * Chạy trên SQLite THẬT (node:sqlite) với HTTP được mô phỏng - không dùng adapter bộ nhớ tự viết.
 *   pnpm test:scanner:multitrip
 */
import { AUTH_TOKEN_KEY } from '../src/services/api/apiClient';
import { setSecureItem } from '../src/services/auth/secureStorage';
import { QrVerifier, DEFAULT_KEY_INFO } from '../src/services/crypto/qrVerifier';
import { DatabaseAdapter, useDatabaseForTests } from '../src/services/sqlite/database';
import { SqliteRepository } from '../src/services/sqlite/sqliteRepository';
import { scannerCredentials } from '../src/services/checkin/scannerCredentials';
import { syncService } from '../src/services/sync/syncService';
import { useScannerStore } from '../src/store/useScannerStore';
import { useAuthStore } from '../src/store/useAuthStore';
import { eligibleTicketIds, selectedTicketIds, toggleAllTickets, toggleTicket } from '../src/screens/booking/ticketSelection';
import { formatVnTime } from '../src/utils/formatTime';
import type { ManifestBookingResponse, MyScannerAssignment, ScannerManifestResponse } from '../src/types/checkin';
import type { Ticket } from '../src/types/scanner';

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { DatabaseSync } = require('node:sqlite');

function createRealSqlite(): DatabaseAdapter {
  const db = new DatabaseSync(':memory:');
  let depth = 0;
  return {
    execAsync: async (sql) => { db.exec(sql); },
    runAsync: async (sql, params = []) => {
      const r = db.prepare(sql).run(...params);
      return { lastInsertRowId: Number(r.lastInsertRowid), changes: Number(r.changes) };
    },
    getAllAsync: async (sql, params = []) => db.prepare(sql).all(...params),
    getFirstAsync: async (sql, params = []) => db.prepare(sql).get(...params) ?? null,
    withTransactionAsync: async (callback) => {
      if (depth++ === 0) db.exec('BEGIN');
      try {
        const result = await callback();
        if (--depth === 0) db.exec('COMMIT');
        return result;
      } catch (error) {
        if (--depth === 0) db.exec('ROLLBACK');
        throw error;
      }
    },
  };
}

let passed = 0;
let total = 0;
function assert(condition: boolean, name: string, detail?: string) {
  total++;
  if (condition) { passed++; console.log(`  ✅ [PASS] ${name}`); }
  else console.error(`  ❌ [FAIL] ${name}${detail ? `\n     ↳ ${detail}` : ''}`);
}

const STAFF = 'staff-multi';
const identity = { deviceId: 'device-1', enrollmentId: 'enroll-1', deviceCode: 'GATE-1', stationId: 'station-A', stationName: 'Bến A' };
const now = Date.now();
const iso = (offsetMs: number) => new Date(now + offsetMs).toISOString();

const callOf = (n: number, departMin: number, trip: string): MyScannerAssignment => ({
  assignmentId: 'shift-1', tripStopCallId: `call-${n}`, tripId: trip, routeName: 'Tuyến X', stationName: 'Bến A',
  visitOrder: 1, tripStatus: 'Boarding', estimatedArrivalTime: null, deviceId: identity.deviceId, deviceCode: identity.deviceCode,
  isPrimaryOffline: false, activeFrom: iso(-3_600_000), activeUntil: iso(7_200_000),
  checkInOpenAt: iso(-600_000), checkInCloseAt: iso(1_800_000),
  tripDepartureTime: iso(departMin * 60_000), boatCode: `B0${n}`, boatName: `Tàu ${n}`,
});
// Trả danh sách theo thứ tự NGƯỢC giờ khởi hành để kiểm tra việc sắp xếp.
const serverCalls = [callOf(3, 90, 'trip-3'), callOf(2, 60, 'trip-2'), callOf(1, 30, 'trip-1')];

const booking = (id: string, trip: string, tickets: string[]): ManifestBookingResponse => ({
  bookingId: id, publicBookingId: `pub_${id}`, bookingCode: id.toUpperCase(), tripId: trip, customerName: id, customerPhone: null,
  quotedTotal: 0, qrCredentialVersion: 1, status: 'Confirmed',
  tickets: tickets.map((t) => ({ ticketId: t, ticketCode: `TC-${t}`, passengerName: t, seatCode: t, seatClass: 'Std', boardingStatus: 'NotCheckedIn', paidFare: 1 })),
});
const bookingsByCall: Record<string, ManifestBookingResponse[]> = {
  'call-1': [booking('b1', 'trip-1', ['t1a', 't1b']), booking('b1x', 'trip-1', ['t1x'])],
  'call-2': [booking('b2', 'trip-2', ['t2a'])],
  'call-3': [booking('b3', 'trip-3', ['t3a'])],
};
const manifestFor = (callId: string): ScannerManifestResponse => {
  const call = serverCalls.find((c) => c.tripStopCallId === callId)!;
  return {
    tripStopCallId: callId, stationName: 'Bến A', checkInOpenAt: call.checkInOpenAt, checkInCloseAt: call.checkInCloseAt,
    trip: { tripId: call.tripId, routeName: 'Tuyến X', boatName: call.boatName ?? '', departureStationName: 'A', arrivalStationName: 'B',
      departureTime: call.tripDepartureTime!, arrivalTime: null, salesCloseAt: null, tripType: 'Commuter', status: 'Boarding' },
    bookings: bookingsByCall[callId], generatedAt: new Date().toISOString(),
    // Chỉ call-1 là Primary Offline của máy này.
    offlineGrant: callId === 'call-1' ? { id: 'grant-1', activeFrom: iso(-3_600_000), activeUntil: iso(7_200_000) } : null,
  };
};

// ---- HTTP mô phỏng ----
const calls: { method: string; path: string; body?: any }[] = [];
const syncedByServer = new Map<string, number>();
const originalFetch = globalThis.fetch;
globalThis.fetch = (async (url: any, init?: any) => {
  const path = new URL(String(url)).pathname;
  const method = init?.method ?? 'GET';
  const body = init?.body ? JSON.parse(init.body) : undefined;
  calls.push({ method, path, body });
  const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
  if (path === '/api/v1/checkins/assignments/mine') return json(serverCalls);
  if (path.startsWith('/api/v1/checkins/manifest/')) return json(manifestFor(path.split('/').pop()!));
  if (path === '/api/v1/checkouts/qr-public-key')
    return json({ kid: DEFAULT_KEY_INFO.kid, algorithm: 'RS256', publicKeyPem: DEFAULT_KEY_INFO.publicKeyPem });
  if (path === '/api/v1/scanner/status')
    return json({ scanner: identity, shift: { id: 'shift-1', activeFrom: iso(-3_600_000), activeUntil: iso(7_200_000) } });
  if (path === '/api/v1/checkins/sync') {
    for (const e of body.events) syncedByServer.set(e.clientEventId, (syncedByServer.get(e.clientEventId) ?? 0) + 1);
    return json({ results: body.events.map((e: any) => ({ clientEventId: e.clientEventId, outcome: 'Success', wasAlreadySynced: false, error: null })) });
  }
  if (path === '/api/v1/checkins' && method === 'POST') return json({ outcome: 'Success', checkedInAt: new Date().toISOString() });
  return json({ title: 'not mocked' }, 404);
}) as typeof fetch;

const store = () => useScannerStore.getState();
const scan = async (pubId: string, tripId: string) => {
  const qr = QrVerifier.generateTestSignedQr(pubId.startsWith('pub_') ? pubId : `pub_${pubId}`, tripId, DEFAULT_KEY_INFO);
  const verification = await QrVerifier.verifyQrCode(qr.compactQr);
  return { verification, result: await store().openScannedBooking(verification.publicBookingId!, verification) };
};
const callsTo = (fragment: string) => calls.filter((c) => c.path.includes(fragment));

async function main() {
  console.log('📦 Multi-trip / partial check-in / simulated offline (SQLite thật)');
  await useDatabaseForTests(createRealSqlite());
  const payload = Buffer.from(JSON.stringify({ sub: STAFF, exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url');
  await setSecureItem(AUTH_TOKEN_KEY, `t.${payload}.t`);
  await scannerCredentials.save(identity, 'credential-1');
  syncService.setDeviceInfo({ ...identity, staffId: STAFF, staffName: 'Nhân viên', forceOfflineMode: false });
  useScannerStore.setState({ deviceInfo: syncService.getDeviceInfo() });

  // ---- Issue 2: định dạng giờ VN ----
  assert(formatVnTime('2026-11-16T00:30:00Z') === '07:30', 'Giờ hiển thị theo Asia/Ho_Chi_Minh', formatVnTime('2026-11-16T00:30:00Z'));

  // ---- Issue 3: tải một lần nhiều chuyến ----
  await store().preloadCache('call-1');
  assert(store().cachedAssignments.length === 1, 'Tải riêng một chuyến vẫn hoạt động');
  const all = await store().preloadAll();
  assert(all.loaded === 3 && all.total === 3, 'preloadAll tải đủ 3 chuyến tại bến', JSON.stringify(all));
  assert(store().cachedAssignments.map((a) => a.tripStopCallId).join() === 'call-1,call-2,call-3',
    'Các chuyến đã tải được sắp theo giờ khởi hành');
  const stats = await SqliteRepository.getManifestStats();
  assert(stats.tripCount === 3 && stats.bookingCount === 4, 'Nhiều manifest cùng tồn tại trong SQLite, không bị thay thế', JSON.stringify(stats));
  const ctxs = await scannerCredentials.cacheList();
  assert(ctxs.find((c) => c.assignment.tripStopCallId === 'call-1')?.assignment.isPrimaryOffline === true &&
    ctxs.find((c) => c.assignment.tripStopCallId === 'call-2')?.assignment.isPrimaryOffline === false,
    'Quyền Primary Offline được giữ riêng theo từng điểm dừng');

  // ---- QR chuyến A rồi chuyến B, không chọn lại chuyến ----
  let s = await scan('b1', 'trip-1');
  assert(s.result.status === 'opened' && store().activeAssignment?.tripStopCallId === 'call-1', 'QR chuyến 07:30 tự định tuyến tới call-1');
  store().closeBookingModal();
  s = await scan('b2', 'trip-2');
  assert(s.result.status === 'opened' && store().activeAssignment?.tripStopCallId === 'call-2',
    'QR chuyến kế tiếp quét thẳng, không cần chọn chuyến/tải lại');
  store().closeBookingModal();

  // ---- Từ chối ----
  const manifestCallsBefore = callsTo('/manifest/').length;
  const forged = await scan('b-unknown', 'trip-unknown');
  assert(forged.result.status === 'notCached' && callsTo('/manifest/').length === manifestCallsBefore,
    'TripId của QR không tự cấp quyền: chuyến không được phân công không tải được manifest');
  await SqliteRepository.saveManifest({ stationId: 'x', stationName: 'x', generatedAt: iso(0), publicKey: DEFAULT_KEY_INFO,
    trips: [], bookings: [{ id: 'b9', publicBookingId: 'pub_b9', bookingCode: 'B9', tripId: 'trip-9', customerName: 'x', totalAmount: 0,
      qrCredentialVersion: 1, status: 'Confirmed', tickets: [{ id: 't9', bookingId: 'b9', ticketNumber: 'TC-t9', seatCode: '1', seatClass: 'S',
        passengerName: 'x', status: 'Valid', boardingStatus: 'NotCheckedIn', faceFare: 1, paidFare: 1 }] }] });
  const other = await scan('b9', 'trip-9');
  assert(other.result.status === 'notAuthorized', 'Đơn có trong máy nhưng chuyến chưa được cấp quyền bị từ chối');

  // ---- Tải theo yêu cầu cho chuyến đủ điều kiện nhưng chưa tải ----
  await scannerCredentials.removeCache('call-3');
  await SqliteRepository.clearManifest();
  await store().preloadCache('call-1');
  const onDemand = await scan('b3', 'trip-3');
  assert(onDemand.result.status === 'opened' && store().activeAssignment?.tripStopCallId === 'call-3',
    'Chuyến chưa tải được tải theo yêu cầu (online, máy chủ kiểm tra phân công)');
  store().closeBookingModal();

  // ---- Issue 4: soát một phần rồi soát nốt ----
  await scan('b1', 'trip-1');
  let b = store().currentBooking!;
  assert(selectedTicketIds(b.tickets, []).join() === 't1a,t1b', 'Mặc định chọn mọi vé đủ điều kiện');
  const r1 = await store().performCheckIn(['t1a']);
  assert(r1.success && r1.count === 1, 'Soát hành khách A (online)', JSON.stringify(r1));
  store().closeBookingModal();
  await scan('b1', 'trip-1');
  b = store().currentBooking!;
  const first = b.tickets.find((t: Ticket) => t.id === 't1a')!;
  assert(first.boardingStatus === 'CheckedIn' && selectedTicketIds(b.tickets, []).join() === 't1b',
    'Quét lại: A giữ CheckedIn, chỉ B được chọn (đếm = 1)');
  const before = callsTo('/api/v1/checkins').filter((c) => c.method === 'POST' && !c.path.endsWith('/sync')).length;
  const r2 = await store().performCheckIn(selectedTicketIds(b.tickets, []));
  const posts = callsTo('/api/v1/checkins').filter((c) => c.method === 'POST' && !c.path.endsWith('/sync'));
  assert(r2.success && posts.length === before + 1 && posts.at(-1)?.body.ticketId === 't1b',
    'Lần xác nhận thứ hai gửi đúng 1 vé (B), không cần tải lại manifest');
  assert(!(await store().performCheckIn(['t1a'])).success, 'Backend/Store vẫn từ chối gửi lại vé đã check-in');
  store().closeBookingModal();

  // Logic chọn vé (thuần)
  const mk = (id: string, st: 'NotCheckedIn' | 'CheckedIn'): Ticket => ({ id, bookingId: 'b', ticketNumber: id, seatCode: id, seatClass: 's',
    passengerName: id, status: 'Valid', boardingStatus: st, faceFare: 1, paidFare: 1 });
  const part = [mk('A', 'CheckedIn'), mk('B', 'NotCheckedIn'), mk('C', 'NotCheckedIn')];
  assert(eligibleTicketIds(part).join() === 'B,C', 'Chỉ vé NotCheckedIn đủ điều kiện chọn');
  let ex: string[] = [];
  ex = toggleTicket(part, ex, 'A');
  assert(ex.length === 0, 'Không thể chọn/bỏ vé đã CheckedIn');
  ex = toggleTicket(part, ex, 'B');
  assert(selectedTicketIds(part, ex).join() === 'C', 'Bỏ chọn một vé');
  ex = toggleTicket(part, ex, 'B');
  assert(selectedTicketIds(part, ex).join() === 'B,C', 'Chọn lại vé đã bỏ');
  ex = toggleAllTickets(part, []);
  assert(selectedTicketIds(part, ex).length === 0, 'Chọn tất cả khi đang chọn hết ⇒ bỏ chọn');
  ex = toggleAllTickets(part, ex);
  assert(selectedTicketIds(part, ex).join() === 'B,C', 'Chọn tất cả sau khi bỏ chọn ⇒ chỉ vé đủ điều kiện (không có A)');
  assert(selectedTicketIds([mk('A', 'CheckedIn'), mk('B', 'CheckedIn')], []).length === 0, 'Mọi vé đã check-in ⇒ không chọn vé nào');
  const stale = [mk('A', 'NotCheckedIn'), mk('B', 'NotCheckedIn')];
  const afterCheckIn = [mk('A', 'CheckedIn'), mk('B', 'NotCheckedIn')];
  assert(selectedTicketIds(stale, []).join() === 'A,B' && selectedTicketIds(afterCheckIn, []).join() === 'B',
    'Trạng thái vé đổi ⇒ lựa chọn tự đối soát (không giữ vé cũ)');

  // ---- Issue 5: giả lập offline ----
  await store().preloadAll();
  await scan('b1x', 'trip-1');
  const apiBeforeOffline = calls.length;
  await store().setSimulatedOffline(true);
  await store().loadPairing();
  assert(calls.length === apiBeforeOffline && store().cachedAssignments.length === 3 && store().pairingStatus !== 'Invalid',
    'Giả lập offline: loadPairing không gọi API và không làm mất phiên cục bộ hợp lệ');
  assert(!(await store().preloadCache('call-2')) && calls.length === apiBeforeOffline, 'Giả lập offline: không tải manifest từ API');
  const off = await store().performCheckIn(['t1x']);
  assert(off.success && off.queued === true, 'Check-in offline tại điểm dừng Primary với grant hợp lệ → xếp hàng SQLite');
  assert(calls.length === apiBeforeOffline, 'Check-in offline không gọi endpoint check-in trực tuyến');
  assert((await SqliteRepository.countQueue(STAFF, 'Pending')) === 1 && store().stats.pendingSyncCount === 1, 'Đếm sự kiện chờ đồng bộ = 1');
  store().closeBookingModal();
  await scan('b2', 'trip-2');
  const denied = await store().performCheckIn(['t2a']);
  assert(!denied.success && (await SqliteRepository.countQueue(STAFF, 'Pending')) === 1 && calls.length === apiBeforeOffline,
    'Offline tại điểm dừng KHÔNG có Primary/grant bị từ chối, không tự nâng quyền');
  store().closeBookingModal();

  // Bảo vệ trạng thái cục bộ khi nạp lại manifest cũ (vé chờ đồng bộ)
  syncService.setDeviceInfo({ forceOfflineMode: false });
  await store().preloadCache('call-1'); // máy chủ vẫn báo t1x NotCheckedIn (cũ)
  const protectedBooking = await SqliteRepository.getBookingByPublicId('pub_b1x');
  assert(protectedBooking?.tickets[0].boardingStatus === 'CheckedIn', 'Nạp lại manifest không ghi đè vé đang chờ đồng bộ bằng trạng thái cũ');
  syncService.setDeviceInfo({ forceOfflineMode: true });

  // Quay lại online ⇒ đồng bộ lô đúng một lần
  await store().setSimulatedOffline(false);
  assert((await SqliteRepository.countQueue(STAFF, 'Pending')) === 0 && syncedByServer.size === 1 &&
    [...syncedByServer.values()].every((n) => n === 1), 'Trở lại online: đồng bộ lô thành công, mỗi sự kiện gửi đúng 1 lần');
  const syncCalls = callsTo('/checkins/sync').length;
  const again = await syncService.triggerBatchSync(true);
  assert(again.syncedCount === 0 && callsTo('/checkins/sync').length === syncCalls && [...syncedByServer.values()].every((n) => n === 1),
    'Đồng bộ lặp lại không gửi trùng sự kiện');

  // ---- Đặt lại khi đăng xuất ----
  await store().setSimulatedOffline(true);
  await useAuthStore.getState().logout();
  assert(syncService.getDeviceInfo().forceOfflineMode === false && store().deviceInfo.forceOfflineMode === false,
    'Đăng xuất tắt chế độ giả lập offline (mặc định tắt)');

  globalThis.fetch = originalFetch;
  console.log(`\n🎯 MULTI-TRIP VERIFICATION: ${passed}/${total} tests passed`);
  process.exit(passed === total ? 0 : 1);
}

main().catch((error) => { console.error('Fatal test error:', error); process.exit(1); });
