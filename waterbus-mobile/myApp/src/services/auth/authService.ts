import { API_ENDPOINTS } from '../../constants/api';
/**
 * Đăng nhập Staff thật qua POST /api/v1/auth/login - mobile CHỈ phục vụ Staff Scanner
 * (CONVENTIONS.md mục 5), nên tài khoản không có vai trò "Staff" bị từ chối ngay ở đây dù
 * backend xác thực mật khẩu thành công.
 */
import { apiClient, AUTH_TOKEN_KEY } from '../api/apiClient';
import { getSecureItem, setSecureItem, deleteSecureItem } from './secureStorage';
import { AuthUser, LoginRequest, LoginResponseDto } from '../../types/auth';
import { SqliteRepository } from '../sqlite/sqliteRepository';

const USER_KEY = 'waterbus_staff_user';

class AuthService {
  async login(email: string, password: string): Promise<AuthUser> {
    const response = await apiClient.post<LoginResponseDto>(
      API_ENDPOINTS.login,
      { email, password } satisfies LoginRequest,
      { skipAuth: true }
    );

    if (!response.roles.includes('Staff')) {
      throw new Error(
        `Tài khoản "${response.email}" không có vai trò Nhân viên soát vé (Staff). ` +
          'Ứng dụng này chỉ dành cho Staff Scanner.'
      );
    }

    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      fullName: response.fullName,
      roles: response.roles,
    };

    const pending = await SqliteRepository.getPendingEvents(200);
    if (pending.some((event) => event.staffAccountId !== response.userId)) {
      throw new Error('Máy còn sự kiện của nhân viên khác chưa đồng bộ. Hãy đăng nhập lại tài khoản đó để đồng bộ trước.');
    }
    await setSecureItem(AUTH_TOKEN_KEY, response.token);
    await setSecureItem(USER_KEY, JSON.stringify(user));

    return user;
  }

  async logout(): Promise<void> {
    await deleteSecureItem(AUTH_TOKEN_KEY);
    await deleteSecureItem(USER_KEY);
  }

  /** Khôi phục phiên đăng nhập đã lưu khi mở lại app (không gọi lại backend). */
  async restoreSession(): Promise<AuthUser | null> {
    const token = await getSecureItem(AUTH_TOKEN_KEY);
    const userJson = await getSecureItem(USER_KEY);
    if (!token || !userJson) return null;

    try {
      return JSON.parse(userJson) as AuthUser;
    } catch {
      return null;
    }
  }
}

export const authService = new AuthService();
