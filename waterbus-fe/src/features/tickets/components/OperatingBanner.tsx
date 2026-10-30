import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

export default function OperatingBanner({ vessel }: { vessel: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-teal-flow/25 bg-secondary-container/40 px-space-md py-space-md text-sm">
      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-teal-flow text-on-primary">
        <Icon name="check" className="text-[18px]" />
      </span>
      <span>
        <b className="text-deep-river">{t('Trip Operating Normally.')}</b>{' '}
        <span className="text-teal-flow">
          {t('Vessel {vessel} is operating on schedule.', { vessel })}
        </span>
      </span>
    </div>
  )
}
