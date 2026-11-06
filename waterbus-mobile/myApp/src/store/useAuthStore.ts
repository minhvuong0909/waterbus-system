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
 * Review PR #57 (2nd round, High - "Logout privacy still fails open"): nếu lần đăng xuất trước
 * purge manifest lỗi, thiết bị còn tra cứu được tên/SĐT khách ca cũ qua QR scan bình thường. Lần
 * đăng nhập/khôi phục phiên kế tiếp PHẢI thử xoá lại - và lần này KHÔNG được nuốt lỗi: nếu vẫn
 * lỗi, hàm này throw để caller (login/restoreSession) chặn hẳn truy cập manifest/scanner, thay vì
 * chỉ console.warn rồi cho vào như trước. Chặn ở đây TRƯỚC khi gọi authService.login - đây là gate
 * an toàn CỦA THIẾT BỊ, không phụ thuộc staff nào đang đăng nhập.
 */
const MANIFEST_PURGE_BLOCKED_MESSAGE =
  'Thiết bị chưa xoá sạch dữ liệu khách của ca trước (lỗi lưu trữ cục bộ lặp lại). ' +
  'Không thể đăng nhập cho tới khi xử lý xong - vui lòng khởi động lại ứng dụng hoặc báo kỹ thuật.';

async function retryPendingManifestPurgeIfAny(): Promise<void> {
  if (!(await SqliteRepository.isManifestPurgePending())) return;
  try {
    await SqliteRepository.purgeManifestForLogout();
    await SqliteRepository.setPendingManifestPurge(false);
  } catch (err) {
    console.error('[AuthStore] Retry dọn manifest còn treo từ lần đăng xuất trước vẫn lỗi:', err);
    throw new Error(MANIFEST_PURGE_BLOCKED_MESSAGE);
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
    let user: AuthUser | null;
    try {
      user = await authService.restoreSession();
    } catch {
      set({ user: null, isRestoring: false });
      return;
    }

    if (user) {
      try {
        // Review PR #57 (2nd round): purge còn treo + vẫn lỗi -> KHÔNG khôi phục phiên ngầm. Về
        // lại màn login (không phải vào thẳng scanner) để Staff biết cần xử lý trước khi dùng máy.
        await retryPendingManifestPurgeIfAny();
      } catch (err: any) {
        set({ user: null, isRestoring: false, error: err?.message || MANIFEST_PURGE_BLOCKED_MESSAGE });
        return;
      }
      // MOB-02: app bị OS kill rồi mở lại khi đang offline vẫn phải soát vé tiếp được - khôi
      // phục đúng phân công đã lưu của chính Staff này.
      await useScannerStore.getState().restoreActiveAssignment(user.userId);
    }

    applyStaffIdentity(user);
    set({ user, isRestoring: false });
  },

  login: async (email: string, password: string) => {
    set({ isLoggingIn: true, error: null });
    try {
      // Review PR #57 (2nd round): kiểm tra TRƯỚC khi gọi backend - gate an toàn của THIẾT BỊ,
      // không liên quan tài khoản nào đang đăng nhập. Lỗi ở đây chặn hẳn đăng nhập (throw, không
      // nuốt), đúng yêu cầu "block access to scanner/manifest data until cleanup succeeds".
      await retryPendingManifestPurgeIfAny();
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
    // MOB-06 + Review PR #57 (2nd round, High - "persisting pending_manifest_purge is best-effort
    // and can itself fail"): đặt cờ pending=true TRƯỚC KHI thử purge (coi lỗi là mặc định), chỉ gỡ
    // SAU KHI xác nhận purge thành công - ngược thứ tự so với trước (đặt cờ SAU khi cả 2 lần thử
    // đều lỗi, tức là phụ thuộc thêm 1 lượt ghi SQLite thứ 3 phải thành công thì cờ mới đáng tin).
    // Nếu chính việc ghi "pending=true" này lỗi, purge ngay sau đó hầu như chắc chắn CŨNG lỗi (cùng
    // nguyên nhân I/O) nên không mất độ tin cậy; còn nếu ghi "true" thành công nhưng app crash/mất
    // điện trước khi gỡ, lần đăng nhập kế tiếp vẫn coi là pending và thử lại - an toàn hơn chiều
    // ngược lại (quên đặt cờ khi lẽ ra phải đặt).
    await SqliteRepository.setPendingManifestPurge(true);
    let purgeWarning: string | null = null;
    try {
      await SqliteRepository.purgeManifestForLogout();
      await SqliteRepository.setPendingManifestPurge(false);
    } catch {
      try {
        await SqliteRepository.purgeManifestForLogout();
        await SqliteRepository.setPendingManifestPurge(false);
      } catch (retryErr: any) {
        // Cờ pending đã là true từ đầu hàm - không cần đặt lại. Lần đăng nhập/khôi phục phiên kế
        // tiếp (retryPendingManifestPurgeIfAny) giờ CHẶN HẲN nếu vẫn lỗi, không còn best-effort.
        purgeWarning =
          'Không thể xoá sạch dữ liệu khách của ca trước khỏi máy. KHÔNG Staff nào đăng nhập được ' +
          'trên máy này cho tới khi xử lý xong - vui lòng khởi động lại ứng dụng hoặc báo kỹ thuật ngay.';
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
