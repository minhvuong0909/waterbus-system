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
    <section id="tours" className="w-full scroll-mt-20 bg-surface-container-lowest py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-8 px-5 sm:px-8">
        <div data-reveal className="max-w-2xl space-y-space-xs">
          <h2 className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]">
            {t('A Different Perspective on Saigon’s Skyline & Heritage')}
          </h2>
        </div>

        <div className="grid grid-cols-1 gap-space-xl md:grid-cols-3">
          {stories.map((story) => (
            <Link
              data-reveal
              key={story.id}
              to={ROUTES.explore}
              className="hover-lift group flex flex-col overflow-hidden rounded-panel border border-outline-variant/30 bg-surface shadow-sm"
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
                {story.fromPriceVnd !== undefined && (
                  <div className="absolute bottom-4 right-4 rounded-full bg-surface/90 px-3.5 py-1 font-numeric-md text-sm font-bold text-deep-river shadow-sm backdrop-blur-md">
                    {t('From {price}', { price: formatVnd(story.fromPriceVnd) })}
                  </div>
                )}
              </div>
              <div className="flex flex-1 flex-col justify-between space-y-space-md p-space-lg">
                <div className="space-y-space-sm">
                  <div className="flex items-center gap-2 text-xs text-on-surface-variant">
                    {story.durationMins !== undefined && (
                      <>
                        <span className="flex items-center gap-1">
                          <Icon name="timer" className="text-[15px] text-teal-flow" />{' '}
                          {t('{durationMins} mins', { durationMins: story.durationMins })}
                        </span>
                        <span>•</span>
                      </>
                    )}
                    <span className="flex items-center gap-1">
                      <Icon name="pin_drop" className="text-[15px] text-teal-flow" />{' '}
                      {story.location}
                    </span>
                  </div>
                  <h3 className="font-headline-sm text-headline-sm font-bold text-deep-river transition-colors group-hover:text-teal-flow">
                    {story.title}
                  </h3>
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
