/**
 * Lưu trữ an toàn cho JWT token (expo-secure-store trên native, fallback in-memory cho
 * Web/Node test runner - không persist, chỉ để script/test chạy được, giống cách
 * src/services/sqlite/database.ts xử lý Web/Test).
 */

function isNativeApp(): boolean {
  try {
    // Import ĐÚNG submodule Platform, không phải cả gói 'react-native' - require() cả gói kéo theo
    // Libraries/PushNotificationIOS.js (module cũ không ai dùng ở đây), module này tạo
    // NativeEventEmitter() ngay khi nạp và throw Invariant Violation trên Expo Go (native module
    // của nó không có sẵn) - xem lý do tương tự ở src/services/sqlite/database.ts. Hàm này được
    // gọi RẤT SỚM (restoreSession lúc khởi động app), nên đây nhiều khả năng là nguồn gốc thật của
    // crash khi mở app trên thiết bị thật, không chỉ database.ts.
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const Platform = require('react-native/Libraries/Utilities/Platform').default;
    return Platform?.OS === 'android' || Platform?.OS === 'ios';
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
