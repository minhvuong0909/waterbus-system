import type { ReactNode } from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { ChatMessage } from '../types'

/** Questions offered before the first message. The label is shown, the question is what is asked. */
export const SUGGESTIONS = [
  { label: 'Upcoming departures', question: 'Hiện có những chuyến tàu nào sắp khởi hành?' },
  {
    label: 'Bạch Đằng → Linh Đông tomorrow',
    question: 'Tìm chuyến từ Bến Bạch Đằng đến Bến Linh Đông ngày mai',
  },
  { label: 'Trips this evening', question: 'Chiều tối nay còn chuyến nào không?' },
] as const

function AssistantAvatar() {
  return (
    <span
      aria-hidden="true"
      className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-deep-river text-sky-aqua"
    >
      <Icon name="bolt" className="text-[16px]" />
    </span>
  )
}

const TONE_STYLE: Record<NonNullable<ChatMessage['tone']> | 'normal', string> = {
  normal: 'bg-mist text-deep-river',
  fallback: 'border border-signal-amber/40 bg-signal-amber/10 text-deep-river',
  error: 'border border-coral-glow/30 bg-coral-glow/10 text-deep-river',
}

/** One message: the traveler's on the right, the assistant's on the left with its avatar. */
export function MessageBubble({
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

/** Three bouncing dots while the backend is answering (still dots when motion is reduced). */
export function TypingIndicator() {
  return (
    <div className="flex gap-2" role="status">
      <AssistantAvatar />
      <div className="flex items-center gap-1 rounded-2xl rounded-tl-md bg-mist px-3.5 py-3">
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            aria-hidden="true"
            className="h-1.5 w-1.5 animate-bounce rounded-full bg-teal-flow/70 motion-reduce:animate-none"
            style={{ animationDelay: `${delay}ms` }}
          />
        ))}
        <span className="sr-only">{t('Assistant is typing…')}</span>
      </div>
    </div>
  )
}

/** Starter questions, shown until the traveler sends something. */
export function Suggestions({ onPick, disabled }: { onPick: (label: string, question: string) => void; disabled: boolean }) {
  return (
    <div className="pl-9">
      <p className="mb-1.5 text-xs font-semibold text-on-surface-variant">{t('You can ask:')}</p>
      <div className="flex flex-wrap gap-1.5">
        {SUGGESTIONS.map((s) => (
          <button
            key={s.label}
            type="button"
            disabled={disabled}
            onClick={() => onPick(t(s.label), s.question)}
            className="min-h-[36px] rounded-full border border-teal-flow/30 bg-white px-3 text-xs font-medium text-deep-river transition-colors hover:border-teal-flow hover:bg-teal-flow/5 disabled:opacity-50"
          >
            {t(s.label)}
          </button>
        ))}
      </div>
    </div>
  )
}
