/**
 * API client tối giản gắn Bearer token tự động và đọc lỗi dạng ProblemDetails (RFC 7807)
 * mà backend trả về (có traceId, errors theo field). AuthController trả lỗi 401 dạng
 * { message } đơn giản (không qua GlobalExceptionHandlingMiddleware) nên cũng được xử lý.
 */
import { API_BASE_URL } from '../../constants/api';
import { getSecureItem } from '../auth/secureStorage';

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
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };

  if (!options.skipAuth) {
    const token = await getSecureItem(AUTH_TOKEN_KEY);
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method || 'GET',
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

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
    throw new ApiError(response.status, message, traceId, errors);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }
  return (await response.json()) as T;
}

export const apiClient = {
  get: <T>(path: string): Promise<T> => request<T>(path),
  post: <T>(path: string, body?: unknown, options?: { skipAuth?: boolean }): Promise<T> =>
    request<T>(path, { method: 'POST', body, skipAuth: options?.skipAuth }),
};
