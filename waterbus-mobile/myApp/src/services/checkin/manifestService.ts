/**
 * Preload manifest thật cho 1 điểm dừng được phân công - GET /api/v1/checkins/manifest/{tripStopCallId}.
 */
import { apiClient } from '../api/apiClient';
import { ScannerManifestResponse } from '../../types/checkin';
import { API_ENDPOINTS } from '../../constants/api';

export async function fetchScannerManifest(tripStopCallId: string): Promise<ScannerManifestResponse> {
  return apiClient.get<ScannerManifestResponse>(API_ENDPOINTS.manifest(tripStopCallId));
}
