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
import { getSessionEpoch } from '../services/auth/sessionEpoch';
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
  performCheckIn: (
    ticketIds: string[],
    notes?: string,
    isManualOverride?: boolean
  ) => Promise<{ success: boolean; count: number; error?: string }>;
  /** tripStopCallId thật (Guid) lấy từ GetMyScannerAssignments - không còn là stationId demo. */
  preloadCache: (tripStopCallId: string) => Promise<boolean>;
  /** Khôi phục phân công đã lưu của đúng Staff này sau khi app bị kill/restart offline (MOB-02). */
  restoreActiveAssignment: (staffId: string) => Promise<void>;
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
    conflictSyncCount: 0,
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

  performCheckIn: async (ticketIds: string[], notes?: string, isManualOverride?: boolean) => {
    const { currentBooking, activeAssignment, verificationResult } = get();
    const deviceInfo = syncService.getDeviceInfo();
    if (!currentBooking || ticketIds.length === 0) {
      return { success: false, count: 0, error: 'Không có vé nào được chọn' };
    }

    // MOB-03: chữ ký số QR (hoặc đối chiếu version/tripId - MOB-04) chưa hợp lệ thì KHÔNG được
    // check-in như đường quét QR bình thường - bắt buộc đi qua Ghi đè thủ công (Manual Override)
    // có lý do, để tách rõ 2 luồng và có vết audit (BookingDetailModal.handleConfirmCheckIn).
    const isQrVerified = verificationResult?.isValid === true;

    // Review PR #57 (High - Manual Override security): isValid=false gộp 2 trường hợp hoàn toàn
    // khác nhau về rủi ro, không được xử lý như nhau:
    // (a) Staff tra tay hoàn toàn không có QR để xác thực (verifiedOffline=false -
    //     StaffScannerScreen.processManualLookup, vd màn khách vỡ/camera lỗi) - đây là fallback
    //     vận hành hợp lý, vẫn cho Ghi đè kèm lý do như trước.
    // (b) QR ĐÃ quét và xác thực chữ ký số THÀNH CÔNG (verifiedOffline=true) nhưng bị từ chối vì
    //     credential version/tripId không khớp manifest (MOB-04: QR cũ do đã cấp lại, hoặc QR của
    //     chuyến khác) - đây là tín hiệu QR có thể đã bị thu hồi hoặc dùng sai chuyến, một dòng lý
    //     do Staff tự nhập KHÔNG được phép vượt qua. Chặn cứng, không có đường Ghi đè nào áp dụng
    //     cho case này - Staff phải xin khách QR mới hoặc báo Admin xử lý (đối soát ngoài luồng).
    const isStaleOrMismatchedQr = verificationResult?.verifiedOffline === true && !isQrVerified;
    if (isStaleOrMismatchedQr) {
      return {
        success: false,
        count: 0,
        error:
          'QR đã quét được xác thực chữ ký nhưng KHÔNG khớp phiên bản/chuyến hiện tại (có thể đã bị thu hồi hoặc cấp lại). ' +
          'Không thể ghi đè bằng lý do thủ công cho trường hợp này - vui lòng yêu cầu khách cung cấp QR mới hoặc báo Admin xử lý.',
      };
    }
    if (!isManualOverride && !isQrVerified) {
      return {
        success: false,
        count: 0,
        error: 'Chưa xác thực chữ ký số QR. Cần xác nhận Ghi đè thủ công (Manual Override) kèm lý do trước khi check-in.',
      };
    }
    if (isManualOverride && !notes?.trim()) {
      return { success: false, count: 0, error: 'Ghi đè thủ công bắt buộc phải ghi rõ lý do.' };
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
        // Tag rõ "TRA TAY KHÔNG CÓ QR" (không còn ghi mơ hồ "chưa xác thực QR") vì sau khi chặn
        // cứng case (b) ở trên, đây là NHÃN DUY NHẤT còn có thể đi tới đây - giúp Admin đối soát
        // lọc đúng loại Ghi đè trong Notes (review PR #57: "structured audit/reconciliation").
        notes: isManualOverride ? `[GHI ĐÈ THỦ CÔNG - TRA TAY KHÔNG CÓ QR] ${notes}` : notes,
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
    // Review PR #57 (3rd round, High - race preload-vs-logout): chụp epoch TRƯỚC khi bắt đầu các
    // request mạng có thể mất vài giây - nếu Staff logout (hoặc 1 phiên khác bắt đầu) trong lúc
    // đang chờ, epoch đổi và mọi kết quả trả về sau đó đều bị coi là CHẾT, không được áp vào state
    // hiện tại (deviceInfo/activeAssignment có thể đã thuộc về Staff khác lúc này).
    const epochAtStart = getSessionEpoch();
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến và Public Key...' });
    try {
      const stats = await SqliteRepository.getManifestStats();
      if (stats.pendingSyncCount > 0) throw new Error('Hãy đồng bộ các sự kiện đang chờ trước khi tải lại manifest.');
      const assignments = await getMyScannerAssignments();
      const assignment = assignments.find((a) => a.tripStopCallId === tripStopCallId);
      if (!assignment) throw new Error('Bạn không được phân công tại điểm dừng này.');

      // syncService.preloadManifest tự kiểm tra lại epoch này NGAY TRƯỚC khi ghi SQLite (sau 2
      // request mạng) - throw nếu đã lệch, nên saveManifest() không bao giờ ghi dữ liệu của 1
      // phiên đã kết thúc.
      const res = await syncService.preloadManifest(tripStopCallId, epochAtStart);

      if (getSessionEpoch() !== epochAtStart) {
        // Phòng hờ thêm: dù saveManifest ở trên đã tự chặn, state Zustand/syncService dưới đây
        // vẫn KHÔNG được áp cho 1 phiên đã chết (có thể đã là deviceInfo của Staff khác).
        return false;
      }

      // Ghi lại điểm dừng vừa preload (tái dùng field stationId) để performCheckIn gửi đúng
      // TripStopCallId khi tạo CheckInEvent offline.
      syncService.setDeviceInfo({ stationId: tripStopCallId, stationName: res.manifest.stationName,
        deviceId: assignment.deviceId, deviceCode: assignment.deviceCode, isPrimaryOffline: assignment.isPrimaryOffline });
      set({ activeAssignment: assignment, deviceInfo: syncService.getDeviceInfo() });
      // MOB-02: lưu lại phân công vào SQLite - nếu app bị OS kill khi offline rồi mở lại, Staff
      // vẫn soát vé tiếp được dù không còn mạng để gọi lại GetMyScannerAssignments.
      await SqliteRepository.saveActiveAssignment(assignment, syncService.getDeviceInfo().staffId);
      await get().refreshStats();
      set({ isPreloading: false, syncMessage: res.message, deviceInfo: syncService.getDeviceInfo() });
      return true;
    } catch (e: any) {
      if (getSessionEpoch() !== epochAtStart) {
        // Lỗi (kể cả lỗi "phiên đã kết thúc" tự throw ở trên) của 1 phiên đã chết - không hiển thị
        // syncMessage này lên UI hiện tại (đã thuộc về phiên khác hoặc màn login).
        return false;
      }
      set({ isPreloading: false, syncMessage: `Lỗi tải trước: ${e.message}` });
      return false;
    }
  },

  restoreActiveAssignment: async (staffId: string) => {
    if (!staffId) return;
    try {
      const assignment = await SqliteRepository.getActiveAssignment(staffId);
      if (!assignment) return;
      syncService.setDeviceInfo({
        stationId: assignment.tripStopCallId,
        stationName: assignment.stationName,
        deviceId: assignment.deviceId,
        deviceCode: assignment.deviceCode,
        isPrimaryOffline: assignment.isPrimaryOffline,
      });
      set({ activeAssignment: assignment, deviceInfo: syncService.getDeviceInfo() });
    } catch (err) {
      console.warn('[Store] Could not restore active assignment:', err);
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
    // MOB-05 + Review PR #57 (Medium - Cache deletion race): đếm Pending/Conflict và xoá giờ nằm
    // CHUNG 1 transaction trong SqliteRepository.clearCache - không còn khoảng hở giữa đọc count
    // (await riêng) và DELETE (await riêng) như trước để 1 sự kiện mới chen vào rồi bị xoá nhầm.
    const result = await SqliteRepository.clearCache();
    if (!result.cleared) {
      if (result.conflictSyncCount > 0) {
        throw new Error(
          'Còn sự kiện bị máy chủ từ chối/đối soát (Conflict) chưa xử lý. Hãy báo Admin xử lý trước khi xoá bộ nhớ đệm.'
        );
      }
      throw new Error('Còn sự kiện chưa đồng bộ. Không thể xoá bộ nhớ đệm.');
    }
    await SqliteRepository.clearActiveAssignment();
    set({ activeAssignment: null, currentBooking: null, verificationResult: null, isModalOpen: false });
    await get().refreshStats();
    set({ syncMessage: 'Đã xoá sạch bộ nhớ đệm cục bộ' });
  },
}));

