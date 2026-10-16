// Pier photos live in `assets/images/piers/<station code>.<ext>` (see the README there). Vite
// bundles whatever is in the folder, so adding a photo needs no code change.
const PHOTOS = import.meta.glob('../../assets/images/piers/*.{jpg,jpeg,png,webp}', {
  eager: true,
  as: 'url',
}) as Record<string, string>

const BY_CODE = new Map(
  Object.entries(PHOTOS).map(([path, url]) => {
    const file = path.split('/').pop() ?? ''
    return [file.replace(/\.[^.]+$/, '').toLowerCase(), url] as const
  }),
)

/** URL of the photo of the pier with this station code, or undefined when there is none yet. */
export function pierPhoto(code: string | undefined): string | undefined {
  return code ? BY_CODE.get(code.toLowerCase()) : undefined
}
