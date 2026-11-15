import { API_ENDPOINTS } from '../../constants/api';
import type { CheckInEventSyncInput } from '../../types/checkin';
import { apiClient } from '../api/apiClient';

export const submitOnlineCheckIn = (body: CheckInEventSyncInput, staffId: string) =>
  apiClient.post<{ outcome: string; checkedInAt: string | null }>(
    API_ENDPOINTS.checkIn, body, { scanner: true, staffId },
  );
