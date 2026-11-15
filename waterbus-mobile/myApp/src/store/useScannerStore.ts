import { create } from 'zustand';
import type { Booking, QrVerificationResult, ManifestStats, ScannerDeviceInfo } from '../types/scanner';
import type { MyScannerAssignment } from '../types/checkin';
import { SqliteRepository, generateUUID } from '../services/sqlite/sqliteRepository';
import { syncService } from '../services/sync/syncService';
import { getMyScannerAssignments } from '../services/checkin/scannerAssignmentService';
import { scannerCredentials, type ScannerIdentity } from '../services/checkin/scannerCredentials';
import { pickContextForTrip, serverNow } from '../services/checkin/tripContext';
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
  /** Mọi TripStopCall đã tải và được máy chủ cấp quyền trên máy này (đã sắp theo giờ khởi hành). */
  cachedAssignments: MyScannerAssignment[];
  pairingStatus: 'Unpaired' | 'Paired' | 'Unverified' | 'Invalid';
  shift: { activeFrom: string; activeUntil: string } | null;
  needsReviewCount: number;
  refreshStats: () => Promise<void>;
  loadPairing: () => Promise<void>;
  setCurrentBooking: (booking: Booking | null, verification?: QrVerificationResult | null) => void;
  closeBookingModal: () => void;
  performCheckIn: (ticketIds: string[], notes?: string) => Promise<CheckInFeedback>;
  preloadCache: (tripStopCallId: string) => Promise<boolean>;
  /** Tải một lần mọi chuyến được phân công tại bến của máy trong ca hiện tại. */
  preloadAll: () => Promise<{ loaded: number; total: number }>;
  openScannedBooking: (key: string, verification: QrVerificationResult) => Promise<ScanOpenResult>;
  /** Chỉ build development: giả lập mất mạng cho thao tác soát vé (không tắt Wi-Fi của máy). */
  setSimulatedOffline: (on: boolean) => Promise<void>;
  syncPendingBatch: () => Promise<number>;
  clearCache: () => Promise<void>;
}

export interface ScanOpenResult {
  status: 'opened' | 'notCached' | 'notAuthorized';
  message?: string;
}

const emptyStats: ManifestStats = {
  tripCount: 0, bookingCount: 0, ticketCount: 0, checkedInCount: 0,
  pendingSyncCount: 0, lastPreloadAt: null, databaseSizeKb: 0,
};

const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Không thể thực hiện yêu cầu.';

const departureKey = (a: MyScannerAssignment) => Date.parse(a.tripDepartureTime ?? a.checkInOpenAt ?? '') || 0;

async function loadContexts(staffId: string, enrollmentId: string) {
  return (await scannerCredentials.cacheList())
    .filter((context) => context.staffId === staffId && context.enrollmentId === enrollmentId)
    .sort((a, b) => departureKey(a.assignment) - departureKey(b.assignment));
}

const isDevBuild = () => typeof __DEV__ === 'undefined' || __DEV__;

/** Tải manifest (gộp vào SQLite) và lưu ngữ cảnh do máy chủ cấp cho đúng một TripStopCall. */
async function loadTripStop(call: MyScannerAssignment, identity: ScannerIdentity, staffId: string) {
  const { response, message } = await syncService.preloadManifest(call.tripStopCallId, staffId);
  const scanner = await scannerCredentials.identity();
  if (scanner?.enrollmentId !== identity.enrollmentId || syncService.getDeviceInfo().staffId !== staffId)
    throw new Error('Phiên làm việc đã thay đổi.');
  const grant = response.offlineGrant ?? null;
  const assignment: MyScannerAssignment = { ...call, checkInOpenAt: response.checkInOpenAt,
    checkInCloseAt: response.checkInCloseAt, isPrimaryOffline: grant !== null };
  await scannerCredentials.saveCache({
    staffId, enrollmentId: identity.enrollmentId, assignment, grant,
    serverTime: Date.parse(response.generatedAt), deviceTime: Date.now(),
  });
  return { assignment, message };
}

function inWindow(at: number, from: string | null, until: string | null, inclusiveEnd = false) {
  if (!from || !until) return false;
  const end = Date.parse(until);
  return at >= Date.parse(from) && (inclusiveEnd ? at <= end : at < end);
}

