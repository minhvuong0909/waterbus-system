import { create } from 'zustand';
import type { Booking, QrVerificationResult, ManifestStats, ScannerDeviceInfo } from '../types/scanner';
import type { MyScannerAssignment } from '../types/checkin';
import { SqliteRepository, generateUUID } from '../services/sqlite/sqliteRepository';
import { syncService } from '../services/sync/syncService';
import { getMyScannerAssignments } from '../services/checkin/scannerAssignmentService';
import { scannerCredentials } from '../services/checkin/scannerCredentials';
import { fetchScannerStatus } from '../services/checkin/scannerPairingService';
import { submitOnlineCheckIn } from '../services/checkin/onlineCheckInService';
import { ApiError, AUTH_TOKEN_KEY, tokenIsCurrent } from '../services/api/apiClient';
import { getSecureItem } from '../services/auth/secureStorage';

interface CheckInFeedback {
  success: boolean;
  count: number;
  queued?: boolean;
  error?: string;
}

interface ScannerState {
  currentBooking: Booking | null;
  verificationResult: QrVerificationResult | null;
  isModalOpen: boolean;
  stats: ManifestStats;
  isPreloading: boolean;
  isCheckingIn: boolean;
  isSyncing: boolean;
  syncMessage: string | null;
  deviceInfo: ScannerDeviceInfo;
  activeAssignment: MyScannerAssignment | null;
  pairingStatus: 'Unpaired' | 'Paired' | 'Unverified' | 'Invalid';
  shift: { activeFrom: string; activeUntil: string } | null;
  needsReviewCount: number;
  refreshStats: () => Promise<void>;
  loadPairing: () => Promise<void>;
  setCurrentBooking: (booking: Booking | null, verification?: QrVerificationResult | null) => void;
  closeBookingModal: () => void;
  performCheckIn: (ticketIds: string[], notes?: string) => Promise<CheckInFeedback>;
  preloadCache: (tripStopCallId: string) => Promise<boolean>;
  syncPendingBatch: () => Promise<number>;
  clearCache: () => Promise<void>;
}

const emptyStats: ManifestStats = {
  tripCount: 0, bookingCount: 0, ticketCount: 0, checkedInCount: 0,
  pendingSyncCount: 0, lastPreloadAt: null, databaseSizeKb: 0,
};

const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Không thể thực hiện yêu cầu.';

function inWindow(at: number, from: string | null, until: string | null, inclusiveEnd = false) {
  if (!from || !until) return false;
  const end = Date.parse(until);
  return at >= Date.parse(from) && (inclusiveEnd ? at <= end : at < end);
}

