/**
 * Zustand Store cho trạng thái đăng nhập Staff.
 */
import { create } from 'zustand';
import { authService } from '../services/auth/authService';
import { syncService } from '../services/sync/syncService';
import { SqliteRepository } from '../services/sqlite/sqliteRepository';
import { bumpSessionEpoch } from '../services/auth/sessionEpoch';
import { AuthUser } from '../types/auth';
import { useScannerStore } from './useScannerStore';

/**
 * Đồng bộ danh tính Staff thật vào deviceInfo (trước đây luôn là tên demo hard-code).
 * Review PR #57 (3rd round, High - "Restored offline assignment is immediately cleared during
 * session restoration"): hàm này reset `activeAssignment: null` - PHẢI được gọi TRƯỚC bất kỳ lệnh
 * khôi phục phân công nào (restoreActiveAssignment), không bao giờ SAU. Gọi sau sẽ xoá mất đúng
 * phân công vừa khôi phục (bug thật đã xảy ra ở restoreSession() bản trước - xem lại thứ tự ở
 * dưới nếu sửa hàm này).
 */
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

const MANIFEST_PURGE_BLOCKED_MESSAGE =
  'Thiết bị chưa xoá sạch dữ liệu khách của ca trước (lỗi lưu trữ cục bộ lặp lại). ' +
  'Không thể đăng nhập cho tới khi xử lý xong - vui lòng khởi động lại ứng dụng hoặc báo kỹ thuật.';

const LOGOUT_ABORTED_MESSAGE =
  'Không thể đăng xuất an toàn ngay lúc này (lỗi lưu trữ cục bộ). Vui lòng thử lại.';

/**
 * Review PR #57 (2nd round, High - "Logout privacy still fails open"): nếu lần đăng xuất trước
 * purge manifest lỗi, thiết bị còn tra cứu được tên/SĐT khách ca cũ qua QR scan bình thường. Lần
 * đăng nhập/khôi phục phiên kế tiếp PHẢI thử xoá lại - và lần này KHÔNG được nuốt lỗi: nếu vẫn
 * lỗi, hàm này throw để caller (login/restoreSession) chặn hẳn truy cập manifest/scanner, thay vì
 * chỉ console.warn rồi cho vào như trước. Chặn ở đây TRƯỚC khi gọi authService.login - đây là gate
 * an toàn CỦA THIẾT BỊ, không phụ thuộc staff nào đang đăng nhập.
 */
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

interface LogoutResult {
  /** false = logout bị HUỶ HOÀN TOÀN (lỗi ghi cờ pending) - vẫn đang đăng nhập, state không đổi. */
  loggedOut: boolean;
  /** Cảnh báo khi đăng xuất xong nhưng purge dữ liệu khách ca trước thất bại. */
  warning: string | null;
}

interface AuthState {
  user: AuthUser | null;
  /** true trong lúc khôi phục phiên đã lưu khi app khởi động (để tránh nháy màn hình login). */
  isRestoring: boolean;
  isLoggingIn: boolean;
  error: string | null;

  restoreSession: () => Promise<void>;
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<LogoutResult>;
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

    if (!user) {
      applyStaffIdentity(null);
      set({ user: null, isRestoring: false });
      return;
    }

    try {
      // Review PR #57 (2nd round): purge còn treo + vẫn lỗi -> KHÔNG khôi phục phiên ngầm. Về
      // lại màn login (không phải vào thẳng scanner) để Staff biết cần xử lý trước khi dùng máy.
      await retryPendingManifestPurgeIfAny();
    } catch (err: any) {
      set({ user: null, isRestoring: false, error: err?.message || MANIFEST_PURGE_BLOCKED_MESSAGE });
      return;
    }

    // Review PR #57 (3rd round, High): applyStaffIdentity PHẢI chạy TRƯỚC restoreActiveAssignment -
    // applyStaffIdentity reset activeAssignment:null, nên gọi SAU sẽ xoá mất đúng phân công vừa
    // khôi phục từ SQLite (bug thật ở bản trước, đã bị MOB-02 regression này chỉ ra).
    applyStaffIdentity(user);
    // MOB-02: app bị OS kill rồi mở lại khi đang offline vẫn phải soát vé tiếp được - khôi phục
    // đúng phân công đã lưu của chính Staff này.
    await useScannerStore.getState().restoreActiveAssignment(user.userId);
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
      // Review PR #57 (3rd round, High - race preload-vs-logout): bump epoch khi 1 phiên MỚI xác
      // thực xong - vô hiệu hoá nốt bất kỳ preload nào còn treo từ 1 phiên trước đó chưa logout
      // sạch (phòng hờ; luồng chính vẫn là logout() tự bump epoch của chính nó).
      bumpSessionEpoch();
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
    // Review PR #57 (3rd round, High - "in-flight manifest preload can repopulate passenger data
    // after logout"): dòng ĐỒNG BỘ đầu tiên, trước MỌI await - 1 preload đang treo (đã chụp epoch
    // cũ) sẽ tự phát hiện ra epoch đã đổi ở lần kiểm tra kế tiếp của nó (syncService.preloadManifest,
    // ngay trước khi ghi SQLite) và bỏ kết quả, bất kể logout() phía dưới có mất bao lâu để xong.
    bumpSessionEpoch();

    // Review PR #57 (3rd round, High - "logout can fail before the purge marker is persisted"):
    // ĐẶT CỜ TRƯỚC KHI xoá credentials, và nếu CHÍNH việc đặt cờ này lỗi thì HUỶ HẲN logout ngay -
    // không gọi authService.logout(), không đổi state nào. Ở thiết kế cũ, authService.logout() đã
    // chạy trước nên nếu bước đặt cờ sau đó lỗi, credentials mất nhưng state app chưa kịp đổi -
    // không nhất quán (reviewer: "must not leave the app operational under inconsistent
    // authentication state"). Giờ: toàn bộ chuỗi (đặt cờ -> xoá credentials -> purge -> reset
    // state) phải chạy trọn vẹn, hoặc dừng NGAY TỪ ĐẦU và không đổi gì - Staff vẫn ở trạng thái
    // đăng nhập cũ (an toàn hơn là nửa-đăng-xuất).
    try {
      await SqliteRepository.setPendingManifestPurge(true);
    } catch (err) {
      console.error('[AuthStore] Không thể đăng xuất an toàn - lỗi ghi cờ pending ngay từ đầu:', err);
      return { loggedOut: false, warning: LOGOUT_ABORTED_MESSAGE };
    }

    await authService.logout();

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
        // tiếp (retryPendingManifestPurgeIfAny) CHẶN HẲN nếu vẫn lỗi, không còn best-effort.
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
    return { loggedOut: true, warning: purgeWarning };
  },
}));