export const useScannerStore = create<ScannerState>((set, get) => ({
  currentBooking: null,
  activeAssignment: null,
  cachedAssignments: [],
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
      set({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: null, cachedAssignments: [],
        pairingStatus: 'Unpaired', shift: null });
      return;
    }

    const applyContexts = async () => {
      const contexts = await loadContexts(staffId, identity.enrollmentId);
      if (staffId !== syncService.getDeviceInfo().staffId) return;
      const assignments = contexts.map((context) => context.assignment);
      const current = get().activeAssignment;
      const active = assignments.find((a) => a.tripStopCallId === current?.tripStopCallId) ?? assignments[0] ?? null;
      set({ cachedAssignments: assignments, activeAssignment: active,
        shift: active?.activeUntil ? { activeFrom: active.activeFrom, activeUntil: active.activeUntil } : null });
    };
    set({ deviceInfo: syncService.getDeviceInfo(), pairingStatus: 'Unverified' });
    await applyContexts();

    // Giả lập ngoại tuyến: không gọi API xác minh ghép nối (tránh một lần gọi/lỗi làm mất phiên cục bộ hợp lệ).
    if (syncService.getDeviceInfo().forceOfflineMode) return;

    try {
      const status = await fetchScannerStatus(staffId);
      if (staffId !== syncService.getDeviceInfo().staffId) return;
      if (!status.shift) {
        await scannerCredentials.invalidateCache();
        set({ currentBooking: null, verificationResult: null, isModalOpen: false });
      } else {
        for (const context of await loadContexts(staffId, identity.enrollmentId))
          if (context.assignment.assignmentId !== status.shift.id)
            await scannerCredentials.removeCache(context.assignment.tripStopCallId);
      }
      await applyContexts();
      set({ pairingStatus: 'Paired', shift: status.shift });
    } catch (error) {
      if (staffId !== syncService.getDeviceInfo().staffId) return;
      if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
        await scannerCredentials.invalidateCache();
        set({ pairingStatus: 'Invalid', shift: null, activeAssignment: null, cachedAssignments: [], currentBooking: null,
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
    const { currentBooking, verificationResult, isCheckingIn, isPreloading } = get();
    if (isCheckingIn || isPreloading) return { success: false, count: 0, error: 'Máy đang xử lý một yêu cầu khác.' };
    if (!currentBooking || ticketIds.length === 0)
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
    let activeCall: MyScannerAssignment | null = null;
    try {
      const identity = await scannerCredentials.identity();
      const token = await getSecureItem(AUTH_TOKEN_KEY);
      if (!identity || !token || !tokenIsCurrent(token, staffId) || syncService.getDeviceInfo().staffId !== staffId)
        throw new Error('Vui lòng đăng nhập, ghép nối và tải dữ liệu đúng điểm dừng.');
      // Quyền lấy từ ngữ cảnh máy chủ đã cấp cho chính TripStopCall của chuyến này; TripId của QR chỉ để định tuyến.
      const cache = pickContextForTrip(await loadContexts(staffId, identity.enrollmentId), currentBooking.tripId);
      if (!cache) throw new Error('Chuyến này chưa được tải hoặc chưa được cấp quyền tại máy. Hãy tải trước dữ liệu chuyến.');
      const activeAssignment = cache.assignment;
      activeCall = activeAssignment;
      const deviceTime = Date.now();
      const at = serverNow(cache, deviceTime);
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
            if (response.outcome === 'NotAssigned') await scannerCredentials.removeCache(activeAssignment.tripStopCallId);
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
      set({ ...(activeCall ? { activeAssignment: activeCall } : {}), currentBooking: { ...currentBooking, tickets: currentBooking.tickets.map((ticket) =>
        accepted.has(ticket.id) ? { ...ticket, boardingStatus: 'CheckedIn', checkedInAt: accepted.get(ticket.id) } : ticket) } });
      await get().refreshStats();
    }
    return { success: accepted.size > 0, count: accepted.size, queued, error: failure };
  },

  preloadCache: async (tripStopCallId) => {
    if (get().isPreloading || get().isCheckingIn) return false;
    if (!syncService.isConnected()) {
      set({ syncMessage: 'Đang ở chế độ ngoại tuyến: không thể tải dữ liệu chuyến mới.' });
      return false;
    }
    const { staffId } = syncService.getDeviceInfo();
    set({ isPreloading: true, syncMessage: 'Đang tải dữ liệu chuyến...' });
    try {
      const identity = await scannerCredentials.identity();
      const calls = await getMyScannerAssignments(staffId);
      const call = calls.find((assignment) => assignment.tripStopCallId === tripStopCallId);
      if (!identity || !call || call.deviceId !== identity.deviceId)
        throw new Error('Bạn không được phân công tại điểm dừng này.');
      const { assignment, message } = await loadTripStop(call, identity, staffId);
      set({ activeAssignment: assignment, syncMessage: message });
      await get().loadPairing();
      await get().refreshStats();
      return true;
    } catch (error) {
      set({ syncMessage: `Lỗi tải trước: ${errorMessage(error)}` });
      return false;
    } finally {
      set({ isPreloading: false });
    }
  },

  preloadAll: async () => {
    if (get().isPreloading || get().isCheckingIn) return { loaded: 0, total: 0 };
    if (!syncService.isConnected()) {
      set({ syncMessage: 'Đang ở chế độ ngoại tuyến: không thể tải dữ liệu chuyến mới.' });
      return { loaded: 0, total: 0 };
    }
    const { staffId } = syncService.getDeviceInfo();
    set({ isPreloading: true, syncMessage: 'Đang tải mọi chuyến tại bến...' });
    let loaded = 0;
    let total = 0;
    const failures: string[] = [];
    try {
      const identity = await scannerCredentials.identity();
      if (!identity) throw new Error('Máy chưa được ghép nối.');
      const calls = (await getMyScannerAssignments(staffId))
        .filter((call) => call.deviceId === identity.deviceId)
        .sort((a, b) => departureKey(a) - departureKey(b));
      total = calls.length;
      for (const call of calls) {
        try {
          await loadTripStop(call, identity, staffId);
          loaded++;
        } catch (error) {
          failures.push(errorMessage(error));
        }
      }
      set({ syncMessage: `Đã tải ${loaded}/${total} chuyến tại bến.${failures.length ? ` Lỗi: ${failures[0]}` : ''}` });
    } catch (error) {
      set({ syncMessage: `Lỗi tải trước: ${errorMessage(error)}` });
    } finally {
      set({ isPreloading: false });
    }
    await get().loadPairing();
    await get().refreshStats();
    return { loaded, total };
  },

  openScannedBooking: async (key, verification) => {
    const { staffId } = syncService.getDeviceInfo();
    const identity = await scannerCredentials.identity();
    const find = async () =>
      (await SqliteRepository.getBookingByPublicId(key)) ?? (await SqliteRepository.searchBookingOrTicket(key));
    let booking = await find();
    let message: string | undefined;

    // Chuyến chưa có trong máy: nếu đang online và QR hợp lệ, tải theo yêu cầu. Quyền do máy chủ quyết định
    // (danh sách phân công + manifest kiểm tra lại); TripId trong QR chỉ chọn phân công cần tải.
    if (!booking && identity && verification.isValid && verification.tripId && syncService.isConnected()
      && !get().isPreloading && !get().isCheckingIn) {
      set({ isPreloading: true });
      try {
        const calls = (await getMyScannerAssignments(staffId)).filter((call) =>
          call.deviceId === identity.deviceId && call.tripId === verification.tripId);
        for (const call of calls) {
          await loadTripStop(call, identity, staffId);
          booking = await find();
          if (booking) break;
        }
      } catch (error) {
        message = errorMessage(error);
      } finally {
        set({ isPreloading: false });
      }
      await get().loadPairing();
      await get().refreshStats();
    }

    if (!booking) return { status: 'notCached', message };
    const contexts = identity ? await loadContexts(staffId, identity.enrollmentId) : [];
    const context = pickContextForTrip(contexts, booking.tripId);
    if (!context) return { status: 'notAuthorized', message };
    set({ activeAssignment: context.assignment });
    get().setCurrentBooking(booking, verification);
    return { status: 'opened' };
  },

  setSimulatedOffline: async (on) => {
    if (on && !isDevBuild()) return;
    syncService.setDeviceInfo({ forceOfflineMode: on });
    set({ deviceInfo: syncService.getDeviceInfo(),
      syncMessage: on ? 'Đang GIẢ LẬP ngoại tuyến (chỉ để thử nghiệm).' : null });
    if (on) return;
    // Trở lại online: đồng bộ lô các sự kiện đã xếp hàng rồi xác minh lại ghép nối.
    await get().refreshStats();
    if (get().stats.pendingSyncCount > 0) await get().syncPendingBatch();
    await get().loadPairing();
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
    set({ activeAssignment: null, cachedAssignments: [], currentBooking: null, verificationResult: null, isModalOpen: false });
    await get().refreshStats();
  },
}));
