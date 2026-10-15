import { useEffect, useState } from 'react'
import Icon from './Icon'
import { t } from '../../i18n'

interface CopyButtonProps {
  value: string
  className?: string
}

/** "Copy" button that puts `value` on the clipboard and briefly shows "Copied". */
export default function CopyButton({ value, className = '' }: CopyButtonProps) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 1800)
    return () => clearTimeout(timer)
  }, [copied])

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
    } catch {
      // Clipboard can be unavailable (insecure context); the visual feedback still runs.
    }
    setCopied(true)
  }

  return (
    <button
      type="button"
      onClick={copy}
      aria-label={t('Copy {value}', { value })}
      className={`inline-flex items-center gap-1 text-xs font-semibold text-teal-flow transition-colors hover:text-deep-river ${className}`.trim()}
    >
      <Icon name={copied ? 'check' : 'content_copy'} className="text-[14px]" />
      {copied ? t('Copied') : t('Copy')}
    </button>
  )
}
