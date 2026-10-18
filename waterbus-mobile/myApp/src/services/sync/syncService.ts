/**
 * Preload Cache & Idempotent Batch Sync Worker
 * 1. Preload manifest thật (BR-OFF-01) từ GET /checkins/manifest/{tripStopCallId} +
 *    GET /checkouts/qr-public-key, lưu vào SQLite qua SqliteRepository.
 * 2. Đồng bộ theo lô các sự kiện check-in offline (BR-OFF-03) qua POST /checkins/sync.
 *
 * KHÔNG còn fallback ngầm sang dữ liệu demo khi gọi API thật thất bại - lỗi mạng phải báo rõ
 * cho Staff, không được âm thầm nạp dữ liệu giả vào máy lúc tác nghiệp thật (đã từng là lỗi:
 * Staff không biết mình đang thấy danh sách khách giả). Dữ liệu demo chỉ dùng khi Staff bấm nút
 * demo rõ ràng (preloadDemoManifest) hoặc trong scripts/verify-staff-scanner.ts.
 */

import { SqliteRepository } from '../sqlite/sqliteRepository';
import { DEFAULT_KEY_INFO } from '../crypto/qrVerifier';
import { fetchScannerManifest } from '../checkin/manifestService';
import { fetchQrPublicKey } from '../checkin/qrPublicKeyService';
import { mapScannerManifestToPreloadManifest } from '../checkin/manifestMapper';
import { syncCheckInEvents } from '../checkin/syncCheckInService';
import { ApiError } from '../api/apiClient';
import {
  PreloadManifest,
  ScannerDeviceInfo,
} from '../../types/scanner';
import { CheckInEventSyncInput } from '../../types/checkin';

export type SyncListener = (status: {
  isSyncing: boolean;
  pendingCount: number;
  lastSyncAt: string | null;
  lastMessage: string | null;
}) => void;

class SyncService {
  private syncIntervalId: any = null;
  private isSyncing = false;
  private isOnline = true;
  private listeners: Set<SyncListener> = new Set();
  private lastSyncAt: string | null = null;
  private lastMessage: string | null = null;

  // staffId/staffName được điền từ tài khoản đăng nhập thật (xem useAuthStore); stationId/stationName
  // được điền bằng TripStopCallId/StationName thật sau khi preloadManifest thành công (xem
  // useScannerStore.preloadCache). Trống nghĩa là "chưa đăng nhập" / "chưa chọn điểm dừng".
  private currentDevice: ScannerDeviceInfo = {
    deviceId: 'DEV-BD-01',
    deviceCode: 'DEV-BD-01',
    staffId: '',
    staffName: '',
    stationId: '',
    stationName: '',
    isPrimaryOffline: true,
    forceOfflineMode: false,
  };

  constructor() {
    // Listen to network changes safely
    try {
      const netInfoModule = require('@react-native-community/netinfo');
      const NetInfo = netInfoModule?.default || netInfoModule;
      if (NetInfo?.addEventListener) {
        NetInfo.addEventListener((state: any) => {
          const wasOffline = !this.isOnline;
          this.isOnline = !!(state.isConnected && state.isInternetReachable !== false);

          if (wasOffline && this.isOnline && !this.currentDevice.forceOfflineMode) {
            console.log('[SyncService] Network restored (3G/WiFi) — auto triggering batch sync');
            this.triggerBatchSync();
          }
        });
      }
    } catch {
      // Non-native / Test environment
    }
  }

  public setDeviceInfo(info: Partial<ScannerDeviceInfo>) {
    this.currentDevice = { ...this.currentDevice, ...info };
  }

  public getDeviceInfo(): ScannerDeviceInfo {
    return { ...this.currentDevice };
  }

  public subscribe(listener: SyncListener): () => void {
    this.listeners.add(listener);
    // Send immediate initial status
    this.notifyStatus(0);
    return () => this.listeners.delete(listener);
  }

  private async notifyStatus(pendingCount?: number) {
    let count = pendingCount;
    if (count === undefined) {
      try {
        const stats = await SqliteRepository.getManifestStats();
        count = stats.pendingSyncCount;
      } catch {
        count = 0;
      }
    }

    const payload = {
      isSyncing: this.isSyncing,
      pendingCount: count,
      lastSyncAt: this.lastSyncAt,
      lastMessage: this.lastMessage,
    };

    this.listeners.forEach((l) => l(payload));
  }

