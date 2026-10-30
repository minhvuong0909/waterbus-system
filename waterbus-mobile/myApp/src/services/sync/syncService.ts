/**
 * Preload Cache & Idempotent Batch Sync Service
 * Implements:
 * 1. Preload cache of trips, bookings, tickets, and public keys before gate opening (BR-OFF-01)
 * 2. Background task for automatic batch syncing offline check-in events when network is available (BR-OFF-03)
 */

import { SqliteRepository } from '../sqlite/sqliteRepository';
import CryptoJS from 'crypto-js';
import {
  PreloadManifest,
  SyncBatchResponse,
  ScannerDeviceInfo,
} from '../../types/scanner';

declare const require: any;

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

  private currentDevice: ScannerDeviceInfo = {
    deviceId: 'DEV-STAFF-TUAN-01',
    deviceCode: 'DEV-BD-01',
    staffId: 'STAFF-TUAN-004',
    staffName: 'Nguyễn Văn Tuấn',
    stationId: 'ST-BACH-DANG',
    stationName: 'Bến Bạch Đằng (Quận 1)',
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

  /**
   * M08: Atomically claim or verify Primary Offline Scanner role
   * Prevents race condition when multiple scanners attempt to become primary for the same TripStopCall
   */
  public async claimPrimaryOfflineRole(
    tripStopCallId: string,
    apiBaseUrl = 'http://localhost:5000'
  ): Promise<{ success: boolean; isPrimary: boolean; message: string }> {
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/scanner/claim-primary`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          deviceId: this.currentDevice.deviceId,
          deviceCode: this.currentDevice.deviceCode,
          staffAccountId: this.currentDevice.staffId,
          tripStopCallId,
        }),
      });

      if (response.ok) {
        const data = await response.json();
        const isPrimary = !!data.isPrimaryOffline;
        this.setDeviceInfo({ isPrimaryOffline: isPrimary });
        return {
          success: true,
          isPrimary,
          message: isPrimary
            ? 'Thiết bị này được phân công soát vé chính tại bến.'
            : 'Một thiết bị khác đã được phân công soát vé chính tại bến này.',
        };
      } else if (response.status === 409) {
        // Concurrency conflict / Another primary exists
        this.setDeviceInfo({ isPrimaryOffline: false });
        return {
          success: false,
          isPrimary: false,
          message: 'Một thiết bị khác đã được phân công soát vé chính tại bến này.',
        };
      } else {
        throw new Error(`Server returned HTTP ${response.status}`);
      }
    } catch {
      // Offline fallback: keep current designated local primary state
      return {
        success: true,
        isPrimary: this.currentDevice.isPrimaryOffline,
        message: this.currentDevice.isPrimaryOffline
          ? 'Chưa thể xác nhận trên máy chủ. Thiết bị này vẫn được phân công soát vé chính.'
          : 'Chưa thể xác nhận quyền soát vé trên máy chủ.',
      };
    }
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

    this.syncIntervalId = setInterval(() => {
      if (this.isOnline && !this.currentDevice.forceOfflineMode) {
        this.triggerBatchSync().catch(() => {});
      }
    }, intervalMs);
  }

  public stopBackgroundWorker(): void {
    if (this.syncIntervalId) {
      clearInterval(this.syncIntervalId);
      this.syncIntervalId = null;
    }
  }

  /**
   * Preload Cache Mechanism (BR-OFF-01)
   * Downloads trips, bookings, tickets, and Public Key into local SQLite before gate opening
   */
  public async preloadManifest(
    stationId = 'ST-BACH-DANG',
    apiBaseUrl = 'http://localhost:5000'
  ): Promise<{ manifest: PreloadManifest; message: string }> {
    const response = await fetch(
      `${apiBaseUrl}/api/v1/scanner/manifest?stationId=${encodeURIComponent(stationId)}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
      }
    );
    if (!response.ok) {
      throw new Error(`Không thể tải dữ liệu từ máy chủ (HTTP ${response.status}).`);
    }

    const manifest: PreloadManifest = await response.json();
    if (
      !manifest ||
      manifest.stationId !== stationId ||
      !manifest.publicKey ||
      manifest.publicKey.algorithm !== 'RS256' ||
      !manifest.publicKey.isActive ||
      !manifest.publicKey.publicKeyPem?.includes('BEGIN PUBLIC KEY') ||
      !Array.isArray(manifest.trips) ||
      !Array.isArray(manifest.bookings)
    ) {
      throw new Error('Dữ liệu tải về thiếu thông tin chuyến, vé hoặc khóa công khai RS256 hợp lệ.');
    }

    await SqliteRepository.saveManifest(manifest);

    this.lastMessage = `Đã tải thành công ${manifest.bookings.length} đơn đặt vé, ${
      manifest.bookings.flatMap((b) => b.tickets).length
    } vé về thiết bị.`;
    await this.notifyStatus();

    return {
      manifest,
      message: this.lastMessage,
    };
  }

  /**
   * Idempotent Batch Sync Worker (BR-OFF-03)
   * Fetches pending events from SQLite and pushes batch to server
   */
  public async triggerBatchSync(
    apiBaseUrl = 'http://localhost:5000'
  ): Promise<{ syncedCount: number; message: string }> {
    if (this.isSyncing) {
      return { syncedCount: 0, message: 'Đang gửi dữ liệu, vui lòng đợi.' };
    }

    if (this.currentDevice.forceOfflineMode) {
      this.lastMessage = 'Chế độ sử dụng khi mất kết nối đang bật. Dữ liệu chưa được gửi lên máy chủ.';
      await this.notifyStatus();
      return { syncedCount: 0, message: this.lastMessage };
    }

    this.isSyncing = true;
    await this.notifyStatus();

    try {
      const pendingEvents = await SqliteRepository.getPendingEvents(50);
      if (pendingEvents.length === 0) {
        this.lastMessage = 'Không có lượt soát vé nào đang chờ gửi.';
        this.isSyncing = false;
        await this.notifyStatus(0);
        return { syncedCount: 0, message: this.lastMessage };
      }

      const batchTimestamp = new Date().toISOString();
      const eventIds = pendingEvents.map((event) => event.clientEventId);
      const batchFingerprint = CryptoJS.SHA256([...eventIds].sort().join(':')).toString();
      const idempotencyKey = `BATCH-${this.currentDevice.deviceCode}-${batchFingerprint}`;

      const requestPayload = {
        scannerDeviceId: this.currentDevice.deviceId,
        staffAccountId: this.currentDevice.staffId,
        batchTimestamp,
        events: pendingEvents.map((e) => ({
          clientEventId: e.clientEventId,
          ticketId: e.ticketId,
          ticketNumber: e.ticketNumber,
          passengerName: e.passengerName,
          seatCode: e.seatCode,
          bookingId: e.bookingId,
          tripId: e.tripId,
          tripStopCallId: e.tripStopCallId,
          occurredAtDevice: e.occurredAtDevice,
          outcome: e.outcome,
          notes: e.notes,
        })),
      };

      const successUpdates: { clientEventId: string; receivedAtServer: string }[] = [];
      let lateRejectionCount = 0;
      let conflictCount = 0;
      const acknowledgedIds = new Set<string>();
      const requestedIds = new Set(eventIds);

      let resData: SyncBatchResponse;
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/tickets/sync-offline`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Idempotency-Key': idempotencyKey,
          },
          body: JSON.stringify(requestPayload),
        });

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        resData = await response.json();
        if (!resData || !Array.isArray(resData.processedEvents)) {
          throw new Error('Máy chủ trả về kết quả đồng bộ không hợp lệ.');
        }
      } catch {
        await SqliteRepository.recordSyncFailure(
          eventIds,
          'Chưa nhận được xác nhận từ máy chủ; lượt soát vẫn được giữ để gửi lại.'
        );
        throw new Error('Không nhận được xác nhận từ máy chủ.');
      }

      for (const pe of resData.processedEvents) {
        if (!requestedIds.has(pe.clientEventId) || acknowledgedIds.has(pe.clientEventId)) {
          continue;
        }

        if (pe.syncStatus === 'Synced' && pe.outcome === 'Success') {
          acknowledgedIds.add(pe.clientEventId);
          successUpdates.push({
            clientEventId: pe.clientEventId,
            receivedAtServer: new Date().toISOString(),
          });
        } else if (pe.syncStatus === 'Conflict' || pe.outcome === 'Conflict') {
          acknowledgedIds.add(pe.clientEventId);
          conflictCount++;
          const isLate = pe.message?.toLowerCase().includes('muộn') ||
            pe.message?.toLowerCase().includes('trễ') ||
            pe.message?.toLowerCase().includes('late') ||
            pe.message?.toLowerCase().includes('window');
          if (isLate) lateRejectionCount++;
          await SqliteRepository.markEventConflict(
            pe.clientEventId,
            pe.message || (isLate ? 'Đồng bộ trễ bị từ chối do quá giờ quy định (H03)' : 'Xung đột check-in'),
            isLate
          );
        }
      }

      const unacknowledgedIds = eventIds.filter((eventId) => !acknowledgedIds.has(eventId));
      if (unacknowledgedIds.length > 0) {
        await SqliteRepository.recordSyncFailure(
          unacknowledgedIds,
          'Máy chủ chưa xác nhận lượt soát; lượt này sẽ được gửi lại.'
        );
      }

      if (successUpdates.length > 0) {
        await SqliteRepository.markEventsSynced(successUpdates);
      }

      this.lastSyncAt = new Date().toLocaleTimeString();
      let statusSummary = `Đã được máy chủ xác nhận ${successUpdates.length} lượt soát`;
      if (lateRejectionCount > 0) {
        statusSummary += `, ${lateRejectionCount} vé không được chấp nhận do gửi trễ`;
      }
      if (conflictCount > lateRejectionCount) {
        statusSummary += `, ${conflictCount - lateRejectionCount} vé bị trùng thông tin`;
      }
      if (unacknowledgedIds.length > 0) {
        statusSummary += `, ${unacknowledgedIds.length} lượt chưa được xác nhận và sẽ gửi lại`;
      }
      this.lastMessage = `${statusSummary} lúc ${this.lastSyncAt}`;

      const remainingStats = await SqliteRepository.getManifestStats();
      this.isSyncing = false;
      await this.notifyStatus(remainingStats.pendingSyncCount);

      return {
        syncedCount: successUpdates.length,
        message: this.lastMessage,
      };
    } catch {
      this.lastMessage = 'Không thể gửi dữ liệu lên máy chủ. Các lượt chưa được xác nhận vẫn được giữ trên thiết bị.';
      this.isSyncing = false;
      await this.notifyStatus();
      return { syncedCount: 0, message: this.lastMessage };
    }
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
      publicKey: {
        kid: 'unavailable',
        algorithm: 'RS256',
        publicKeyPem: '',
        validFrom: '',
        validTo: '',
        isActive: false,
      },
      trips,
      bookings,
    };
  }
}

export const syncService = new SyncService();
