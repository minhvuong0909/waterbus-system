// Pier photos live in `assets/images/piers/` (see the README there): `<code>.jpg` for the first
// photo and `<code>-2.jpg`, `<code>-3.jpg`, … for more. Vite bundles whatever is in the folder, so
// adding a photo needs no code change.
const FILES = import.meta.glob('../../assets/images/piers/*.{jpg,jpeg,png,webp}', {
  eager: true,
  as: 'url',
}) as Record<string, string>

const BY_CODE = new Map<string, { order: number; url: string }[]>()
for (const [path, url] of Object.entries(FILES)) {
  const name = (path.split('/').pop() ?? '').replace(/\.[^.]+$/, '').toLowerCase()
  const match = /^(.+?)(?:-(\d+))?$/.exec(name)
  if (!match) continue
  const list = BY_CODE.get(match[1]) ?? []
  list.push({ order: match[2] ? Number(match[2]) : 1, url })
  BY_CODE.set(match[1], list)
}

/** Photo URLs of the pier with this station code, in order; empty when there are none yet. */
export function pierPhotos(code: string | undefined): string[] {
  const list = code ? BY_CODE.get(code.toLowerCase()) : undefined
  return list ? [...list].sort((a, b) => a.order - b.order).map((p) => p.url) : []
}
