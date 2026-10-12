import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Story } from '../types'
import { t } from '../../../i18n'

interface StoriesSectionProps {
  stories: Story[]
}

export default function StoriesSection({ stories }: StoriesSectionProps) {
  return (
    <section className="w-full bg-surface-container-lowest py-space-3xl">
      <div className="mx-auto max-w-7xl space-y-space-2xl px-margin">
        <div className="max-w-2xl space-y-space-xs">
          <span className="block text-body-sm font-bold uppercase tracking-wider text-coral-glow">
            {t('River Stories & Destinations')}
          </span>
          <h2 className="font-headline-lg text-headline-lg font-bold tracking-tight text-deep-river">
            {t('A Different Perspective on Saigon’s Skyline & Heritage')}
          </h2>
          <p className="text-body-md text-on-surface-variant">
            {t(
              'Immerse yourself in centuries of river commerce, shimmering night architecture, and peaceful winding canals away from crowded streets.',
            )}
          </p>
        </div>

        <div className="grid grid-cols-1 gap-space-xl md:grid-cols-3">
          {stories.map((story) => (
            <Link
              key={story.id}
              to={ROUTES.explore}
              className="group flex flex-col overflow-hidden rounded-panel border border-outline-variant/30 bg-surface shadow-sm transition-all duration-300 hover:shadow-xl"
            >
              <div className="relative h-72 w-full overflow-hidden bg-deep-river/10">
                <div
                  role="img"
                  aria-label={story.title}
                  className="h-full w-full bg-cover bg-center transition-transform duration-700 group-hover:scale-105"
                  style={{ backgroundImage: `url(${story.image})` }}
                />
                <div className="absolute left-4 top-4 flex items-center gap-1.5 rounded-full border border-white/10 bg-deep-river/85 px-3 py-1 text-xs font-medium text-on-primary backdrop-blur-md">
                  <Icon name={story.badgeIcon} className={`text-[15px] ${story.badgeIconClass}`} />
                  <span>{story.badge}</span>
                </div>
                <div className="absolute bottom-4 right-4 rounded-full bg-surface/90 px-3.5 py-1 font-numeric-md text-sm font-bold text-deep-river shadow-sm backdrop-blur-md">
                  {t('From {price}', { price: formatVnd(story.fromPriceVnd) })}
                </div>
              </div>
              <div className="flex flex-1 flex-col justify-between space-y-space-md p-space-lg">
                <div className="space-y-space-sm">
                  <div className="flex items-center gap-2 text-xs text-on-surface-variant">
                    <span className="flex items-center gap-1">
                      <Icon name="timer" className="text-[15px] text-teal-flow" />{' '}
                      {t('{durationMins} mins', { durationMins: story.durationMins })}
                    </span>
                    <span>•</span>
                    <span className="flex items-center gap-1">
                      <Icon name="pin_drop" className="text-[15px] text-teal-flow" />{' '}
                      {story.location}
                    </span>
                  </div>
                  <h3 className="font-headline-sm text-headline-sm font-bold text-deep-river transition-colors group-hover:text-teal-flow">
                    {story.title}
                  </h3>
                  <p className="text-body-md text-on-surface-variant">{story.description}</p>
                </div>
                <div className="flex items-center justify-between border-t border-surface-container pt-3 text-body-md font-semibold text-teal-flow">
                  <span className="inline-flex items-center gap-1 transition-transform group-hover:translate-x-1">
                    {t('Explore Tour')}
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </span>
                  <Icon
                    name={story.trailingIcon}
                    className={`text-[20px] ${story.trailingIconClass}`}
                  />
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  )
}