  /**
   * Start recurring background sync timer (e.g. every 15s)
   */
  public startBackgroundWorker(intervalMs = 15000): void {
    if (this.syncIntervalId) return;

    console.log(`[SyncService] Background worker started (interval: ${intervalMs}ms)`);
    this.syncIntervalId = setInterval(() => {
      if (this.isOnline && !this.currentDevice.forceOfflineMode) {
        this.triggerBatchSync().catch((err) => {
          console.warn('[SyncService] Periodic batch sync error:', err.message);
        });
      }
    }, intervalMs);
  }

  public stopBackgroundWorker(): void {
    if (this.syncIntervalId) {
      clearInterval(this.syncIntervalId);
      this.syncIntervalId = null;
      console.log('[SyncService] Background worker stopped');
    }
  }

  /**
   * Preload Cache thật (BR-OFF-01): tải Public Key RSA + manifest Booking/Ticket của đúng điểm
   * dừng (TripStopCallId) Staff được phân công, lưu vào SQLite. KHÔNG fallback sang dữ liệu demo
   * khi lỗi - ném lỗi để màn hình báo rõ cho Staff (tránh tác nghiệp nhầm trên dữ liệu giả).
   */
  public async preloadManifest(tripStopCallId: string): Promise<{ manifest: PreloadManifest; message: string }> {
    console.log(`[SyncService] Preloading REAL manifest for TripStopCallId: ${tripStopCallId}...`);

    const [manifestResponse, publicKeyResponse] = await Promise.all([
      fetchScannerManifest(tripStopCallId),
      fetchQrPublicKey(),
    ]);

    const manifest = mapScannerManifestToPreloadManifest(manifestResponse, publicKeyResponse);

    await SqliteRepository.saveManifest(manifest);

    this.lastMessage = `Đã tải trước thành công ${manifest.bookings.length} đơn đặt, ${
      manifest.bookings.flatMap((b) => b.tickets).length
    } vé vào SQLite (${manifest.stationName})`;
    await this.notifyStatus();

    return { manifest, message: this.lastMessage };
  }

