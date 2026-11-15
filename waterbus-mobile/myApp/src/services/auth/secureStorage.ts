/** Ephemeral credential storage for the Node verification suite and web preview. */
const memoryFallback = new Map<string, string>();

export async function setSecureItem(key: string, value: string): Promise<void> {
  memoryFallback.set(key, value);
}

export async function getSecureItem(key: string): Promise<string | null> {
  return memoryFallback.get(key) ?? null;
}

export async function deleteSecureItem(key: string): Promise<void> {
  memoryFallback.delete(key);
}
