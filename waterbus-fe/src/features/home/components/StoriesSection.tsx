import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import type { Landmark } from '../../explore/types'

/**
 * Giới thiệu vài địa danh có thuyết minh trên chuyến ngắm cảnh (cùng nguồn với trang Khám phá). Đây
 * là nội dung giới thiệu, không phải sản phẩm để bán: mỗi thẻ mở trang Khám phá.
 */
export default function StoriesSection({ places }: { places: Landmark[] }) {
  if (places.length === 0) return null
  return (
    <section id="tours" className="w-full scroll-mt-20 bg-surface-container-lowest py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-8 px-5 sm:px-8">
        <div
          data-reveal
          className="flex flex-col justify-between gap-space-sm md:flex-row md:items-end"
        >
          <div className="max-w-2xl space-y-space-xs">
            <h2 className="font-display text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-4xl">
              {t('Places you pass on the river')}
            </h2>
            <p className="text-body-md text-on-surface-variant">
              {t(
                'On sightseeing journeys, stories about the places you pass play over the boat’s speakers.',
              )}
            </p>
          </div>
          <Link
            to={ROUTES.explore}
            className="tap-area inline-flex shrink-0 items-center gap-0.5 text-sm font-bold text-teal-flow hover:underline"
          >
            {t('Explore along the river')}
            <Icon name="chevron_right" className="text-base" />
          </Link>
        </div>

        <div className="grid grid-cols-1 gap-space-xl md:grid-cols-3">
          {places.map((place) => (
            <Link
              data-reveal
              key={place.id}
              to={ROUTES.explore}
              className="hover-lift group flex flex-col overflow-hidden rounded-panel border border-outline-variant/30 bg-surface shadow-sm"
            >
              <div className="relative h-56 w-full overflow-hidden bg-deep-river/10">
                <div
                  role="img"
                  aria-label={place.title}
                  className="h-full w-full bg-cover bg-center transition-transform duration-700 group-hover:scale-105"
                  style={{ backgroundImage: `url(${place.image})` }}
                />
                <span className="absolute left-4 top-4 rounded-full border border-white/10 bg-deep-river/85 px-3 py-1 text-xs font-medium text-on-primary backdrop-blur-md">
                  {t(place.category)}
                </span>
              </div>
              <div className="flex-1 space-y-space-sm p-space-lg">
                <h3 className="font-display text-headline-sm font-bold text-deep-river transition-colors group-hover:text-teal-flow">
                  {place.title}
                </h3>
                <p className="line-clamp-3 text-sm text-on-surface-variant">{place.description}</p>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  )
}
