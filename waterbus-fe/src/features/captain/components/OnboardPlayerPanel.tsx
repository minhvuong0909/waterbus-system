import { Icon, Panel } from '../../../components/ui'
import { t } from '../../../i18n'
import { useOnboardPlayer } from '../hooks/useOnboardPlayer'

/**
 * Speaker of the boat (BR-EXP-04): plays the station announcements and sightseeing commentary the server triggers by the
 * boat's position, Vietnamese first and then English.
 */
export default function OnboardPlayerPanel() {
  const { status, armed, arm, nowPlaying, log } = useOnboardPlayer(true)

  return (
    <Panel title="Boat speaker">
      <div className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-on-surface-variant">
            {status === 'live' ? t('Connected to the boat speaker.') : t('Connecting…')}
          </p>
          <button
            type="button"
            onClick={arm}
            disabled={armed}
            className="inline-flex items-center gap-2 rounded-xl bg-teal-flow px-4 py-2 text-sm font-bold text-on-primary disabled:opacity-60"
          >
            <Icon name={armed ? 'volume_up' : 'volume_off'} className="text-xl" />
            {armed ? t('Speaker is on') : t('Turn on the speaker')}
          </button>
        </div>
        {!armed && (
          <p className="text-xs text-on-surface-variant">
            {t('Turn the speaker on once so the browser allows sound. Announcements play automatically after that.')}
          </p>
        )}
        {nowPlaying && (
          <div className="rounded-xl bg-surface-container p-3 text-sm text-deep-river" role="status">
            <div className="font-semibold">
              {t('Now playing')}: {nowPlaying.subjectName} ({nowPlaying.language})
            </div>
            <div className="text-on-surface-variant">{nowPlaying.text}</div>
          </div>
        )}
        {log.length > 0 && (
          <ul className="space-y-0.5 text-xs text-on-surface-variant">
            {log.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        )}
      </div>
    </Panel>
  )
}
