~/**
 * Script test thủ công cho 3 bug fixes: M13, H03, M08
 * Chạy: npm run test:scanner (chạy cả 23 tests) hoặc chạy riêng file này
 */
import CryptoJS from 'crypto-js';
import { QrVerifier, DEFAULT_KEY_INFO } from '../src/services/crypto/qrVerifier';
import { SqliteRepository } from '../src/services/sqlite/sqliteRepository';
import { syncService } from '../src/services/sync/syncService';

declare const process: any;

async function manualTests() {
  let pass = 0;
  let total = 0;

  function check(ok: boolean, name: string, detail?: string) {
    total++;
    if (ok) { pass++; console.log(`  ✅ ${name}`); }
    else { console.error(`  ❌ ${name}`); }
    if (detail) console.log(`     → ${detail}`);
  }

  // ══════════════════════════════════════════
  // TEST M13: kid đọc từ JWS header
  // ══════════════════════════════════════════
  console.log('\n📌 TEST M13 — QR Scanner đọc kid từ JWS header (RFC 7515)\n');

  // Tạo JWS QR mà kid CHỈ nằm ở header, KHÔNG có trong payload
  const header = { alg: 'HS256', typ: 'JWT', kid: 'waterbus-root-2026' };
  const payload = { pbid: 'pub_wb_test_m13', ver: 1, tid: 'TRIP-01', iat: Math.floor(Date.now() / 1000) };
  
  const hB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(header)));
  const pB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(payload)));
  const secret = DEFAULT_KEY_INFO.publicKeyPem;
  const sig = CryptoJS.HmacSHA256(`${hB64}.${pB64}`, secret).toString(CryptoJS.enc.Base64url);
  const jwsQr = `${hB64}.${pB64}.${sig}`;

  console.log(`  JWS QR (kid chỉ ở header): ${jwsQr.substring(0, 60)}...`);

  const m13Result = await QrVerifier.verifyQrCode(jwsQr);
  check(m13Result.isValid === true, 'M13-1: QR hợp lệ dù kid không có trong payload', `isValid=${m13Result.isValid}`);
  check(m13Result.kid === 'waterbus-root-2026', 'M13-2: kid đọc đúng từ JWS header', `kid="${m13Result.kid}"`);
  check(m13Result.publicBookingId === 'pub_wb_test_m13', 'M13-3: publicBookingId parse đúng', `pbid="${m13Result.publicBookingId}"`);

  // Test: JWS có kid cả ở header và payload → header thắng
  const payload2 = { pbid: 'pub_wb_test_m13b', ver: 1, kid: 'old-kid-from-payload', iat: Math.floor(Date.now() / 1000) };
  const pB64_2 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(payload2)));
  const sig2 = CryptoJS.HmacSHA256(`${hB64}.${pB64_2}`, secret).toString(CryptoJS.enc.Base64url);
  const jwsQr2 = `${hB64}.${pB64_2}.${sig2}`;

  const m13Result2 = await QrVerifier.verifyQrCode(jwsQr2);
  check(
    m13Result2.kid === 'waterbus-root-2026',
    'M13-4: Header kid ưu tiên hơn payload kid',
    `header.kid="waterbus-root-2026" > payload.kid="old-kid-from-payload" → result.kid="${m13Result2.kid}"`
  );

  // ══════════════════════════════════════════
  // TEST H03: Late sync rejection
  // ══════════════════════════════════════════
  console.log('\n📌 TEST H03 — Offline check-in đồng bộ trễ bị từ chối\n');

  // Preload manifest trước để có ticket trong SQLite
  await syncService.preloadManifest('ST-BACH-DANG');
  const stats = await SqliteRepository.getManifestStats();
  console.log(`  (Đã preload: ${stats.ticketCount} vé, ${stats.bookingCount} booking)\n`);

  // Lấy booking có sẵn để check-in
  const sampleBooking = await SqliteRepository.getBookingByPublicId('pub_wb_98a76b5c');
  if (!sampleBooking || sampleBooking.tickets.length === 0) {
    check(false, 'H03-0: Không tìm thấy booking mẫu để test', 'Cần preload manifest trước');
  } else {
    // Tìm ticket chưa check-in
    const uncheckedTicket = sampleBooking.tickets.find((t: any) => t.boardingStatus !== 'CheckedIn');
    if (!uncheckedTicket) {
      check(false, 'H03-0: Không còn vé chưa check-in', 'Tất cả vé đã checked in');
    } else {
      await SqliteRepository.checkInTickets({
        ticketIds: [uncheckedTicket.id],
        bookingId: sampleBooking.id,
        tripId: sampleBooking.tripId,
        tripStopCallId: 'TSC-H03',
        staffAccountId: 'STAFF-H03',
        staffName: 'Staff H03 Test',
        scannerDeviceId: 'DEV-H03',
      });

      const pendingBefore = await SqliteRepository.getPendingEvents();
      const h03Event = pendingBefore.find((e: any) => e.staffAccountId === 'STAFF-H03');
      check(!!h03Event, 'H03-1: Event check-in tạo thành công và pending', `clientEventId="${h03Event?.clientEventId}"`);

      if (h03Event) {
        // Server reject vì đồng bộ trễ
        await SqliteRepository.markEventConflict(
          h03Event.clientEventId,
          'Đồng bộ trễ bị từ chối do quá giờ quy định (window closed)',
          true
        );

        const pendingAfter = await SqliteRepository.getPendingEvents();
        const stillPending = pendingAfter.find((e: any) => e.clientEventId === h03Event.clientEventId);
        check(!stillPending, 'H03-2: Event trễ bị loại khỏi pending queue (sync_status=Conflict)', `còn pending=${!!stillPending}`);
      }
    }
  }

  // ══════════════════════════════════════════
  // TEST M08: Primary Scanner race condition
  // ══════════════════════════════════════════
  console.log('\n📌 TEST M08 — Race condition Primary Scanner\n');

  // Test 1: Offline fallback (không có server)
  const m08Offline = await syncService.claimPrimaryOfflineRole('TSC-M08-TEST', 'http://localhost:19999');
  check(m08Offline.success === true, 'M08-1: Offline fallback trả success=true', `msg="${m08Offline.message}"`);
  check(typeof m08Offline.isPrimary === 'boolean', 'M08-2: isPrimary là boolean', `isPrimary=${m08Offline.isPrimary}`);

  // Test 2: Set isPrimary=true rồi claim offline → giữ trạng thái
  syncService.setDeviceInfo({ isPrimaryOffline: true });
  const m08Offline2 = await syncService.claimPrimaryOfflineRole('TSC-M08-TEST-2', 'http://localhost:19999');
  check(m08Offline2.isPrimary === true, 'M08-3: Offline giữ nguyên isPrimary=true khi đã set', `isPrimary=${m08Offline2.isPrimary}`);

  // Reset lại
  syncService.setDeviceInfo({ isPrimaryOffline: false });

  // ══════════════════════════════════════════
  // KẾT QUẢ
  // ══════════════════════════════════════════
  console.log(`\n${'═'.repeat(50)}`);
  console.log(`📊 KẾT QUẢ TEST THỦ CÔNG: ${pass}/${total} passed`);
  console.log(`${'═'.repeat(50)}\n`);

  process.exit(pass === total ? 0 : 1);
}

manualTests().catch((err: any) => {
  console.error('Lỗi:', err);
  process.exit(1);
});
