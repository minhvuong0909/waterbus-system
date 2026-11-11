// Web và bộ test dùng adapter bộ nhớ trong database.ts.
export async function openDatabaseAsync(_name: string): Promise<import('expo-sqlite').SQLiteDatabase> {
  throw new Error('SQLite native chỉ khả dụng trên Android và iOS.');
}
