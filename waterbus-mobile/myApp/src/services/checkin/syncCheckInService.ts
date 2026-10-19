import { API_ENDPOINTS } from '../../constants/api';
/**
 * Đồng bộ theo lô các sự kiện check-in offline - POST /api/v1/checkins/sync (BR-OFF-03).
 */
import { apiClient } from '../api/apiClient';
import { CheckInEventSyncInput, SyncCheckInEventsResponse } from '../../types/checkin';

export async function syncCheckInEvents(events: CheckInEventSyncInput[]): Promise<SyncCheckInEventsResponse> {
  return apiClient.post<SyncCheckInEventsResponse>(API_ENDPOINTS.sync, { events });
}
