/**
 * Shares a GET between the components that ask for the same data at about the same time: a request in
 * flight and a recent result are reused for `ttlMs`; a failure is not kept, so the next call asks again.
 */
export function createRequestCache<T>(ttlMs: number) {
  const entries = new Map<string, { value: Promise<T>; at: number }>()

  return {
    get(key: string, load: () => Promise<T>): Promise<T> {
      const hit = entries.get(key)
      if (hit && Date.now() - hit.at < ttlMs) return hit.value
      const entry = { value: load(), at: Date.now() }
      entries.set(key, entry)
      entry.value.catch(() => {
        if (entries.get(key) === entry) entries.delete(key)
      })
      return entry.value
    },
    clear() {
      entries.clear()
    },
  }
}
