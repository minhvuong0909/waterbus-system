/**
 * Zustand Store cho trạng thái đăng nhập Staff.
 */
import { create } from 'zustand';
import { authService } from '../services/auth/authService';
import { syncService } from '../services/sync/syncService';
import { AuthUser } from '../types/auth';
import { useScannerStore } from './useScannerStore';

/** Đồng bộ danh tính Staff thật vào deviceInfo (trước đây luôn là tên demo hard-code). */
function applyStaffIdentity(user: AuthUser | null) {
  syncService.setDeviceInfo({
    staffId: user?.userId ?? '',
    staffName: user?.fullName ?? '',
    forceOfflineMode: false,
  });
  useScannerStore.setState({ deviceInfo: syncService.getDeviceInfo(), activeAssignment: null,
    currentBooking: null, verificationResult: null, isModalOpen: false, shift: null, syncMessage: null, needsReviewCount: 0 });
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
      set({ user, isRestoring: false });
      if (user) void useScannerStore.getState().loadPairing();
      void useScannerStore.getState().refreshStats();
    } catch {
      set({ user: null, isRestoring: false });
    }
  },

  login: async (email: string, password: string) => {
    set({ isLoggingIn: true, error: null });
    try {
      const user = await authService.login(email.trim(), password);
      applyStaffIdentity(user);
      set({ user, isLoggingIn: false, error: null });
      void useScannerStore.getState().loadPairing();
      void useScannerStore.getState().refreshStats();
      return true;
    } catch (err: any) {
      set({ isLoggingIn: false, error: err?.message || 'Đăng nhập thất bại. Vui lòng thử lại.' });
      return false;
    }
  },

  logout: async () => {
    applyStaffIdentity(null);
    try {
      await authService.logout();
    } finally {
      set({ user: null, error: null });
    }
  },
}));
