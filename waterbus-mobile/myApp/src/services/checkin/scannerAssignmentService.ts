/**
 * Phân công soát vé của Staff đang đăng nhập - GET /api/v1/checkins/assignments/mine.
 */
import { apiClient } from '../api/apiClient';
import { MyScannerAssignment } from '../../types/checkin';

export async function getMyScannerAssignments(): Promise<MyScannerAssignment[]> {
  return apiClient.get<MyScannerAssignment[]>('/api/v1/checkins/assignments/mine');
}
