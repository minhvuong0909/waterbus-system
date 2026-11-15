/**
 * API client tối giản gắn Bearer token tự động và đọc lỗi dạng ProblemDetails (RFC 7807)
 * mà backend trả về (có traceId, errors theo field). AuthController trả lỗi 401 dạng
 * { message } đơn giản (không qua GlobalExceptionHandlingMiddleware) nên cũng được xử lý.
 */
import { API_BASE_URL } from '../../constants/api';
import { getSecureItem } from '../auth/secureStorage';
import { scannerCredentials } from '../checkin/scannerCredentials';
import forge from 'node-forge';

export const AUTH_TOKEN_KEY = 'waterbus_staff_token';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly traceId?: string,
    public readonly errors?: Record<string, string[]>
  ) {
    super(message);
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  /** Bỏ qua gắn Authorization header (vd gọi /auth/login) */
  skipAuth?: boolean;
  scanner?: true | string;
  staffId?: string;
}

export function tokenIsCurrent(token: string, staffId: string): boolean {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(forge.util.decode64(payload.padEnd(Math.ceil(payload.length / 4) * 4, '='))) as { sub?: string; exp?: number };
    return claims.sub === staffId && typeof claims.exp === 'number' && claims.exp * 1000 > Date.now();
  } catch {
    return false;
  }
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  if (typeof __DEV__ !== 'undefined' && !__DEV__ && !API_BASE_URL.startsWith('https://'))
    throw new ApiError(400, 'Ứng dụng soát vé yêu cầu API HTTPS.');

  const headers: Record<string, string> = { 'Content-Type': 'application/json', 'Accept-Language': 'vi-VN' };

  if (!options.skipAuth) {
    const token = await getSecureItem(AUTH_TOKEN_KEY);
    if (options.staffId && (!token || !tokenIsCurrent(token, options.staffId)))
      throw new ApiError(401, 'Vui lòng đăng nhập lại đúng tài khoản Staff.');
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
  }

  if (options.scanner) {
    const identity = options.scanner === true ? await scannerCredentials.identity() : null;
    const credential = options.scanner === true
      ? identity && await scannerCredentials.credential(identity.enrollmentId)
      : options.scanner;
    if (!credential) throw new ApiError(403, 'Máy chưa được ghép nối.');
    headers['X-Scanner-Credential'] = credential;
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method || 'GET',
    headers,
    redirect: 'error',
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: controller.signal,
    });
  } catch {
    throw new ApiError(0, 'Không kết nối được API. Kiểm tra địa chỉ máy chủ và Wi-Fi.');
  } finally {
    clearTimeout(timeout);
  }

  if (!response.ok) {
    let message = `Lỗi máy chủ (HTTP ${response.status})`;
    let traceId: string | undefined;
    let errors: Record<string, string[]> | undefined;
    try {
      const body = await response.json();
      message = body?.detail || body?.title || body?.message || message;
      traceId = body?.traceId;
      errors = body?.errors;
    } catch {
      // Phản hồi rỗng hoặc không phải JSON - giữ message mặc định.
    }
    if (options.scanner === true && (response.status === 401 || response.status === 403))
      await scannerCredentials.invalidateCache();
    throw new ApiError(response.status, message, traceId, errors);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }
  return (await response.json()) as T;
}

export const apiClient = {
  get: <T>(path: string, options?: Pick<RequestOptions, 'scanner' | 'staffId'>): Promise<T> => request<T>(path, options),
  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T> =>
    request<T>(path, { ...options, method: 'POST', body }),
};
