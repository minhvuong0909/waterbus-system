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
import { scannerCredentials } from '../checkin/scannerCredentials';
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
    deviceId: '',
    deviceCode: '',
    staffId: '',
    staffName: '',
    stationId: '',
    stationName: '',
    isPrimaryOffline: false,
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
          this.isOnline = state.isConnected !== false && state.isInternetReachable !== false;

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
        count = await SqliteRepository.countQueue(this.currentDevice.staffId, 'Pending');
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
  public isConnected(): boolean {
    return this.isOnline && !this.currentDevice.forceOfflineMode;
  }

  public async preloadManifest(tripStopCallId: string, staffId: string) {
    const [response, publicKey] = await Promise.all([fetchScannerManifest(tripStopCallId, staffId), fetchQrPublicKey()]);
    if (this.currentDevice.staffId !== staffId) throw new Error('Phiên đăng nhập đã thay đổi.');
    const manifest = mapScannerManifestToPreloadManifest(response, publicKey);
    // Gộp (không thay thế) để nhiều chuyến tại bến cùng nằm trong SQLite; vé chờ đồng bộ được bảo vệ.
    await SqliteRepository.saveManifest(manifest);
    const message = `Đã tải ${manifest.bookings.length} đơn đặt tại ${manifest.stationName}.`;
    return { response, message };
  }

  public async triggerBatchSync(includeReview = false): Promise<{ syncedCount: number; message: string }> {
    if (this.isSyncing) return { syncedCount: 0, message: 'Tiến trình đồng bộ đang chạy.' };
    const { staffId, forceOfflineMode } = this.currentDevice;
    if (!staffId || forceOfflineMode) return { syncedCount: 0, message: 'Chưa thể đồng bộ ở chế độ hiện tại.' };
    this.isSyncing = true;
    let sentIds: string[] = [];
    try {
      const pending = await SqliteRepository.getPendingEvents(500, staffId, undefined, true, includeReview);
      if (!pending.length) {
        const outstanding = await SqliteRepository.countQueue(staffId, 'Pending');
        this.lastMessage = outstanding > 0
          ? 'Có sự kiện cũ cần quản trị viên đối soát tại máy.'
          : 'Không có sự kiện chờ đồng bộ.';
        return { syncedCount: 0, message: this.lastMessage };
      }

      await this.notifyStatus();
      let syncedCount = 0;
      let needsReview = 0;
      let rejected = 0;
      let failed = 0;
      const enrollments = new Set(pending.map((event) => event.scannerEnrollmentId));
      for (const enrollmentId of enrollments) {
        if (!enrollmentId || this.currentDevice.staffId !== staffId) break;
        const credential = await scannerCredentials.credential(enrollmentId);
        const batch = pending.filter((event) => event.scannerEnrollmentId === enrollmentId).slice(0, 50);
        if (!credential) {
          failed += batch.length;
          continue;
        }

        const payload: CheckInEventSyncInput[] = batch.map((event) => ({
          ticketId: event.ticketId,
          tripStopCallId: event.tripStopCallId,
          clientEventId: event.clientEventId,
          occurredAtDevice: event.occurredAtDevice,
          offlineGrantId: event.offlineGrantId,
          note: event.notes,
        }));
        sentIds = batch.map((event) => event.clientEventId);
        const response = await syncCheckInEvents(payload, credential, staffId);
        for (const result of response.results) {
          if (!sentIds.includes(result.clientEventId)) continue;
          if (['Success', 'AlreadyCheckedIn'].includes(result.outcome)) {
            await SqliteRepository.markEventsSynced([{ clientEventId: result.clientEventId }]);
            syncedCount++;
          } else if (result.outcome === 'ReviewRejected') {
            await SqliteRepository.markEventsSynced([{ clientEventId: result.clientEventId }]);
            rejected++;
          } else if (result.outcome === 'Error') {
            await SqliteRepository.recordSyncFailure([result.clientEventId], result.error ?? 'Lỗi đồng bộ.');
            failed++;
          } else {
            await SqliteRepository.markEventConflict(result.clientEventId, result.error || result.outcome);
            needsReview++;
          }
        }
        sentIds = [];
      }

      if (syncedCount > 0) this.lastSyncAt = new Date().toLocaleTimeString();
      const message = `Đã đồng bộ ${syncedCount}. Cần đối soát ${needsReview}. Đã từ chối ${rejected}. Lỗi hoặc thiếu thông tin ghép nối ${failed}.`;
      if (this.currentDevice.staffId === staffId) this.lastMessage = message;
      return { syncedCount, message };
    } catch (error) {
      const reason = error instanceof Error ? error.message : 'Không kết nối được máy chủ.';
      if (error instanceof ApiError && (error.status === 401 || error.status === 403))
        await scannerCredentials.invalidateCache();
      await SqliteRepository.recordSyncFailure(sentIds, reason);
      const message = `Lỗi đồng bộ: ${reason}`;
      if (this.currentDevice.staffId === staffId) this.lastMessage = message;
      return { syncedCount: 0, message };
    } finally {
      this.isSyncing = false;
      await this.notifyStatus();
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
