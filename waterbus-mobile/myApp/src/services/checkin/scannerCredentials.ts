import { deleteSecureItem, getSecureItem, setSecureItem } from '../auth/secureStorage';
import type { MyScannerAssignment, ScannerOfflineGrant } from '../../types/checkin';

const PAIRING_KEY = 'waterbus_scanner_pairing_v1';
const LEGACY_CACHE_KEY = 'waterbus_scanner_manifest_v1';
// Mỗi TripStopCall một khoá riêng (SecureStore giới hạn ~2KB/khoá) + một chỉ mục các id đã tải.
const INDEX_KEY = 'waterbus_scanner_trips_v2';
const contextKey = (id: string) => `waterbus_scanner_trip_${id}`;
const credentialKey = (id: string) => `waterbus_scanner_credential_${id}`;

export interface ScannerIdentity {
  deviceId: string;
  enrollmentId: string;
  deviceCode: string;
  stationId: string;
  stationName: string;
}

/** Ngữ cảnh đã được máy chủ cấp cho MỘT TripStopCall (phân công, cửa sổ soát vé, quyền offline). */
export interface CachedScannerContext {
  staffId: string;
  enrollmentId: string;
  assignment: MyScannerAssignment;
  grant: ScannerOfflineGrant | null;
  serverTime: number;
  deviceTime: number;
}

async function readJson<T>(key: string): Promise<T | null> {
  const value = await getSecureItem(key);
  return value === null ? null : JSON.parse(value) as T;
}

const readIndex = async () => (await readJson<string[]>(INDEX_KEY)) ?? [];

async function cacheList(): Promise<CachedScannerContext[]> {
  const contexts = await Promise.all((await readIndex()).map((id) => readJson<CachedScannerContext>(contextKey(id))));
  return contexts.filter((context): context is CachedScannerContext => context !== null);
}

async function removeCache(tripStopCallId: string) {
  await deleteSecureItem(contextKey(tripStopCallId));
  await setSecureItem(INDEX_KEY, JSON.stringify((await readIndex()).filter((id) => id !== tripStopCallId)));
}

export const scannerCredentials = {
  identity: () => readJson<ScannerIdentity>(PAIRING_KEY),
  credential: (id: string) => getSecureItem(credentialKey(id)),
  cacheList,
  removeCache,
  async invalidateCache() {
    for (const id of await readIndex()) await deleteSecureItem(contextKey(id));
    await deleteSecureItem(INDEX_KEY);
    await deleteSecureItem(LEGACY_CACHE_KEY);
  },
  /** Thêm/cập nhật ngữ cảnh của một TripStopCall; bỏ ngữ cảnh của nhân viên/máy khác. */
  async saveCache(context: CachedScannerContext) {
    for (const other of await cacheList()) {
      if (other.staffId !== context.staffId || other.enrollmentId !== context.enrollmentId)
        await removeCache(other.assignment.tripStopCallId);
    }
    const id = context.assignment.tripStopCallId;
    await setSecureItem(contextKey(id), JSON.stringify(context));
    await setSecureItem(INDEX_KEY, JSON.stringify([...new Set([...(await readIndex()), id])]));
  },
  async save(scanner: ScannerIdentity, credential: string) {
    await scannerCredentials.invalidateCache();
    await setSecureItem(credentialKey(scanner.enrollmentId), credential);
    await setSecureItem(PAIRING_KEY, JSON.stringify(scanner));
  },
};