  /**
   * Idempotent Batch Sync Worker thật (BR-OFF-03): gửi các event Pending lên
   * POST /checkins/sync. Chỉ đánh "Synced" các event server đã THỰC SỰ xử lý (outcome khác
   * "Error"); event "Error" (vé không tồn tại, concurrency...) vẫn giữ Pending để báo lỗi/retry,
   * KHÔNG được giả lập thành công khi gọi server thất bại hoàn toàn (mất mạng/server down) -
   * trước đây có lỗi nghiêm trọng ở đây: catch mất mạng vẫn đánh dấu Synced hết, khiến check-in
   * thật không bao giờ tới được backend mà Staff không biết.
   */
  public async triggerBatchSync(): Promise<{ syncedCount: number; message: string }> {
    if (this.isSyncing) {
      return { syncedCount: 0, message: 'Tiến trình đồng bộ đang chạy' };
    }

    if (this.currentDevice.forceOfflineMode) {
      this.lastMessage = 'Đang bật chế độ Ngoại tuyến cố định (Offline Mode). Không gửi lên server.';
      await this.notifyStatus();
      return { syncedCount: 0, message: this.lastMessage };
    }

    this.isSyncing = true;
    await this.notifyStatus();

    try {
      const pendingEvents = await SqliteRepository.getPendingEvents(50);
      if (pendingEvents.length === 0) {
        this.lastMessage = 'Dữ liệu cục bộ đã đồng bộ đầy đủ (0 vé tồn đọng)';
        this.isSyncing = false;
        await this.notifyStatus(0);
        return { syncedCount: 0, message: this.lastMessage };
      }

      console.log(`[SyncService] Pushing batch of ${pendingEvents.length} offline check-in events...`);

      const payload: CheckInEventSyncInput[] = pendingEvents.map((e) => ({
        ticketId: e.ticketId,
        tripStopCallId: e.tripStopCallId,
        clientEventId: e.clientEventId,
        occurredAtDevice: e.occurredAtDevice,
        note: e.notes,
      }));

      const response = await syncCheckInEvents(payload);

      const resolved = response.results.filter((r) => r.outcome !== 'Error');
      const failed = response.results.filter((r) => r.outcome === 'Error');

      await SqliteRepository.markEventsSynced(
        resolved.map((r) => ({ clientEventId: r.clientEventId, receivedAtServer: new Date().toISOString() }))
      );
      for (const f of failed) {
        await SqliteRepository.recordSyncFailure([f.clientEventId], f.error || 'Lỗi không xác định từ máy chủ.');
      }

      this.lastSyncAt = new Date().toLocaleTimeString();
      this.lastMessage =
        failed.length === 0
          ? `Đã đồng bộ thành công lô ${resolved.length} vé lên hệ thống lúc ${this.lastSyncAt}`
          : `Đồng bộ ${resolved.length} vé thành công, ${failed.length} vé lỗi (xem chi tiết để đồng bộ lại).`;
      console.log(`[SyncService] ${this.lastMessage}`);

      const remainingStats = await SqliteRepository.getManifestStats();
      this.isSyncing = false;
      await this.notifyStatus(remainingStats.pendingSyncCount);

      return { syncedCount: resolved.length, message: this.lastMessage };
    } catch (error: any) {
      // Mất mạng hoàn toàn / server không phản hồi: KHÔNG đánh dấu Synced, chỉ ghi nhận lỗi để
      // lần quét định kỳ sau thử lại - việc giữ Pending ở đây chính là điểm "an toàn khi lỗi".
      const message = error instanceof ApiError ? error.message : error?.message || 'Không kết nối được máy chủ.';
      try {
        const pendingEvents = await SqliteRepository.getPendingEvents(50);
        await SqliteRepository.recordSyncFailure(pendingEvents.map((e) => e.clientEventId), message);
      } catch {
        // Bỏ qua lỗi phụ khi đang xử lý lỗi chính.
      }

      console.error('[SyncService] Batch sync failure:', error);
      this.lastMessage = `Lỗi đồng bộ: ${message}`;
      this.isSyncing = false;
      await this.notifyStatus();
      return { syncedCount: 0, message: this.lastMessage };
    }
  }

  /**
   * Preload DEMO rõ ràng (không phải đường tắt ngầm của preloadManifest thật) - chỉ dùng cho nút
   * demo hoặc scripts/verify-staff-scanner.ts khi chưa có backend thật để gọi.
   */
  public async preloadDemoManifest(
    stationId = 'ST-BACH-DANG'
  ): Promise<{ manifest: PreloadManifest; message: string }> {
    console.log(`[SyncService] Preloading DEMO manifest for station: ${stationId}...`);
    const manifest = this.generateDemoManifest(stationId);
    await SqliteRepository.saveManifest(manifest);

    this.lastMessage = `[DEMO] Đã tải ${manifest.bookings.length} đơn đặt, ${
      manifest.bookings.flatMap((b) => b.tickets).length
    } vé mẫu vào SQLite`;
    await this.notifyStatus();

    return { manifest, message: this.lastMessage };
  }

