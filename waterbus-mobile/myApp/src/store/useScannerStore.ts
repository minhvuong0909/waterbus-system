import { create } from 'zustand';
import {
  Booking,
  QrVerificationResult,
  ManifestStats,
  ScannerDeviceInfo,
} from '../types/scanner';
import { SqliteRepository } from '../services/sqlite/sqliteRepository';
import { syncService } from '../services/sync/syncService';

interface ScannerState {
  currentBooking: Booking | null;
  verificationResult: QrVerificationResult | null;
  isModalOpen: boolean;

  stats: ManifestStats;
  isPreloading: boolean;
  isSyncing: boolean;
  syncMessage: string | null;

  deviceInfo: ScannerDeviceInfo;

  recentScans: {
    bookingCode: string;
    customerName: string;
    ticketCount: number;
    scannedAt: string;
    status: string;
  }[];

  refreshStats: () => Promise<void>;
  setCurrentBooking: (booking: Booking | null, verification?: QrVerificationResult | null) => void;
  closeBookingModal: () => void;
  performCheckIn: (ticketIds: string[], notes?: string) => Promise<{ success: boolean; count: number; error?: string }>;
  preloadCache: (stationId?: string) => Promise<boolean>;
  syncPendingBatch: () => Promise<number>;
  claimPrimaryScanner: (tripStopCallId: string) => Promise<{ success: boolean; isPrimary: boolean; message: string }>;
  setDeviceInfo: (info: Partial<ScannerDeviceInfo>) => void;
  setForceOfflineMode: (enabled: boolean) => void;
  clearCache: () => Promise<void>;
}

