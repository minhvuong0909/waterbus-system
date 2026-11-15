import * as SecureStore from 'expo-secure-store';

const options = { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY };

export const setSecureItem = (key: string, value: string): Promise<void> =>
  SecureStore.setItemAsync(key, value, options);

export const getSecureItem = (key: string): Promise<string | null> =>
  SecureStore.getItemAsync(key, options);

export const deleteSecureItem = (key: string): Promise<void> =>
  SecureStore.deleteItemAsync(key, options);
