/**
 * Lưu trữ an toàn cho JWT token (expo-secure-store trên native, fallback in-memory cho
 * Web/Node test runner - không persist, chỉ để script/test chạy được, giống cách
 * src/services/sqlite/database.ts xử lý Web/Test).
 */

function isNativeApp(): boolean {
  try {
    const RN = require('react-native');
    return RN?.Platform?.OS === 'android' || RN?.Platform?.OS === 'ios';
  } catch {
    return false;
  }
}

const memoryFallback: Record<string, string> = {};

export async function setSecureItem(key: string, value: string): Promise<void> {
  if (isNativeApp()) {
    const SecureStore = await import('expo-secure-store');
    await SecureStore.setItemAsync(key, value);
  } else {
    memoryFallback[key] = value;
  }
}

export async function getSecureItem(key: string): Promise<string | null> {
  if (isNativeApp()) {
    const SecureStore = await import('expo-secure-store');
    return (await SecureStore.getItemAsync(key)) ?? null;
  }
  return memoryFallback[key] ?? null;
}

export async function deleteSecureItem(key: string): Promise<void> {
  if (isNativeApp()) {
    const SecureStore = await import('expo-secure-store');
    await SecureStore.deleteItemAsync(key);
  } else {
    delete memoryFallback[key];
  }
}
