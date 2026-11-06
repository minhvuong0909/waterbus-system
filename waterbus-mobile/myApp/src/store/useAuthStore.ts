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
  useScannerStore.setState({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: null,
    currentBooking: null, verificationResult: null, isModalOpen: false });
}

interface AuthState {
  user: AuthUser | null;
  /** true trong lúc khôi phục phiên đã lưu khi app khởi động (để tránh nháy màn hình login). */
  isRestoring: boolean;
  isLoggingIn: boolean;
  error: string | null;

  restoreSession: () => Promise<void>;
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
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
      // MOB-02: app bị OS kill rồi mở lại khi đang offline vẫn phải soát vé tiếp được - khôi
      // phục đúng phân công đã lưu của chính Staff này (applyStaffIdentity ở trên vừa reset về null).
      if (user) await useScannerStore.getState().restoreActiveAssignment(user.userId);
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
    // MOB-06: xoá tên/SĐT khách (manifest) khỏi SQLite chung thiết bị - Staff kế tiếp đăng nhập
    // trên máy này không được tra cứu dữ liệu khách của ca trước. KHÔNG xoá offline_checkin_events
    // (giữ Pending/Conflict để đối soát - xem purgeManifestForLogout).
    try {
      await SqliteRepository.purgeManifestForLogout();
    } catch (err) {
      console.warn('[AuthStore] Could not purge manifest on logout:', err);
    }
    await SqliteRepository.clearActiveAssignment();
    // Xoá luôn TripStopCallId đang preload (tái dùng field stationId) - Staff kế tiếp đăng nhập
    // trên máy này không được kế thừa phân công của người trước.
    syncService.setDeviceInfo({ staffId: '', staffName: '', stationId: '', stationName: '' });
    applyStaffIdentity(null);
    set({ user: null, error: null });
  },
}));