export const useScannerStore = create<ScannerState>((set, get) => ({
  currentBooking: null,
  activeAssignment: null,
  verificationResult: null,
  isModalOpen: false,
  stats: emptyStats,
  isPreloading: false,
  isCheckingIn: false,
  isSyncing: false,
  syncMessage: null,
  deviceInfo: syncService.getDeviceInfo(),
  pairingStatus: 'Unpaired',
  shift: null,
  needsReviewCount: 0,

  refreshStats: async () => {
    const staffId = syncService.getDeviceInfo().staffId;
    try {
      const [stats, pending, review] = await Promise.all([
        staffId ? SqliteRepository.getManifestStats() : Promise.resolve(emptyStats),
        SqliteRepository.countQueue(staffId, 'Pending'),
        SqliteRepository.countQueue(staffId, 'Conflict'),
      ]);
      if (staffId === syncService.getDeviceInfo().staffId)
        set({ stats: { ...stats, pendingSyncCount: pending }, needsReviewCount: review });
    } catch (error) {
      set({ syncMessage: `Lỗi cơ sở dữ liệu offline: ${errorMessage(error)}` });
    }
  },

  loadPairing: async () => {
    const staffId = syncService.getDeviceInfo().staffId;
    const identity = await scannerCredentials.identity();
    if (staffId !== syncService.getDeviceInfo().staffId) return;
    syncService.setDeviceInfo({
      deviceId: identity?.deviceId ?? '', deviceCode: identity?.deviceCode ?? '',
      stationId: identity?.stationId ?? '', stationName: identity?.stationName ?? '',
    });
    if (!identity || !staffId) {
      set({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: null, pairingStatus: 'Unpaired', shift: null });
      return;
    }

    let cached = await scannerCredentials.cache();
    if (cached?.staffId !== staffId || cached.enrollmentId !== identity.enrollmentId) cached = null;
    const cachedShift = cached?.assignment;
    set({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: cachedShift ?? null,
      pairingStatus: 'Unverified',
      shift: cachedShift?.activeUntil ? { activeFrom: cachedShift.activeFrom, activeUntil: cachedShift.activeUntil } : null });

    try {
      const status = await fetchScannerStatus(staffId);
      if (staffId !== syncService.getDeviceInfo().staffId) return;
      if (!status.shift || (cached && status.shift.id !== cached.assignment.assignmentId)) {
        await scannerCredentials.invalidateCache();
        set({ activeAssignment: null, currentBooking: null, verificationResult: null, isModalOpen: false });
      }
      set({ pairingStatus: 'Paired', shift: status.shift });
    } catch (error) {
      if (staffId !== syncService.getDeviceInfo().staffId) return;
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
        await scannerCredentials.invalidateCache();
        set({ pairingStatus: 'Invalid', shift: null, activeAssignment: null, currentBooking: null,
          verificationResult: null, isModalOpen: false });
      } else if (!(error instanceof ApiError) || error.status !== 0) {
        set({ syncMessage: errorMessage(error) });
      }
    }
  },

  setCurrentBooking: (booking, verification = null) => set({
    currentBooking: booking, verificationResult: verification, isModalOpen: booking !== null,
  }),

  closeBookingModal: () => set({ isModalOpen: false, currentBooking: null, verificationResult: null }),

  performCheckIn: async (ticketIds, notes) => {
    const { currentBooking, verificationResult, activeAssignment, isCheckingIn, isPreloading } = get();
    if (isCheckingIn || isPreloading) return { success: false, count: 0, error: 'Máy đang xử lý một yêu cầu khác.' };
    if (!currentBooking || !activeAssignment || ticketIds.length === 0)
      return { success: false, count: 0, error: 'Hãy tải dữ liệu đúng điểm dừng và chọn vé cần soát.' };
    if (!verificationResult?.isValid || verificationResult.publicBookingId !== currentBooking.publicBookingId ||
      verificationResult.version !== currentBooking.qrCredentialVersion ||
      (verificationResult.tripId && verificationResult.tripId !== currentBooking.tripId))
      return { success: false, count: 0, error: 'Cần xác minh mã QR hợp lệ của đơn đặt vé.' };
    if (ticketIds.some((id) => !currentBooking.tickets.some((ticket) =>
      ticket.id === id && ticket.status === 'Valid' && ticket.boardingStatus === 'NotCheckedIn')))
      return { success: false, count: 0, error: 'Vé không hợp lệ hoặc đã check-in.' };

    set({ isCheckingIn: true });
    const { staffId, staffName } = syncService.getDeviceInfo();
    const accepted = new Map<string, string>();
    let queued = false;
    let failure: string | undefined;
    try {
      const identity = await scannerCredentials.identity();
      const cache = await scannerCredentials.cache();
      const token = await getSecureItem(AUTH_TOKEN_KEY);
      if (!identity || !cache || cache.enrollmentId !== identity.enrollmentId || cache.staffId !== staffId ||
        !token || !tokenIsCurrent(token, staffId) || cache.assignment.tripStopCallId !== activeAssignment.tripStopCallId ||
        currentBooking.tripId !== activeAssignment.tripId || syncService.getDeviceInfo().staffId !== staffId)
        throw new Error('Vui lòng đăng nhập, ghép nối và tải dữ liệu đúng điểm dừng.');
      const deviceTime = Date.now();
      const at = cache.serverTime + deviceTime - cache.deviceTime;
      if (deviceTime < cache.deviceTime ||
        !inWindow(at, activeAssignment.activeFrom, activeAssignment.activeUntil) ||
        !inWindow(at, activeAssignment.checkInOpenAt, activeAssignment.checkInCloseAt, true))
        throw new Error('Ngoài ca làm việc hoặc cửa sổ soát vé.');

      const occurredAt = new Date(at).toISOString();
      if (syncService.isConnected()) {
        for (const ticketId of new Set(ticketIds)) {
          if (syncService.getDeviceInfo().staffId !== staffId) break;
          const response = await submitOnlineCheckIn({
            ticketId, tripStopCallId: activeAssignment.tripStopCallId, clientEventId: generateUUID(),
            occurredAtDevice: occurredAt, note: notes,
          }, staffId);
          if (response.outcome === 'Success' || response.outcome === 'AlreadyCheckedIn') {
            const checkedAt = response.checkedInAt ?? occurredAt;
            accepted.set(ticketId, checkedAt);
            await SqliteRepository.markOnlineCheckIn(ticketId, staffId, checkedAt);
          } else {
            if (response.outcome === 'NotAssigned') await scannerCredentials.invalidateCache();
            failure = `Máy chủ từ chối soát vé (${response.outcome}).`;
          }
        }
      } else {
        const grant = cache.grant;
        if (!activeAssignment.isPrimaryOffline || !grant || !inWindow(at, grant.activeFrom, grant.activeUntil))
          throw new Error('Thiết bị chưa có quyền soát vé ngoại tuyến hoặc quyền đã hết hạn.');

        const result = await SqliteRepository.checkInTickets({
          ticketIds: [...new Set(ticketIds)], bookingId: currentBooking.id, tripId: currentBooking.tripId,
          tripStopCallId: activeAssignment.tripStopCallId, staffAccountId: staffId, staffName,
          scannerDeviceId: identity.deviceId, scannerEnrollmentId: identity.enrollmentId, offlineGrantId: grant.id,
          occurredAtDevice: occurredAt, notes,
        });
        result.events.forEach((event) => accepted.set(event.ticketId, event.occurredAtDevice));
        queued = accepted.size > 0;
      }
    } catch (error) {
      failure = errorMessage(error);
      if (error instanceof ApiError && [401, 403].includes(error.status)) await get().loadPairing();
    } finally {
      set({ isCheckingIn: false });
    }

    if (syncService.getDeviceInfo().staffId === staffId && get().currentBooking?.id === currentBooking.id) {
      set({ currentBooking: { ...currentBooking, tickets: currentBooking.tickets.map((ticket) =>
        accepted.has(ticket.id) ? { ...ticket, boardingStatus: 'CheckedIn', checkedInAt: accepted.get(ticket.id) } : ticket) } });
      await get().refreshStats();
    }
    return { success: accepted.size > 0, count: accepted.size, queued, error: failure };
  },

  preloadCache: async (tripStopCallId) => {
    if (get().isPreloading || get().isCheckingIn) return false;
    const { staffId } = syncService.getDeviceInfo();
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến...' });
    try {
      const identity = await scannerCredentials.identity();
      const calls = await getMyScannerAssignments(staffId);
      const call = calls.find((assignment) => assignment.tripStopCallId === tripStopCallId);
      if (!identity || !call || call.deviceId !== identity.deviceId)
        throw new Error('Bạn không được phân công tại điểm dừng này.');
      set({ activeAssignment: null, currentBooking: null, verificationResult: null, isModalOpen: false });
      const { response, message } = await syncService.preloadManifest(tripStopCallId, staffId);
      const scanner = await scannerCredentials.identity();
      if (scanner?.enrollmentId !== identity.enrollmentId || syncService.getDeviceInfo().staffId !== staffId)
        throw new Error('Phiên làm việc đã thay đổi.');
      const grant = response.offlineGrant ?? null;
      const assignment = { ...call, checkInOpenAt: response.checkInOpenAt,
        checkInCloseAt: response.checkInCloseAt, isPrimaryOffline: grant !== null };
      await scannerCredentials.saveCache({
        staffId, enrollmentId: identity.enrollmentId, assignment, grant,
        serverTime: Date.parse(response.generatedAt), deviceTime: Date.now(),
      });
      set({ activeAssignment: assignment, syncMessage: message });
      await get().refreshStats();
      return true;
    } catch (error) {
      set({ syncMessage: `Lỗi tải trước: ${errorMessage(error)}` });
      return false;
    } finally {
      set({ isPreloading: false });
    }
  },

  syncPendingBatch: async () => {
    const result = await syncService.triggerBatchSync(true);
    set({ syncMessage: result.message });
    await get().refreshStats();
    return result.syncedCount;
  },

  clearCache: async () => {
    if (get().isCheckingIn || get().isPreloading) return;
    await scannerCredentials.invalidateCache();
    await SqliteRepository.clearManifest();
    set({ activeAssignment: null, currentBooking: null, verificationResult: null, isModalOpen: false });
    await get().refreshStats();
  },
}));
