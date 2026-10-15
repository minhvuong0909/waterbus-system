import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function DiscoverBanner() {
  return (
    <div className="relative flex min-h-[220px] items-center overflow-hidden rounded-2xl bg-deep-river p-space-xl text-on-primary shadow-sm">
      <img
        alt=""
        aria-hidden="true"
        src={vesselImage}
        className="absolute inset-0 h-full w-full object-cover opacity-45"
      />
      <div className="absolute inset-0 bg-gradient-to-r from-deep-river via-deep-river/85 to-transparent" />
      <div className="relative z-10 flex w-full flex-col justify-between gap-space-lg md:flex-row md:items-center">
        <div className="max-w-md">
          <span className="text-[11px] font-bold uppercase tracking-wider text-sky-aqua">
            {t('Waterway Perspective')}
          </span>
          <h2 className="mt-1 font-headline-sm text-3xl font-extrabold leading-tight tracking-tight">
            {t('Discover Saigon from the River')}
          </h2>
          <p className="mt-2 text-sm text-sand-light/90">
            {t(
              'Experience the city skyline from panoramic catamarans with curated audio guides and sunset sailings.',
            )}
          </p>
        </div>
        <Link
          to={ROUTES.explore}
          className="inline-flex items-center justify-center gap-2 self-start rounded-full bg-white px-6 py-3 font-headline-sm text-sm font-bold text-deep-river shadow-md transition-all hover:bg-sand-light md:mr-12 md:self-center"
        >
          {t('Explore experiences')}
          <Icon name="arrow_forward" className="text-[18px]" />
        </Link>
      </div>
    </div>
  )
}
