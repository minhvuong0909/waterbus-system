import { t } from '../../../i18n'
import AssistantAvatar from './AssistantAvatar'

/** Three bouncing dots while the backend is answering (still dots when motion is reduced). */
export default function TypingIndicator() {
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
