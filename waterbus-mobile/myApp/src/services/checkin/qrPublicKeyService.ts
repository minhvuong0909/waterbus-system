import { API_ENDPOINTS } from '../../constants/api';
/**
 * Public key RSA thật để verify QR offline - GET /api/v1/checkouts/qr-public-key (BR-QR-02, BR-OFF-01).
 */
import { apiClient } from '../api/apiClient';
import { QrPublicKeyResponse } from '../../types/checkin';

export async function fetchQrPublicKey(): Promise<QrPublicKeyResponse> {
  return apiClient.get<QrPublicKeyResponse>(API_ENDPOINTS.publicKey);
}
