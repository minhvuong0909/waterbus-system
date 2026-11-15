import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { useOnboardPlayer } from '../hooks/useOnboardPlayer'

/**
 * Loa trên tàu (BR-EXP-04): phát thông báo bến và thuyết minh địa danh do backend kích hoạt theo vị trí tàu,
 * tiếng Việt trước rồi tiếng Anh. Dùng khung `captain-card` của Cổng thuyền trưởng.
 */
export default function OnboardPlayerPanel() {
  const { status, armed, arm, nowPlaying, log } = useOnboardPlayer(true)

  return (
    <section className="captain-card" aria-label={t('Boat speaker')}>
      <div className="captain-card-heading">
        <h2>
          <Icon name={armed ? 'volume_up' : 'volume_off'} className="text-xl" />
          {t('Boat speaker')}
        </h2>
        <button type="button" className="captain-outline" onClick={arm} disabled={armed}>
          {armed ? t('Speaker is on') : t('Turn on the speaker')}
        </button>
      </div>
      <div className="space-y-2 px-[18px] pb-4 text-sm">
        <p className="captain-muted">
          {status === 'live' ? t('Connected to the boat speaker.') : t('Connecting…')}
          {!armed && ` ${t('Turn the speaker on once so the browser allows sound. Announcements play automatically after that.')}`}
        </p>
        {nowPlaying && (
          <p className="captain-pill is-success" role="status">
            {t('Now playing')}: {nowPlaying.subjectName} ({nowPlaying.language}) — {nowPlaying.text}
          </p>
        )}
        {log.length > 0 && (
          <ul className="captain-muted space-y-0.5 text-xs">
            {log.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
