/**
 * Production-ready API Client for Smart Waterbus Mobile App (Staff Scanner)
 * Configured with dynamic baseUrl, timeout, and authentication token interceptor.
 */

import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { PreloadManifest, SyncBatchResponse, CheckInEvent, PublicKeyInfo } from '../types/scanner';

declare const require: any;

let isAndroid = false;
try {
  const RN = require('react-native');
  isAndroid = RN?.Platform?.OS === 'android';
} catch {
  // Web / Test environment fallback
}

// Default to backend port 5000 (standard for waterbus-be ASP.NET Core)
const DEFAULT_URL = isAndroid ? 'http://10.0.2.2:5000' : 'http://localhost:5000';

let currentBaseUrl = DEFAULT_URL;
let authToken: string | null = null;

export const setApiBaseUrl = (url: string) => {
  currentBaseUrl = url.trim().replace(/\/+$/, '');
  apiClient.defaults.baseURL = currentBaseUrl;
};

export const setAuthToken = (token: string | null) => {
  authToken = token;
};

export const getApiBaseUrl = () => currentBaseUrl;

export const apiClient: AxiosInstance = axios.create({
  baseURL: currentBaseUrl,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
});

// Interceptor to inject JWT Bearer Token if available
apiClient.interceptors.request.use((config) => {
  if (authToken && config.headers) {
    config.headers.Authorization = `Bearer ${authToken}`;
  }
  return config;
});

/**
 * High-level API Service methods matching Waterbus Backend contracts
 */
export const scannerApiService = {
  /**
   * GET /api/v1/scanner/manifest?stationId={stationId}
   * Downloads trips, bookings, tickets, and Public Key into local SQLite cache before gate opening (BR-OFF-01)
   */
  async getPreloadManifest(stationId: string): Promise<PreloadManifest> {
    const response = await apiClient.get<PreloadManifest>(`/api/v1/scanner/manifest`, {
      params: { stationId },
    });
    return response.data;
  },

  /**
   * POST /api/v1/tickets/sync-offline
   * Batch push offline check-in events to backend with Idempotency-Key (BR-OFF-03, BR-OFF-05)
   */
  async syncOfflineBatch(
    payload: {
      scannerDeviceId: string;
      staffAccountId: string;
      batchTimestamp: string;
      events: any[];
    },
    idempotencyKey: string
  ): Promise<SyncBatchResponse> {
    const response = await apiClient.post<SyncBatchResponse>(`/api/v1/tickets/sync-offline`, payload, {
      headers: {
        'Idempotency-Key': idempotencyKey,
      },
    });
    return response.data;
  },

  /**
   * GET /api/v1/scanner/public-key
   * Fetches latest public key for QR signature verification
   */
  async getPublicKey(kid?: string): Promise<PublicKeyInfo> {
    const response = await apiClient.get<PublicKeyInfo>(`/api/v1/scanner/public-key`, {
      params: kid ? { kid } : undefined,
    });
    return response.data;
  },

  /**
   * GET /api/v1/stations
   */
  async getStations(): Promise<any[]> {
    const response = await apiClient.get<any[]>(`/api/v1/stations`);
    return response.data;
  },

  /**
   * Health check / Ping
   */
  async ping(): Promise<boolean> {
    try {
      const response = await apiClient.get(`/api/v1/stations`, { timeout: 3000 });
      return response.status >= 200 && response.status < 300;
    } catch {
      return false;
    }
  },
};

export default apiClient;
