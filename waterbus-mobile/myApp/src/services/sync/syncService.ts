import { SqliteRepository } from '../sqlite/sqliteRepository';
import CryptoJS from 'crypto-js';
import {
  PreloadManifest,
  SyncOfflineBatchRequest,
  SyncBatchResponse,
  ScannerDeviceInfo,
} from '../../types/scanner';
import { getApiBaseUrl, scannerApiService, setApiBaseUrl } from '../../api/client';
import axios from 'axios';

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
  private primaryAssignments = new Map<string, string | null>();
  private listeners: Set<SyncListener> = new Set();
  private lastSyncAt: string | null = null;
  private lastMessage: string | null = null;

  private currentDevice: ScannerDeviceInfo = {
    deviceId: 'DEV-STAFF-TUAN-01',
    deviceCode: 'DEV-BD-01',
    staffId: '',
    staffName: 'Chưa đăng nhập',
    stationId: 'ST01',
    stationName: 'Bến Bạch Đằng (Quận 1)',
    isPrimaryOffline: false,
    forceOfflineMode: false,
  };

  constructor() {
    try {
      const netInfoModule = require('@react-native-community/netinfo');
      const NetInfo = netInfoModule?.default || netInfoModule;
      if (NetInfo?.addEventListener) {
        NetInfo.addEventListener((state: any) => {
          const wasOffline = !this.isOnline;
          this.isOnline = !!(state.isConnected && state.isInternetReachable !== false);

          // Khi mạng vừa hoạt động lại, gửi các lượt check-in đang chờ.
          if (wasOffline && this.isOnline && !this.currentDevice.forceOfflineMode) {
            this.triggerBatchSync();
          }
        });
      }
    } catch {
      // NetInfo có thể không khả dụng trong môi trường web hoặc kiểm thử.
    }
  }

  public setDeviceInfo(info: Partial<ScannerDeviceInfo>) {
    const identityChanged = (info.staffId !== undefined && info.staffId !== this.currentDevice.staffId)
      || (info.deviceCode !== undefined && info.deviceCode !== this.currentDevice.deviceCode);
    if (identityChanged || info.isPrimaryOffline === false) {
      this.primaryAssignments.clear();
      this.currentDevice.isPrimaryOffline = false;
    }
    this.currentDevice = { ...this.currentDevice, ...info };
  }

  public isPrimaryOfflineFor(tripStopCallId: string): boolean {
    if (!this.primaryAssignments.has(tripStopCallId)) return false;
    const activeUntil = this.primaryAssignments.get(tripStopCallId);
    if (activeUntil && Date.parse(activeUntil) <= Date.now()) {
      this.primaryAssignments.delete(tripStopCallId);
      this.currentDevice.isPrimaryOffline = this.primaryAssignments.size > 0;
      return false;
    }
    return true;
  }

  public getDeviceInfo(): ScannerDeviceInfo {
    return { ...this.currentDevice };
  }

  // Yêu cầu máy chủ phân công duy nhất một thiết bị soát vé chính cho mỗi lượt ghé bến.
  public async claimPrimaryOfflineRole(
    tripStopCallId: string,
    apiBaseUrl?: string
  ): Promise<{ success: boolean; isPrimary: boolean; message: string }> {
    try {
      if (apiBaseUrl && apiBaseUrl !== getApiBaseUrl()) {
        setApiBaseUrl(apiBaseUrl);
      }

      const response = await scannerApiService.claimPrimaryRole({
        deviceId: this.currentDevice.deviceId,
        deviceCode: this.currentDevice.deviceCode,
        staffAccountId: this.currentDevice.staffId,
        tripStopCallId,
      });

      const isPrimary = response.success && !!response.isPrimaryOffline;
      if (isPrimary) {
        this.primaryAssignments.set(tripStopCallId, response.activeUntil ?? null);
      } else {
        this.primaryAssignments.delete(tripStopCallId);
      }
      this.currentDevice.isPrimaryOffline = this.primaryAssignments.size > 0;
      return {
        success: response.success,
        isPrimary: this.isPrimaryOfflineFor(tripStopCallId),
        message: response.message ?? (isPrimary
          ? 'Thiết bị này được phân công soát vé chính tại bến.'
          : 'Một thiết bị khác đã được phân công soát vé chính tại bến này.'),
      };
    } catch (error) {
      if (axios.isAxiosError(error) && error.response) {
        const message = error.response.status === 409
          ? 'Một thiết bị khác đang là scanner offline chính tại bến này.'
          : error.response.status === 401 || error.response.status === 403
            ? 'Vui lòng đăng nhập bằng tài khoản Staff được phân công cho điểm dừng này.'
            : `Không thể xác nhận phân công trên máy chủ (HTTP ${error.response.status}).`;
        this.primaryAssignments.delete(tripStopCallId);
        this.currentDevice.isPrimaryOffline = this.primaryAssignments.size > 0;
        return { success: false, isPrimary: false, message };
      }

      if (!axios.isAxiosError(error)) {
        throw error;
      }

      // Khi mất mạng, chỉ giữ quyền đã được xác nhận trước đó trên thiết bị.
      return {
        success: this.isPrimaryOfflineFor(tripStopCallId),
        isPrimary: this.isPrimaryOfflineFor(tripStopCallId),
        message: this.isPrimaryOfflineFor(tripStopCallId)
          ? 'Chưa thể xác nhận trên máy chủ. Thiết bị này vẫn được phân công soát vé chính.'
          : 'Chưa thể xác nhận quyền soát vé trên máy chủ.',
      };
    }
  }

  public subscribe(listener: SyncListener): () => void {
    this.listeners.add(listener);
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

  public async preloadManifest(
    stationId = 'ST01',
    apiBaseUrl?: string
  ): Promise<{ manifest: PreloadManifest; message: string }> {
    // Lấy manifest theo bến để thiết bị có dữ liệu tra cứu khi mất mạng.
    if (apiBaseUrl && apiBaseUrl !== getApiBaseUrl()) {
      setApiBaseUrl(apiBaseUrl);
    }
    const manifest = await scannerApiService.getPreloadManifest(stationId);
    // Không lưu dữ liệu sai bến hoặc thiếu khóa RS256 dùng để xác minh QR offline.
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

  public async triggerBatchSync(
    apiBaseUrl?: string
  ): Promise<{ syncedCount: number; message: string }> {
    // Chặn hai lần gửi đồng thời và không gửi khi nhân viên bật chế độ offline.
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
      if (apiBaseUrl && apiBaseUrl !== getApiBaseUrl()) {
        setApiBaseUrl(apiBaseUrl);
      }

      // Mỗi yêu cầu chỉ lấy một lô giới hạn để tránh gửi quá nhiều dữ liệu cùng lúc.
      const pendingEvents = await SqliteRepository.getPendingEvents(50);
      if (pendingEvents.length === 0) {
        this.lastMessage = 'Không có lượt soát vé nào đang chờ gửi.';
        this.isSyncing = false;
        await this.notifyStatus(0);
        return { syncedCount: 0, message: this.lastMessage };
      }

      const batchTimestamp = new Date().toISOString();
      const eventIds = pendingEvents.map((event) => event.clientEventId);
      // Sắp xếp ID để gửi lại cùng lô luôn tạo cùng Idempotency-Key.
      const batchFingerprint = CryptoJS.SHA256([...eventIds].sort().join(':')).toString();
      const idempotencyKey = `BATCH-${this.currentDevice.deviceCode}-${batchFingerprint}`;

      const requestPayload: SyncOfflineBatchRequest = {
        scannerDeviceId: this.currentDevice.deviceCode,
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
        // Gửi lô kèm khóa ổn định để máy chủ nhận diện yêu cầu gửi lại.
        resData = await scannerApiService.syncOfflineBatch(requestPayload, idempotencyKey);
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

      // Chỉ chấp nhận phản hồi cho ID thuộc lô này; không tin ACK lạ hoặc lặp.
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
        // Chỉ đánh dấu Synced sau khi máy chủ xác nhận đúng sự kiện thành công.
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

  public startBackgroundWorker(intervalMs = 15000): void {
    if (this.syncIntervalId) return;

    // Bộ hẹn giờ hoạt động trong tiến trình app; không thay thế tác vụ nền của hệ điều hành.
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

}

export const syncService = new SyncService();