  /**
   * Sample generator producing realistic Saigon Waterbus data for local testing
   */
  private generateDemoManifest(stationId: string): PreloadManifest {
    const today = new Date().toISOString().split('T')[0];

    const trips = [
      {
        id: 'TRIP-BD-BA-01',
        tripCode: 'SWB-0830',
        routeName: 'Tuyến Bạch Đằng - Bình An - Linh Đông',
        tripType: 'Commuter' as const,
        boatName: 'Tàu Sài Gòn 01 (60 chỗ)',
        departureStationId: 'ST-BACH-DANG',
        departureStationName: 'Bến Bạch Đằng (Q1)',
        arrivalStationId: 'ST-BINH-AN',
        arrivalStationName: 'Bến Bình An (TP. Thủ Đức)',
        departureTime: `${today}T08:30:00+07:00`,
        arrivalTime: `${today}T09:15:00+07:00`,
        salesCloseAt: `${today}T08:20:00+07:00`,
        status: 'Boarding',
      },
      {
        id: 'TRIP-BD-SS-02',
        tripCode: 'SWB-SUNSET-1700',
        routeName: 'Tuyến Hoàng Hôn Ngắm Cảnh Sông Sài Gòn',
        tripType: 'Sightseeing' as const,
        boatName: 'Saigon Princess 02 (120 chỗ)',
        departureStationId: 'ST-BACH-DANG',
        departureStationName: 'Bến Bạch Đằng (Q1)',
        arrivalStationId: 'ST-BACH-DANG',
        arrivalStationName: 'Bến Bạch Đằng (Q1)',
        departureTime: `${today}T17:00:00+07:00`,
        arrivalTime: `${today}T18:30:00+07:00`,
        salesCloseAt: `${today}T16:50:00+07:00`,
        status: 'Scheduled',
      },
    ];

    const bookings = [
      {
        id: 'BK-001',
        publicBookingId: 'pub_wb_98a76b5c',
        bookingCode: 'WB20261006001',
        tripId: 'TRIP-BD-BA-01',
        customerName: 'Trần Văn Minh',
        customerPhone: '0901234567',
        totalAmount: 45000,
        qrCredentialVersion: 1,
        status: 'Confirmed',
        tickets: [
          {
            id: 'TK-001-A1',
            bookingId: 'BK-001',
            ticketNumber: 'TK202610060011',
            seatCode: 'A01',
            seatClass: 'Khoang trước (FrontCabin)',
            passengerName: 'Trần Văn Minh',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
          {
            id: 'TK-001-A2',
            bookingId: 'BK-001',
            ticketNumber: 'TK202610060012',
            seatCode: 'A02',
            seatClass: 'Khoang trước (FrontCabin)',
            passengerName: 'Lê Thị Mai (Vợ)',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
          {
            id: 'TK-001-A3',
            bookingId: 'BK-001',
            ticketNumber: 'TK202610060013',
            seatCode: 'A03',
            seatClass: 'Khoang trước (FrontCabin)',
            passengerName: 'Trần Minh Khang (Con)',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
        ],
      },
      {
        id: 'BK-002',
        publicBookingId: 'pub_wb_11f22e33',
        bookingCode: 'WB20261006002',
        tripId: 'TRIP-BD-BA-01',
        customerName: 'Nguyễn Thu Trang',
        customerPhone: '0987654321',
        totalAmount: 30000,
        qrCredentialVersion: 1,
        status: 'Confirmed',
        tickets: [
          {
            id: 'TK-002-B1',
            bookingId: 'BK-002',
            ticketNumber: 'TK202610060021',
            seatCode: 'B05',
            seatClass: 'Tiêu chuẩn (Standard)',
            passengerName: 'Nguyễn Thu Trang',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
          {
            id: 'TK-002-B2',
            bookingId: 'BK-002',
            ticketNumber: 'TK202610060022',
            seatCode: 'B06',
            seatClass: 'Tiêu chuẩn (Standard)',
            passengerName: 'Hoàng Quốc Bảo',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
        ],
      },
      {
        id: 'BK-003',
        publicBookingId: 'pub_wb_44d55c66',
        bookingCode: 'WB20261006003',
        tripId: 'TRIP-BD-BA-01',
        customerName: 'Phạm Đức Huy',
        customerPhone: '0912345678',
        totalAmount: 15000,
        qrCredentialVersion: 1,
        status: 'Confirmed',
        tickets: [
          {
            id: 'TK-003-C1',
            bookingId: 'BK-003',
            ticketNumber: 'TK202610060031',
            seatCode: 'C10',
            seatClass: 'Boong ngoài trời (Outdoor)',
            passengerName: 'Phạm Đức Huy',
            status: 'Valid' as const,
            boardingStatus: 'NotCheckedIn' as const,
            faceFare: 15000,
            paidFare: 15000,
          },
        ],
      },
    ];

    return {
      stationId,
      stationName: 'Bến Bạch Đằng (Quận 1)',
      generatedAt: new Date().toISOString(),
      publicKey: DEFAULT_KEY_INFO,
      trips,
      bookings,
    };
  }
}

export const syncService = new SyncService();
