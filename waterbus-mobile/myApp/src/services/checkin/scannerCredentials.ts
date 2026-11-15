import { deleteSecureItem, getSecureItem, setSecureItem } from '../auth/secureStorage';
import type { MyScannerAssignment, ScannerOfflineGrant } from '../../types/checkin';

const PAIRING_KEY = 'waterbus_scanner_pairing_v1';
const CACHE_KEY = 'waterbus_scanner_manifest_v1';
const credentialKey = (id: string) => `waterbus_scanner_credential_${id}`;

export interface ScannerIdentity {
  deviceId: string;
  enrollmentId: string;
  deviceCode: string;
  stationId: string;
  stationName: string;
}

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

export const scannerCredentials = {
  identity: () => readJson<ScannerIdentity>(PAIRING_KEY),
  credential: (id: string) => getSecureItem(credentialKey(id)),
  invalidateCache: () => deleteSecureItem(CACHE_KEY),
  cache: () => readJson<CachedScannerContext>(CACHE_KEY),
  saveCache: (context: CachedScannerContext) => setSecureItem(CACHE_KEY, JSON.stringify(context)),
  async save(scanner: ScannerIdentity, credential: string) {
    await deleteSecureItem(CACHE_KEY);
    await setSecureItem(credentialKey(scanner.enrollmentId), credential);
    await setSecureItem(PAIRING_KEY, JSON.stringify(scanner));
  },
};