export const useScannerStore = create<ScannerState>((set, get) => ({
  currentBooking: null,
  verificationResult: null,
  isModalOpen: false,

  stats: {
    tripCount: 0,
    bookingCount: 0,
    ticketCount: 0,
    checkedInCount: 0,
    pendingSyncCount: 0,
    lastPreloadAt: null,
    databaseSizeKb: 0,
  },
  isPreloading: false,
  isSyncing: false,
  syncMessage: null,

  deviceInfo: syncService.getDeviceInfo(),
  recentScans: [],

  refreshStats: async () => {
    try {
      const stats = await SqliteRepository.getManifestStats();
      set({ stats });
    } catch {
      // Không để lỗi cập nhật thống kê làm gián đoạn thao tác soát vé.
    }
  },

  setCurrentBooking: (booking, verification = null) => {
    if (booking) {
      const recent = {
        bookingCode: booking.bookingCode,
        customerName: booking.customerName,
        ticketCount: booking.tickets.length,
        scannedAt: new Date().toLocaleTimeString(),
        status: booking.status,
      };

      set((state) => ({
        currentBooking: booking,
        verificationResult: verification,
        isModalOpen: true,
        recentScans: [recent, ...state.recentScans.slice(0, 9)],
      }));
    } else {
      set({
        currentBooking: null,
        verificationResult: null,
        isModalOpen: false,
      });
    }
  },

  closeBookingModal: () => {
    set({ isModalOpen: false, currentBooking: null, verificationResult: null });
  },

  performCheckIn: async (ticketIds: string[], notes?: string) => {
    const { currentBooking, deviceInfo, verificationResult } = get();
    if (!currentBooking || ticketIds.length === 0) {
      return { success: false, count: 0, error: 'Không có vé nào được chọn' };
    }
    if (verificationResult?.isValid !== true) {
      return {
        success: false,
        count: 0,
        error: 'Cần quét mã QR hợp lệ trước khi xác nhận khách lên tàu.',
      };
    }
    if (!deviceInfo.staffId || !currentBooking.tripStopCallId
        || !syncService.isPrimaryOfflineFor(currentBooking.tripStopCallId)) {
      return {
        success: false,
        count: 0,
        error: 'Đăng nhập và nhận quyền scanner offline chính cho chuyến này trước khi check-in.',
      };
    }

    try {
      // Lưu check-in cục bộ trước để vẫn hoạt động khi thiết bị mất mạng.
      const res = await SqliteRepository.checkInTickets({
        ticketIds,
        bookingId: currentBooking.id,
        tripId: currentBooking.tripId,
        tripStopCallId: currentBooking.tripStopCallId,
        staffAccountId: deviceInfo.staffId,
        staffName: deviceInfo.staffName,
        scannerDeviceId: deviceInfo.deviceId,
        notes,
      });

      const updatedTickets = currentBooking.tickets.map((t) => {
        if (ticketIds.includes(t.id)) {
          return {
            ...t,
            boardingStatus: 'CheckedIn' as const,
            checkedInAt: new Date().toISOString(),
          };
        }
        return t;
      });

      set({
        currentBooking: {
          ...currentBooking,
          tickets: updatedTickets,
        },
      });

      await get().refreshStats();

      if (!deviceInfo.forceOfflineMode) {
        // Gửi không chờ kết quả để không chặn nhân viên; worker sẽ thử lại nếu lỗi.
        syncService.triggerBatchSync().catch(() => {});
      }

      return { success: true, count: res.checkedInCount };
    } catch (error: any) {
      return { success: false, count: 0, error: error.message };
    }
  },

  preloadCache: async (stationId?: string) => {
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến và khóa xác thực...' });
    try {
      const targetStation = stationId || get().deviceInfo.stationId;
      const res = await syncService.preloadManifest(targetStation);
      const tripStopCallIds = [...new Set(
        res.manifest.bookings
          .map(booking => booking.tripStopCallId)
          .filter((tripStopCallId): tripStopCallId is string => !!tripStopCallId)
      )];
      if (tripStopCallIds.length === 0) {
        syncService.setDeviceInfo({ isPrimaryOffline: false });
        set({
          deviceInfo: syncService.getDeviceInfo(),
          isPreloading: false,
          syncMessage: 'Đã tải manifest nhưng chưa có điểm dừng được phân công để nhận quyền offline.',
        });
        await get().refreshStats();
        return false;
      }

      let claimMessage = '';
      for (const tripStopCallId of tripStopCallIds) {
        const claim = await syncService.claimPrimaryOfflineRole(tripStopCallId);
        claimMessage = claim.message;
        if (!claim.success || !claim.isPrimary) {
          set({
            deviceInfo: syncService.getDeviceInfo(),
            isPreloading: false,
            syncMessage: claimMessage,
          });
          await get().refreshStats();
          return false;
        }
      }

      await get().refreshStats();
      set({
        deviceInfo: syncService.getDeviceInfo(),
        isPreloading: false,
        syncMessage: `${res.message} ${claimMessage}`,
      });
      return true;
    } catch (error) {
      set({
        deviceInfo: syncService.getDeviceInfo(),
        isPreloading: false,
        syncMessage: error instanceof Error
          ? error.message
          : 'Không thể tải dữ liệu. Vui lòng kiểm tra kết nối và thử lại.',
      });
      return false;
    }
  },

  syncPendingBatch: async () => {
    set({ isSyncing: true, syncMessage: 'Đang gửi lượt soát vé lên máy chủ...' });
    try {
      const res = await syncService.triggerBatchSync();
      await get().refreshStats();
      set({ isSyncing: false, syncMessage: res.message });
      return res.syncedCount;
    } catch {
      set({ isSyncing: false, syncMessage: 'Không thể gửi dữ liệu. Vui lòng kiểm tra kết nối và thử lại.' });
      return 0;
    }
  },

  claimPrimaryScanner: async (tripStopCallId: string) => {
    const res = await syncService.claimPrimaryOfflineRole(tripStopCallId);
    set({
      deviceInfo: syncService.getDeviceInfo(),
      syncMessage: res.message,
    });
    return res;
  },

  setDeviceInfo: (info) => {
    syncService.setDeviceInfo(info);
    set({ deviceInfo: syncService.getDeviceInfo() });
  },

  setForceOfflineMode: (enabled: boolean) => {
    syncService.setDeviceInfo({ forceOfflineMode: enabled });
    set({
      deviceInfo: syncService.getDeviceInfo(),
      syncMessage: enabled ? 'Đã bật chế độ sử dụng khi mất kết nối.' : 'Đã bật chế độ tự động gửi dữ liệu.',
    });
  },

  clearCache: async () => {
    await SqliteRepository.clearCache();
    await get().refreshStats();
    set({ syncMessage: 'Đã xóa dữ liệu trên thiết bị.' });
  },
}));
