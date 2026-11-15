import { useEffect, useState } from 'react'
import Icon from './Icon'
import { t } from '../../i18n'

interface CopyButtonProps {
  value: string
  className?: string
}

/** Nút "Sao chép" đưa `value` vào clipboard và hiện "Đã sao chép" trong chốc lát. */
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
      // Clipboard có thể không dùng được (ngữ cảnh không an toàn); phản hồi trên giao diện vẫn chạy.
    }
    setCopied(true)
  }

  return (
    <button
      type="button"
      onClick={copy}
      aria-label={t('Copy {value}', { value })}
      className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap text-xs font-semibold text-teal-flow transition-colors hover:text-deep-river ${className}`.trim()}
    >
      <Icon name={copied ? 'check' : 'content_copy'} className="text-sm" />
      {copied ? t('Copied') : t('Copy')}
    </button>
  )
}
