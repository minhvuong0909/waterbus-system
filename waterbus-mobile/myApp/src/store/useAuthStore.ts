/**
 * Zustand Store cho trạng thái đăng nhập Staff.
 */
import { create } from 'zustand';
import { authService } from '../services/auth/authService';
import { AuthUser } from '../types/auth';

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
      set({ user, isRestoring: false });
    } catch {
      set({ user: null, isRestoring: false });
    }
  },

  login: async (email: string, password: string) => {
    set({ isLoggingIn: true, error: null });
    try {
      const user = await authService.login(email.trim(), password);
      set({ user, isLoggingIn: false, error: null });
      return true;
    } catch (err: any) {
      set({ isLoggingIn: false, error: err?.message || 'Đăng nhập thất bại. Vui lòng thử lại.' });
      return false;
    }
  },

  logout: async () => {
    await authService.logout();
    set({ user: null, error: null });
  },
}));
