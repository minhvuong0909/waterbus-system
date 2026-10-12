import Icon from './Icon'
import { t } from '../../i18n'

export interface FeatureStripItem {
  icon: string
  title: string
  description: string
}

interface FeatureStripProps {
  items: FeatureStripItem[]
  /** `cards` = separate white cards; `panel` = one tinted panel with dividers. */
  variant?: 'cards' | 'panel'
  className?: string
}

/** Row of small icon + title + text cues used at the bottom of the booking pages. */
export default function FeatureStrip({
  items,
  variant = 'cards',
  className = '',
}: FeatureStripProps) {
  const panel = variant === 'panel'

  return (
    <div
      className={`grid grid-cols-1 md:grid-cols-3 ${
        panel
          ? 'rounded-2xl border border-outline-variant/40 bg-sand-light/70 md:divide-x md:divide-outline-variant/40'
          : 'gap-space-md'
      } ${className}`.trim()}
    >
      {items.map((item) => (
        <div
          key={item.title}
          className={`flex items-center gap-space-md p-space-md ${
            panel ? '' : 'rounded-2xl border border-[#E2ECEE] bg-white p-space-lg shadow-sm'
          }`}
        >
          <div
            className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-teal-flow ${
              panel ? 'bg-white shadow-sm' : 'border border-[#E2ECEE] bg-mist'
            }`}
          >
            <Icon name={item.icon} className="text-[24px]" />
          </div>
          <div>
            <div className="font-headline-sm text-base font-bold text-deep-river">
              {t(item.title)}
            </div>
            <p className="text-xs text-on-surface-variant">{t(item.description)}</p>
          </div>
        </div>
      ))}
    </div>
  )
}
