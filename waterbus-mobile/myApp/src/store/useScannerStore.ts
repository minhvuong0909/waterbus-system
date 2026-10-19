/**
 * Zustand Store for Staff Scanner App
 * Manages reactive UI state for scanning, check-in, SQLite statistics, and sync progress
 */

import { create } from 'zustand';
import {
  Booking,
  QrVerificationResult,
  ManifestStats,
  ScannerDeviceInfo,
} from '../types/scanner';
import { SqliteRepository } from '../services/sqlite/sqliteRepository';
import { syncService } from '../services/sync/syncService';
import { getMyScannerAssignments } from '../services/checkin/scannerAssignmentService';
import { MyScannerAssignment } from '../types/checkin';

interface ScannerState {
  // Current scanned booking
  currentBooking: Booking | null;
  verificationResult: QrVerificationResult | null;
  isModalOpen: boolean;

  // Local database & cache stats
  stats: ManifestStats;
  isPreloading: boolean;
  isSyncing: boolean;
  syncMessage: string | null;

  // Device & Staff Configuration
  deviceInfo: ScannerDeviceInfo;
  activeAssignment: MyScannerAssignment | null;

  // Recent scans history
  recentScans: {
    bookingCode: string;
    customerName: string;
    ticketCount: number;
    scannedAt: string;
    status: string;
  }[];

  // Actions
  refreshStats: () => Promise<void>;
  setCurrentBooking: (booking: Booking | null, verification?: QrVerificationResult | null) => void;
  closeBookingModal: () => void;
  performCheckIn: (ticketIds: string[], notes?: string) => Promise<{ success: boolean; count: number; error?: string }>;
  /** tripStopCallId thật (Guid) lấy từ GetMyScannerAssignments - không còn là stationId demo. */
  preloadCache: (tripStopCallId: string) => Promise<boolean>;
  syncPendingBatch: () => Promise<number>;
  setForceOfflineMode: (enabled: boolean) => void;
  clearCache: () => Promise<void>;
}

export const useScannerStore = create<ScannerState>((set, get) => ({
  currentBooking: null,
  activeAssignment: null,
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
    } catch (err) {
      console.warn('[Store] Could not load stats:', err);
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
    const { currentBooking, activeAssignment } = get();
    const deviceInfo = syncService.getDeviceInfo();
    if (!currentBooking || ticketIds.length === 0) {
      return { success: false, count: 0, error: 'Không có vé nào được chọn' };
    }

    const now = Date.now();
    if (!activeAssignment || currentBooking.tripId !== activeAssignment.tripId ||
        deviceInfo.stationId !== activeAssignment.tripStopCallId) {
      return { success: false, count: 0, error: 'Hãy tải trước phân công đúng chuyến trước khi soát vé.' };
    }
    if (!activeAssignment.isPrimaryOffline) {
      return { success: false, count: 0, error: 'Thiết bị này không được phân công ghi check-in ngoại tuyến.' };
    }
    const opens = Date.parse(activeAssignment.checkInOpenAt ?? '');
    const closes = Date.parse(activeAssignment.checkInCloseAt ?? '');
    if (!Number.isFinite(opens) || !Number.isFinite(closes) || now < opens || now > closes ||
        now < Date.parse(activeAssignment.activeFrom) ||
        (activeAssignment.activeUntil && now > Date.parse(activeAssignment.activeUntil))) {
      return { success: false, count: 0, error: 'Ngoài cửa sổ check-in hoặc phân công đã hết hiệu lực.' };
    }
    if (ticketIds.some((id) => !currentBooking.tickets.some((t) => t.id === id && t.status === 'Valid' && t.boardingStatus === 'NotCheckedIn'))) {
      return { success: false, count: 0, error: 'Vé không hợp lệ hoặc đã check-in.' };
    }

    try {
      const res = await SqliteRepository.checkInTickets({
        ticketIds,
        bookingId: currentBooking.id,
        tripId: currentBooking.tripId,
        // deviceInfo.stationId giờ giữ TripStopCallId thật của lần preload gần nhất (xem preloadCache).
        tripStopCallId: deviceInfo.stationId,
        staffAccountId: deviceInfo.staffId,
        staffName: deviceInfo.staffName,
        scannerDeviceId: deviceInfo.deviceId,
        notes,
      });

      // Update in-memory booking object to reflect changes immediately
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

      // Refresh SQLite stats
      await get().refreshStats();

      // Trigger background batch sync if online
      if (!deviceInfo.forceOfflineMode) {
        syncService.triggerBatchSync().catch((e) => console.log('Auto sync error:', e));
      }

      return { success: true, count: res.checkedInCount };
    } catch (error: any) {
      console.error('[Store] Check-in error:', error);
      return { success: false, count: 0, error: error.message };
    }
  },

  preloadCache: async (tripStopCallId: string) => {
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến và Public Key...' });
    try {
      const stats = await SqliteRepository.getManifestStats();
      if (stats.pendingSyncCount > 0) throw new Error('Hãy đồng bộ các sự kiện đang chờ trước khi tải lại manifest.');
      const assignments = await getMyScannerAssignments();
      const assignment = assignments.find((a) => a.tripStopCallId === tripStopCallId);
      if (!assignment) throw new Error('Bạn không được phân công tại điểm dừng này.');
      const res = await syncService.preloadManifest(tripStopCallId);
      // Ghi lại điểm dừng vừa preload (tái dùng field stationId) để performCheckIn gửi đúng
      // TripStopCallId khi tạo CheckInEvent offline.
      syncService.setDeviceInfo({ stationId: tripStopCallId, stationName: res.manifest.stationName,
        deviceId: assignment.deviceId, deviceCode: assignment.deviceCode, isPrimaryOffline: assignment.isPrimaryOffline });
      set({ activeAssignment: assignment, deviceInfo: syncService.getDeviceInfo() });
      await get().refreshStats();
      set({ isPreloading: false, syncMessage: res.message, deviceInfo: syncService.getDeviceInfo() });
      return true;
    } catch (e: any) {
      set({ isPreloading: false, syncMessage: `Lỗi tải trước: ${e.message}` });
      return false;
    }
  },

  syncPendingBatch: async () => {
    set({ isSyncing: true, syncMessage: 'Đang gửi lô check-in lên hệ thống...' });
    try {
      const res = await syncService.triggerBatchSync();
      await get().refreshStats();
      set({ isSyncing: false, syncMessage: res.message });
      return res.syncedCount;
    } catch (e: any) {
      set({ isSyncing: false, syncMessage: `Lỗi đồng bộ: ${e.message}` });
      return 0;
    }
  },

  setForceOfflineMode: (enabled: boolean) => {
    const updated = { ...get().deviceInfo, forceOfflineMode: enabled };
    syncService.setDeviceInfo(updated);
    set({
      deviceInfo: updated,
      syncMessage: enabled ? 'Đã bật chế độ Ngoại Tuyến (Offline Mode)' : 'Đã bật chế độ Tự động đồng bộ',
    });
  },

  clearCache: async () => {
    const stats = await SqliteRepository.getManifestStats();
    if (stats.pendingSyncCount > 0) throw new Error('Còn sự kiện chưa đồng bộ. Không thể xoá bộ nhớ đệm.');
    await SqliteRepository.clearCache();
    set({ activeAssignment: null, currentBooking: null, verificationResult: null, isModalOpen: false });
    await get().refreshStats();
    set({ syncMessage: 'Đã xoá sạch bộ nhớ đệm cục bộ' });
  },
}));

