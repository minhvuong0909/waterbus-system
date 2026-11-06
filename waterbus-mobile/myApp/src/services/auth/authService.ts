import axios from 'axios';
import { apiClient, setAuthToken } from '../../api/client';
import { AuthenticatedStaff } from '../../store/useAuthStore';

interface LoginResponse {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  token: string;
}

export async function loginStaff(email: string, password: string): Promise<AuthenticatedStaff> {
  try {
    const response = await apiClient.post<LoginResponse>('/api/v1/auth/login', { email, password });
    if (!response.data.roles.includes('Staff')) {
      throw new Error('Tài khoản này không có quyền nhân viên soát vé.');
    }

    setAuthToken(response.data.token);
    return {
      userId: response.data.userId,
      email: response.data.email,
      fullName: response.data.fullName,
    };
  } catch (error) {
    if (axios.isAxiosError(error)) {
      const body: unknown = error.response?.data;
      if (typeof body === 'object' && body !== null && 'message' in body
          && typeof body.message === 'string') {
        throw new Error(body.message);
      }
      if (typeof body === 'object' && body !== null && 'detail' in body
          && typeof body.detail === 'string') {
        throw new Error(body.detail);
      }
    }
    throw error;
  }
}

export function logoutStaff(): void {
  setAuthToken(null);
}
