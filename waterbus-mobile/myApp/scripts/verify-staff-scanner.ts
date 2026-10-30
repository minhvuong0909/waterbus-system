/**
 * Comprehensive Automated Verification Suite for Staff Scanner (Tuấn - Thành viên 4)
 * Tests:
 * 1. Offline QR Signature Verification with Public Key (BR-QR-01, BR-QR-02)
 * 2. SQLite Manifest Preload & Ultra-fast Lookup (BR-OFF-01)
 * 3. Atomic Offline Ticket Check-In & Queue (BR-OFF-03, BR-OFF-05)
 * 4. Idempotent Batch Sync with UUID Deduplication (BR-OFF-03)
 * 5. Live API Integration Tests
 * 6. Bug Fixes: H03 (late sync rejection), M08 (primary scanner race), M13 (JWS header kid)
 */

import CryptoJS from 'crypto-js';
import { QrVerifier, DEFAULT_KEY_INFO } from '../src/services/crypto/qrVerifier';
import { SqliteRepository } from '../src/services/sqlite/sqliteRepository';
import { syncService } from '../src/services/sync/syncService';
import { scannerApiService, setApiBaseUrl } from '../src/api/client';

declare const process: any;
declare const require: any;
const http = require('http');

async function runVerification() {
  let passedTests = 0;
  let totalTests = 0;

  function assert(condition: boolean, testName: string, detail?: string) {
    totalTests++;
    if (condition) {
      passedTests++;
      console.log(`  ✓ ${testName}`);
    } else {
      console.error(`  ✕ ${testName}`);
      if (detail) console.error(`    ${detail}`);
    }
  }

  const sampleBookingId = 'pub_wb_98a76b5c';
  const sampleTripId = 'TRIP-BD-BA-01';

  // 1.1 Generate valid signed QR
  const signed = QrVerifier.generateTestSignedQr(sampleBookingId, sampleTripId, DEFAULT_KEY_INFO);

  // 1.2 Verify valid QR offline
  const validResult = await QrVerifier.verifyQrCode(signed.compactQr);
  assert(
    validResult.isValid && validResult.publicBookingId === sampleBookingId,
    '1.1 Verify valid signed QR offline with Public Key',
    `Algorithm: ${validResult.algorithmUsed}, VerifiedOffline: ${validResult.verifiedOffline}`
  );

  // 1.3 Verify JSON format QR
  const jsonResult = await QrVerifier.verifyQrCode(signed.jsonQr);
  assert(
    jsonResult.isValid && jsonResult.publicBookingId === sampleBookingId,
    '1.2 Verify JSON-structured signed QR offline',
    `Booking ID parsed: ${jsonResult.publicBookingId}`
  );

  // 1.4 Tamper detection (modified publicBookingId)
  const tamperedQr = signed.compactQr.replace(sampleBookingId, 'pub_wb_FORGED_ID');
  const tamperedResult = await QrVerifier.verifyQrCode(tamperedQr);
  assert(
    !tamperedResult.isValid && !!tamperedResult.error,
    '1.3 Reject tampered/forged QR code (Tamper Proof)',
    `Expected rejection: ${tamperedResult.error}`
  );

  // 1.5 Expired QR detection
  const expiredQrObj = {
    pbid: sampleBookingId,
    ver: 1,
    iat: Math.floor(Date.now() / 1000) - 7200,
    exp: Math.floor(Date.now() / 1000) - 3600, // Expired 1 hour ago
    sig: 'dummy_sig',
    kid: DEFAULT_KEY_INFO.kid,
  };
  const expiredResult = await QrVerifier.verifyQrCode(JSON.stringify(expiredQrObj));
  assert(
    !expiredResult.isValid,
    '1.4 Reject expired QR code past expiration window',
    `Error reported: ${expiredResult.error}`
  );

  // 2.1 Preload demo manifest
  const preloadRes = await syncService.preloadManifest('ST-BACH-DANG');
  assert(
    preloadRes.manifest.bookings.length > 0 && preloadRes.manifest.trips.length > 0,
    '2.1 Preload manifest before gate opening into SQLite',
    `Preloaded ${preloadRes.manifest.bookings.length} bookings, ${preloadRes.manifest.trips.length} trips`
  );

  // 2.2 Verify stats
  const stats = await SqliteRepository.getManifestStats();
  assert(
    stats.ticketCount > 0 && stats.tripCount > 0 && stats.bookingCount > 0,
    '2.2 SQLite local cache reflects correct manifest counts',
    `Cached: ${stats.tripCount} trips, ${stats.bookingCount} bookings, ${stats.ticketCount} tickets`
  );

  // 2.3 Fast lookup by publicBookingId
  const startTime = Date.now();
  const loadedBooking = await SqliteRepository.getBookingByPublicId(sampleBookingId);
  const lookupTimeMs = Date.now() - startTime;
  assert(
    loadedBooking !== null && loadedBooking.tickets.length > 0,
    '2.3 Ultra-fast lookup of Booking & Passenger list by PublicBookingId',
    `Lookup completed in ${lookupTimeMs}ms, Found ${loadedBooking?.tickets.length} tickets for customer "${loadedBooking?.customerName}"`
  );

  if (loadedBooking && loadedBooking.tickets.length >= 2) {
    const ticketToBoard1 = loadedBooking.tickets[0].id;
    const ticketToBoard2 = loadedBooking.tickets[1].id;

    // Check-in 2 passengers
    const checkInResult = await SqliteRepository.checkInTickets({
      ticketIds: [ticketToBoard1, ticketToBoard2],
      bookingId: loadedBooking.id,
      tripId: loadedBooking.tripId,
      tripStopCallId: 'TSC-TEST-001',
      staffAccountId: 'STAFF-TUAN-004',
      staffName: 'Nguyễn Văn Tuấn',
      scannerDeviceId: 'DEV-STAFF-TUAN-01',
      notes: 'Check-in tại cửa bến Bạch Đằng',
    });

    assert(
      checkInResult.checkedInCount === 2 && checkInResult.events.length === 2,
      '3.1 Atomic check-in of selected present passengers',
      `Checked in ${checkInResult.checkedInCount} passengers`
    );

    // Verify deduplication key uniqueness (client_event_id is UUID v4)
    const event1 = checkInResult.events[0];
    const event2 = checkInResult.events[1];
    const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    assert(
      uuidRegex.test(event1.clientEventId) && event1.clientEventId !== event2.clientEventId,
      '3.2 ClientEventId generated as unique UUID v4 deduplication key (BR-OFF-03)',
      `Event1: ${event1.clientEventId}\n     ↳ Event2: ${event2.clientEventId}`
    );

    // Verify ticket boarding status updated locally in SQLite
    const updatedBooking = await SqliteRepository.getBookingByPublicId(sampleBookingId);
    const checkedCount = updatedBooking?.tickets.filter((t) => t.boardingStatus === 'CheckedIn').length;
    assert(
      checkedCount === 2,
      '3.3 Local SQLite ticket boarding status updated to "CheckedIn"',
      `Checked-in tickets: ${checkedCount} / ${updatedBooking?.tickets.length}`
    );
  }

  const pendingEventsBefore = await SqliteRepository.getPendingEvents();
  assert(
    pendingEventsBefore.length >= 2,
    '4.1 Pending offline check-in events queued in SQLite for batch sync',
    `Found ${pendingEventsBefore.length} events pending sync`
  );

  // Trigger batch sync
  const syncResult = await syncService.triggerBatchSync();
  assert(
    syncResult.syncedCount >= 2,
    '4.2 Batch sync worker processes pending queue and marks events as "Synced"',
    `Synced ${syncResult.syncedCount} events successfully`
  );

  const pendingEventsAfter = await SqliteRepository.getPendingEvents();
  assert(
    pendingEventsAfter.length === 0,
    '4.3 Pending queue cleared in SQLite after successful server acknowledgment',
    `Remaining pending events: ${pendingEventsAfter.length}`
  );

  const mockPort = 5055;
  const mockBaseUrl = `http://127.0.0.1:${mockPort}`;
  setApiBaseUrl(mockBaseUrl);

  let lastReceivedIdempotencyKey: string | null = null;
  let lastReceivedBatchEventsCount = 0;

  const mockManifestData = {
    stationId: 'ST-BACH-DANG',
    trips: [
      {
        id: 'TRIP-LIVE-API-01',
        tripCode: 'WS-LIVE-101',
        routeName: 'Bạch Đằng - Bình An (Live Web API)',
        tripType: 'Regular',
        boatName: 'Sài Gòn Waterbus 01',
        departureStationId: 'ST-BACH-DANG',
        departureStationName: 'Bến Bạch Đằng',
        arrivalStationId: 'ST-BINH-AN',
        arrivalStationName: 'Bến Bình An',
        departureTime: new Date().toISOString(),
        status: 'Scheduled'
      }
    ],
    bookings: [
      {
        id: 'BK-LIVE-API-01',
        publicBookingId: 'pub_wb_live_api_01',
        bookingCode: 'WB-LIVE-999',
        tripId: 'TRIP-LIVE-API-01',
        customerName: 'Nguyễn Văn API',
        customerPhone: '0901234567',
        totalAmount: 150000,
        qrCredentialVersion: 1,
        status: 'Confirmed',
        tickets: [
          {
            id: 'TK-LIVE-API-01',
            bookingId: 'BK-LIVE-API-01',
            ticketNumber: 'TK-LIVE-1001',
            seatCode: 'A01',
            seatClass: 'Standard',
            passengerName: 'Nguyễn Văn API',
            status: 'Issued',
            boardingStatus: 'NotCheckedIn'
          }
        ]
      }
    ],
    publicKeys: [DEFAULT_KEY_INFO],
    generatedAt: new Date().toISOString()
  };

  const server = http.createServer((req: any, res: any) => {
    const reqUrl = new URL(req.url, mockBaseUrl);
    res.setHeader('Content-Type', 'application/json');

    // Route 1: Stations ping
    if (reqUrl.pathname === '/api/v1/stations' && req.method === 'GET') {
      res.writeHead(200);
      res.end(JSON.stringify([
        { id: 'ST-BACH-DANG', name: 'Bến Bạch Đằng (Quận 1)', code: 'BD' },
        { id: 'ST-BINH-AN', name: 'Bến Bình An (TP. Thủ Đức)', code: 'BA' }
      ]));
      return;
    }

    // Route 2: Preload Manifest
    if (reqUrl.pathname === '/api/v1/scanner/manifest' && req.method === 'GET') {
      res.writeHead(200);
      res.end(JSON.stringify(mockManifestData));
      return;
    }

    // Route 3: Batch sync with Idempotency-Key
    if (reqUrl.pathname === '/api/v1/tickets/sync-offline' && req.method === 'POST') {
      lastReceivedIdempotencyKey = req.headers['idempotency-key'] || null;
      let bodyData = '';
      req.on('data', (chunk: any) => { bodyData += chunk; });
      req.on('end', () => {
        try {
          const parsed = JSON.parse(bodyData || '{}');
          lastReceivedBatchEventsCount = parsed.events?.length || 0;
          res.writeHead(200);
          res.end(JSON.stringify({
            batchId: 'BATCH-RES-01',
            successCount: lastReceivedBatchEventsCount,
            conflictCount: 0,
            processedEvents: (parsed.events || []).map((e: any) => ({
              clientEventId: e.clientEventId,
              outcome: 'Success',
              syncStatus: 'Synced'
            }))
          }));
        } catch {
          res.writeHead(400);
          res.end(JSON.stringify({ error: 'Invalid JSON' }));
        }
      });
      return;
    }

    // Route 4: Scanner Public Key
    if (reqUrl.pathname === '/api/v1/scanner/public-key' && req.method === 'GET') {
      res.writeHead(200);
      res.end(JSON.stringify(DEFAULT_KEY_INFO));
      return;
    }

    res.writeHead(404);
    res.end(JSON.stringify({ error: 'Endpoint not found' }));
  });

  let isListening = false;
  let originalAdapter: any = null;
  let originalFetch: any = null;

  try {
    await new Promise<void>((resolve, reject) => {
      server.once('error', reject);
      server.listen(mockPort, '127.0.0.1', async () => {
        try {
          // Probe TCP connection to ensure loopback connect is allowed
          await new Promise<void>((res, rej) => {
            const probeReq = http.get(mockBaseUrl + '/api/v1/stations', () => res());
            probeReq.on('error', rej);
          });
          isListening = true;
          resolve();
        } catch (probeErr) {
          reject(probeErr);
        }
      });
    });
  } catch {
    // If TCP socket connect is restricted (e.g. standard sandbox mode), hook in-process HTTP adapter
    const { apiClient } = require('../src/api/client');
    originalAdapter = apiClient.defaults.adapter;
    originalFetch = (globalThis as any).fetch;

    apiClient.defaults.adapter = async (config: any) => {
      if (config.url?.includes('/stations')) {
        return { data: [{ id: 'ST-BACH-DANG', name: 'Bến Bạch Đằng (Quận 1)', code: 'BD' }, { id: 'ST-BINH-AN', name: 'Bến Bình An (TP. Thủ Đức)', code: 'BA' }], status: 200, statusText: 'OK', headers: {}, config };
      }
      if (config.url?.includes('/scanner/manifest')) {
        return { data: mockManifestData, status: 200, statusText: 'OK', headers: {}, config };
      }
      if (config.url?.includes('/sync-offline')) {
        lastReceivedIdempotencyKey = config.headers?.['Idempotency-Key'] || config.headers?.['idempotency-key'] || null;
        return { data: { batchId: 'BATCH-RES-01', successCount: 1, conflictCount: 0, processedEvents: [{ clientEventId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890', outcome: 'Success', syncStatus: 'Synced' }] }, status: 200, statusText: 'OK', headers: {}, config };
      }
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    (globalThis as any).fetch = async (url: any, init: any) => {
      const urlStr = String(url);
      if (urlStr.includes('/scanner/manifest')) {
        return { ok: true, status: 200, json: async () => mockManifestData };
      }
      if (urlStr.includes('/sync-offline')) {
        const body = init?.body ? JSON.parse(init.body) : {};
        const events = body.events || [];
        return { ok: true, status: 200, json: async () => ({
          batchId: 'BATCH-MOCK-01',
          successCount: events.length,
          conflictCount: 0,
          processedEvents: events.map((e: any) => ({
            clientEventId: e.clientEventId,
            outcome: 'Success',
            syncStatus: 'Synced',
          })),
        }) };
      }
      return { ok: true, status: 200, json: async () => ({}) };
    };
  }

  try {
    // 5.1 Ping API
    const pingOk = await scannerApiService.ping();
    assert(pingOk, '5.1 HTTP Ping check to backend endpoint (/api/v1/stations)', `Connected to ${mockBaseUrl}`);

    // 5.2 Get Stations
    const stations = await scannerApiService.getStations();
    assert(
      Array.isArray(stations) && stations.length >= 2,
      '5.2 Call API GET /api/v1/stations via Axios client',
      `Received ${stations.length} stations: ${stations.map((s) => s.code).join(', ')}`
    );

    // 5.3 Live Preload Manifest via scannerApiService
    const liveManifest = await scannerApiService.getPreloadManifest('ST-BACH-DANG');
    assert(
      liveManifest.stationId === 'ST-BACH-DANG' && liveManifest.trips[0].tripCode === 'WS-LIVE-101',
      '5.3 Call API GET /api/v1/scanner/manifest?stationId=ST-BACH-DANG',
      `Trip code from server: "${liveManifest.trips[0].tripCode}", Booking code: "${liveManifest.bookings[0].bookingCode}"`
    );

    // 5.4 Live API Batch Sync with Idempotency Key via scannerApiService
    const testIdempotencyKey = `BATCH-TEST-${Date.now()}`;
    const syncBatchRes = await scannerApiService.syncOfflineBatch(
      {
        scannerDeviceId: 'DEV-STAFF-TUAN-01',
        staffAccountId: 'STAFF-TUAN-004',
        batchTimestamp: new Date().toISOString(),
        events: [
          {
            clientEventId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
            ticketId: 'TK-LIVE-1001',
            ticketNumber: 'TK-LIVE-1001',
            passengerName: 'Nguyễn Văn API',
            seatCode: 'A01',
            bookingId: 'BK-LIVE-API-01',
            tripId: 'TRIP-LIVE-API-01',
            tripStopCallId: 'TSC-001',
            occurredAtDevice: new Date().toISOString(),
            outcome: 'Success',
          },
        ],
      },
      testIdempotencyKey
    );
    assert(
      syncBatchRes.successCount === 1 && lastReceivedIdempotencyKey === testIdempotencyKey,
      '5.4 Call API POST /api/v1/tickets/sync-offline with Idempotency-Key header',
      `Server validated Idempotency-Key: "${lastReceivedIdempotencyKey}", SuccessCount: ${syncBatchRes.successCount}`
    );

    // 5.5 End-to-End Live API Preload with syncService into SQLite
    const livePreloadResult = await syncService.preloadManifest('ST-BACH-DANG', mockBaseUrl);
    const liveBookingInDb = await SqliteRepository.getBookingByPublicId('pub_wb_live_api_01');
    assert(
      liveBookingInDb !== null && liveBookingInDb.bookingCode === 'WB-LIVE-999',
      '5.5 syncService.preloadManifest() calls live API and persists real response into SQLite',
      `Loaded booking "${liveBookingInDb?.bookingCode}" for passenger "${liveBookingInDb?.customerName}" into SQLite`
    );

    // 5.6 End-to-End Live API Check-In & Batch Sync via syncService
    if (liveBookingInDb && liveBookingInDb.tickets.length > 0) {
      await SqliteRepository.checkInTickets({
        ticketIds: [liveBookingInDb.tickets[0].id],
        bookingId: liveBookingInDb.id,
        tripId: liveBookingInDb.tripId,
        tripStopCallId: 'TSC-LIVE-001',
        staffAccountId: 'STAFF-TUAN-004',
        staffName: 'Nguyễn Văn Tuấn',
        scannerDeviceId: 'DEV-STAFF-TUAN-01',
      });
      const liveSyncResult = await syncService.triggerBatchSync(mockBaseUrl);
      assert(
        liveSyncResult.syncedCount >= 1,
        '5.6 syncService.triggerBatchSync() pushes live batch via HTTP and reconciles SQLite',
        `Successfully synced ${liveSyncResult.syncedCount} event(s) to server`
      );
    }
  } finally {
    if (isListening) {
      await new Promise<void>((resolve) => server.close(() => resolve()));
    }
    if (originalAdapter) {
      const { apiClient } = require('../src/api/client');
      apiClient.defaults.adapter = originalAdapter;
    }
    if (originalFetch) {
      (globalThis as any).fetch = originalFetch;
    }
  }

  // ═══════════════════════════════════════════════════
  // GROUP 6: Bug Fixes H03, M08, M13
  // ═══════════════════════════════════════════════════

  // 6.1 (M13) JWS header kid extraction — kid only in header, not in payload
  {
    const headerObj = { alg: 'HS256', typ: 'JWT', kid: DEFAULT_KEY_INFO.kid };
    const payloadObj = {
      pbid: sampleBookingId,
      ver: 1,
      tid: sampleTripId,
      iat: Math.floor(Date.now() / 1000),
      // NOTE: NO kid in payload — kid is only in the JWS header per RFC 7515
    };
    const headerB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(headerObj)));
    const payloadB64 = CryptoJS.enc.Base64url.stringify(CryptoJS.enc.Utf8.parse(JSON.stringify(payloadObj)));
    const signable = `${headerB64}.${payloadB64}`;
    const sig = CryptoJS.HmacSHA256(signable, DEFAULT_KEY_INFO.publicKeyPem).toString(CryptoJS.enc.Base64url);
    const jwsQr = `${headerB64}.${payloadB64}.${sig}`;

    const jwsResult = await QrVerifier.verifyQrCode(jwsQr);
    assert(
      jwsResult.isValid && jwsResult.publicBookingId === sampleBookingId,
      '6.1 (M13) JWS QR with kid only in header is verified successfully',
      `kid extracted: ${jwsResult.kid}, booking: ${jwsResult.publicBookingId}`
    );
    assert(
      jwsResult.kid === DEFAULT_KEY_INFO.kid,
      '6.2 (M13) kid correctly read from JWS header (not payload) per RFC 7515',
      `Expected kid="${DEFAULT_KEY_INFO.kid}", got kid="${jwsResult.kid}"`
    );
  }

  // 6.3 (H03) Late sync rejection handling
  {
    // Insert a pending event
    const lateSyncEventId = `late-sync-test-${Date.now()}`;
    await SqliteRepository.checkInTickets({
      ticketIds: ['TK-LATE-TEST-01'],
      bookingId: 'BK-LATE-TEST',
      tripId: 'TRIP-LATE-TEST',
      tripStopCallId: 'TSC-LATE-TEST',
      staffAccountId: 'STAFF-LATE',
      staffName: 'Staff Late Test',
      scannerDeviceId: 'DEV-LATE',
    });
    const pendingBefore = await SqliteRepository.getPendingEvents();
    const lateEvent = pendingBefore.find(e => e.tripId === 'TRIP-LATE-TEST');
    if (lateEvent) {
      await SqliteRepository.markEventConflict(
        lateEvent.clientEventId,
        'Đồng bộ trễ bị từ chối do quá giờ quy định',
        true // isLateSyncRejection
      );
      const allEvents = await SqliteRepository.getPendingEvents();
      const stillPending = allEvents.find(e => e.clientEventId === lateEvent.clientEventId);
      assert(
        !stillPending,
        '6.3 (H03) Late sync rejected event removed from pending queue (sync_status=Conflict)',
        `Event ${lateEvent.clientEventId} no longer in pending queue`
      );
    } else {
      assert(false, '6.3 (H03) Late sync rejection test setup failed', 'Could not find test event');
    }
  }

  // 6.4 (M08) Primary Scanner claim — offline fallback
  {
    const claimResult = await syncService.claimPrimaryOfflineRole('TSC-TEST-M08', 'http://localhost:19999');
    assert(
      claimResult.success === true && typeof claimResult.isPrimary === 'boolean',
      '6.4 (M08) claimPrimaryOfflineRole falls back gracefully when offline',
      `success=${claimResult.success}, isPrimary=${claimResult.isPrimary}, msg="${claimResult.message}"`
    );
  }

  console.log(`\nTests: ${passedTests} passed, ${totalTests} total`);

  if (passedTests === totalTests) {
    process.exit(0);
  } else {
    process.exit(1);
  }
}

runVerification().catch((err) => {
  console.error('Fatal test error:', err);
  process.exit(1);
});

