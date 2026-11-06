/**
 * Zustand Store cho trạng thái đăng nhập Staff.
 */
import { create } from 'zustand';
import { authService } from '../services/auth/authService';
import { syncService } from '../services/sync/syncService';
import { SqliteRepository } from '../services/sqlite/sqliteRepository';
import { AuthUser } from '../types/auth';
import { useScannerStore } from './useScannerStore';

/** Đồng bộ danh tính Staff thật vào deviceInfo (trước đây luôn là tên demo hard-code). */
function applyStaffIdentity(user: AuthUser | null) {
  syncService.setDeviceInfo({
    staffId: user?.userId ?? '',
    staffName: user?.fullName ?? '',
  });
  // recentScans (tên khách gần đây) là state RAM của THIẾT BỊ, không phải của tài khoản - phải xoá
  // mỗi lần đổi danh tính (đăng nhập HOẶC đăng xuất) để Staff kế tiếp trên cùng máy không thấy lại
  // tên khách ca trước (Review PR #57 - Logout privacy: "recentScans is not reset").
  useScannerStore.setState({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: null,
    currentBooking: null, verificationResult: null, isModalOpen: false, recentScans: [] });
}

/**
 * Review PR #57 (Logout privacy): nếu lần đăng xuất trước purge manifest lỗi cả 2 lần (xem
 * logout() dưới), thiết bị còn tra cứu được tên/SĐT khách ca cũ qua QR scan bình thường. Lần
 * đăng nhập/khôi phục phiên kế tiếp PHẢI thử xoá lại trước khi coi dữ liệu manifest hiện có là an
 * toàn. Best-effort: nếu vẫn lỗi lần nữa, chỉ log - không chặn đăng nhập (máy có thể đang lỗi đĩa
 * thật, buộc Staff kẹt ở màn login sẽ tệ hơn là cố hết sức rồi báo kỹ thuật riêng).
 */
async function retryPendingManifestPurgeIfAny(): Promise<void> {
  try {
    if (await SqliteRepository.isManifestPurgePending()) {
      await SqliteRepository.purgeManifestForLogout();
      await SqliteRepository.setPendingManifestPurge(false);
    }
  } catch (err) {
    console.warn('[AuthStore] Retry dọn manifest còn treo từ lần đăng xuất trước vẫn lỗi:', err);
  }
}

interface AuthState {
  user: AuthUser | null;
  /** true trong lúc khôi phục phiên đã lưu khi app khởi động (để tránh nháy màn hình login). */
  isRestoring: boolean;
  isLoggingIn: boolean;
  error: string | null;

  restoreSession: () => Promise<void>;
  login: (email: string, password: string) => Promise<boolean>;
  /** Trả về thông báo cảnh báo nếu purge dữ liệu khách ca trước thất bại, null nếu đăng xuất sạch. */
  logout: () => Promise<string | null>;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isRestoring: true,
  isLoggingIn: false,
  error: null,

  restoreSession: async () => {
    try {
      const user = await authService.restoreSession();
      applyStaffIdentity(user);
      if (user) {
        await retryPendingManifestPurgeIfAny();
        // MOB-02: app bị OS kill rồi mở lại khi đang offline vẫn phải soát vé tiếp được - khôi
        // phục đúng phân công đã lưu của chính Staff này (applyStaffIdentity ở trên vừa reset về null).
        await useScannerStore.getState().restoreActiveAssignment(user.userId);
      }
      set({ user, isRestoring: false });
    } catch {
      set({ user: null, isRestoring: false });
    }
  },

  login: async (email: string, password: string) => {
    set({ isLoggingIn: true, error: null });
    try {
      const user = await authService.login(email.trim(), password);
      applyStaffIdentity(user);
      await retryPendingManifestPurgeIfAny();
      await useScannerStore.getState().restoreActiveAssignment(user.userId);
      set({ user, isLoggingIn: false, error: null });
      return true;
    } catch (err: any) {
      set({ isLoggingIn: false, error: err?.message || 'Đăng nhập thất bại. Vui lòng thử lại.' });
      return false;
    }
  },

  logout: async () => {
    await authService.logout();
    // MOB-06 + Review PR #57 (High - Logout privacy): trước đây purge lỗi chỉ console.warn rồi
    // coi như đăng xuất xong, trong khi tên/SĐT khách ca trước (manifest_bookings/tickets) vẫn
    // TRA CỨU ĐƯỢC qua QR scan/tra tay bình thường của Staff kế tiếp - không chỉ là 1 màn hình
    // lịch sử. Thử lại 1 lần tại chỗ; nếu vẫn lỗi, đặt cờ pending để lần đăng nhập kế tiếp
    // (retryPendingManifestPurgeIfAny) bắt buộc thử lại, và trả cảnh báo THẬT ra ngoài (không nuốt
    // im lặng) để UI báo cho Staff. Vẫn cho đăng xuất tiếp tục dù purge lỗi hẳn - chặn cứng đăng
    // xuất vì lỗi I/O cục bộ sẽ kẹt Staff ở máy không theo ý muốn, tệ hơn giải pháp retry-sau.
    let purgeWarning: string | null = null;
    try {
      await SqliteRepository.purgeManifestForLogout();
      await SqliteRepository.setPendingManifestPurge(false);
    } catch {
      try {
        await SqliteRepository.purgeManifestForLogout();
        await SqliteRepository.setPendingManifestPurge(false);
      } catch (retryErr: any) {
        await SqliteRepository.setPendingManifestPurge(true).catch(() => {});
        purgeWarning =
          'Không thể xoá sạch dữ liệu khách của ca trước khỏi máy. Vui lòng khởi động lại ứng dụng ' +
          'trước khi đưa máy cho Staff khác, hoặc báo kỹ thuật nếu lặp lại.';
        console.error('[AuthStore] Manifest purge thất bại 2 lần liên tiếp khi đăng xuất:', retryErr);
      }
    }
    await SqliteRepository.clearActiveAssignment();
    // Xoá luôn TripStopCallId đang preload (tái dùng field stationId) - Staff kế tiếp đăng nhập
    // trên máy này không được kế thừa phân công của người trước.
    syncService.setDeviceInfo({ staffId: '', staffName: '', stationId: '', stationName: '' });
    applyStaffIdentity(null);
    set({ user: null, error: null });
    return purgeWarning;
  },
}));
