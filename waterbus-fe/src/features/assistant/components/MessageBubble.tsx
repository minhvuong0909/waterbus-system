import type { ReactNode } from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { ChatMessage } from '../types'
import AssistantAvatar from './AssistantAvatar'

const TONE_STYLE: Record<NonNullable<ChatMessage['tone']> | 'normal', string> = {
  normal: 'bg-mist text-deep-river',
  fallback: 'border border-signal-amber/40 bg-signal-amber/10 text-deep-river',
  error: 'border border-coral-glow/30 bg-coral-glow/10 text-deep-river',
}

/** One message: the traveler's on the right, the assistant's on the left with its avatar. */
export default function MessageBubble({
  message,
  onRetry,
  children,
}: {
  message: ChatMessage
  /** Shown on an error message: sends its question again. */
  onRetry?: () => void
  /** Trip cards under an assistant message. */
  children?: ReactNode
}) {
  if (message.from === 'user') {
    return (
      <div className="flex justify-end">
        <p className="max-w-[85%] whitespace-pre-line break-words rounded-2xl rounded-tr-md bg-teal-flow px-3.5 py-2 text-sm text-white">
          {message.text}
        </p>
      </div>
    )
  }
  return (
    <div className="flex gap-2">
      <AssistantAvatar />
      <div className="min-w-0 flex-1">
        <div
          className={`inline-block max-w-full whitespace-pre-line break-words rounded-2xl rounded-tl-md px-3.5 py-2 text-sm ${
            TONE_STYLE[message.tone ?? 'normal']
          }`}
        >
          {message.tone === 'error' && (
            <Icon name="error" className="mr-1 align-[-3px] text-[16px] text-coral-glow" />
          )}
          {message.tone === 'fallback' && (
            <Icon name="info" className="mr-1 align-[-3px] text-[16px] text-signal-amber" />
          )}
          {message.text}
        </div>
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            className="mt-1.5 inline-flex min-h-[32px] items-center gap-1 rounded-full border border-outline-variant/60 bg-white px-3 text-xs font-semibold text-teal-flow hover:bg-surface-container-low"
          >
            <Icon name="refresh" className="text-[16px]" />
            {t('Try again')}
          </button>
        )}
        {children}
      </div>
    </div>
  )
}
