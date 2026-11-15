import { Platform } from 'react-native';

export { openDatabaseAsync } from 'expo-sqlite';
export const sqlitePlatform: string = Platform.OS;
