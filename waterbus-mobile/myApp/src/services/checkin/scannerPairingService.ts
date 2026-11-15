import { API_ENDPOINTS } from '../../constants/api';
import { apiClient } from '../api/apiClient';
import { scannerCredentials } from './scannerCredentials';
import type { ScannerIdentity } from './scannerCredentials';

interface ScannerStatus {
  scanner: ScannerIdentity;
  shift: { id: string; activeFrom: string; activeUntil: string } | null;
}

export async function pairScanner(activation: string): Promise<ScannerIdentity> {
  if (!/^waterbus-pair:v1:[a-f0-9]{32}\.[A-F0-9]{64}$/.test(activation))
    throw new Error('QR này không phải mã kích hoạt thiết bị Waterbus.');

  const response = await apiClient.post<{ scanner: ScannerIdentity; credential: string }>(
    API_ENDPOINTS.pair, { activation },
  );
  await scannerCredentials.save(response.scanner, response.credential);
  return response.scanner;
}

export const fetchScannerStatus = (staffId: string) => apiClient.get<ScannerStatus>(API_ENDPOINTS.scannerStatus, { scanner: true, staffId });
