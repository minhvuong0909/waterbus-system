import { CopyButton } from '../../../components/ui'

/** A secure link on one line with its copy button; long links are cut, the copy is always whole. */
export default function LinkField({ url }: { url: string }) {
  return (
    <div className="flex items-center gap-2 rounded-xl border border-outline-variant/50 bg-white px-3 py-2">
      <span className="min-w-0 flex-1 truncate font-mono text-xs text-deep-river" title={url}>
        {url}
      </span>
      <CopyButton value={url} />
    </div>
  )
}
