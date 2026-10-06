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
  preloadCache: (stationId?: string) => Promise<boolean>;
  syncPendingBatch: () => Promise<number>;
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
    const { currentBooking, deviceInfo } = get();
    if (!currentBooking || ticketIds.length === 0) {
      return { success: false, count: 0, error: 'Không có vé nào được chọn' };
    }

    try {
      const res = await SqliteRepository.checkInTickets({
        ticketIds,
        bookingId: currentBooking.id,
        tripId: currentBooking.tripId,
        tripStopCallId: `TSC-${currentBooking.tripId}-${deviceInfo.stationId}`,
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

  preloadCache: async (stationId?: string) => {
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến và Public Key...' });
    try {
      const targetStation = stationId || get().deviceInfo.stationId;
      const res = await syncService.preloadManifest(targetStation);
      await get().refreshStats();
      set({ isPreloading: false, syncMessage: res.message });
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
    await SqliteRepository.clearCache();
    await get().refreshStats();
    set({ syncMessage: 'Đã xoá sạch bộ nhớ đệm cục bộ' });
  },
}));

