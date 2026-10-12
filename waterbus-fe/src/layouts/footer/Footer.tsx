import { Link } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Icon } from '../../components/ui'
import { CONTACT } from '../../constant/contact'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const SOCIALS = [
  { label: 'River radar', icon: 'radar' },
  { label: 'Navigation map', icon: 'explore' },
  { label: 'Live alerts channel', icon: 'notifications_active' },
  { label: 'Pier terminal hub', icon: 'anchor' },
]

const QUICK_LINKS = [
  { label: 'Routes & Lines', to: ROUTES.search },
  { label: 'Timetable & Headways', to: ROUTES.searchResults },
  { label: 'Pier Terminals & Amenities', to: ROUTES.explore },
  { label: 'Help & About', to: ROUTES.help },
]

const EXPERIENCES = [
  'Sunset Golden Hour Cruise',
  'Historical River Tour',
  'Heritage Audio Guide',
  'Charter & Group Escapes',
]

const LEGAL = ['Passenger Rights', 'Privacy Policy', 'Waterway Transit Rules', 'Accessibility']

export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant/30 bg-surface-container-low">
      <div className="mx-auto max-w-7xl px-margin pb-space-2xl pt-space-3xl">
        <div className="mb-space-3xl flex flex-col items-start justify-between gap-space-md rounded-2xl border border-outline-variant/30 bg-surface p-space-lg shadow-sm lg:flex-row lg:items-center">
          <div className="flex items-center gap-space-md">
            <span className="relative flex h-3 w-3">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-teal-flow opacity-75" />
              <span className="relative inline-flex h-3 w-3 rounded-full bg-teal-flow" />
            </span>
            <div>
              <span className="block font-headline-sm text-headline-sm font-bold text-deep-river">
                {t('River Corridor Status: Normal Service')}
              </span>
              <span className="text-body-sm text-on-surface-variant">
                {t(
                  'All 18 river stations operational • Average headway 12 mins • Water condition: Calm',
                )}
              </span>
            </div>
          </div>
          <span className="rounded-full border border-teal-flow/20 bg-sand-light px-space-md py-1.5 text-body-sm font-semibold text-teal-flow">
            {t('Waterway Patrol Active')}
          </span>
        </div>

        <div className="grid grid-cols-1 gap-space-2xl pb-space-3xl md:grid-cols-2 lg:grid-cols-5">
          <div className="space-y-space-md lg:col-span-2 lg:pr-space-xl">
            <Logo className="h-8 w-auto" />
            <p className="max-w-sm text-body-md text-on-surface-variant">
              {t(
                'Pioneering high-frequency electric river transit and immersive city voyages. Experience sustainable urban waterways crafted for commuters and explorers alike.',
              )}
            </p>
            <div className="flex items-center gap-space-md pt-space-xs">
              {SOCIALS.map((s) => (
                <button
                  key={s.icon}
                  type="button"
                  aria-label={t(s.label)}
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-outline-variant/30 bg-surface text-on-surface-variant transition-colors hover:bg-mist hover:text-teal-flow"
                >
                  <Icon name={s.icon} className="text-[18px]" />
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-space-md">
            <span className="block font-headline-sm text-body-lg font-bold text-deep-river">
              {t('Quick Links')}
            </span>
            <ul className="space-y-space-sm text-body-md text-on-surface-variant">
              {QUICK_LINKS.map((l) => (
                <li key={l.label}>
                  <Link to={l.to} className="transition-colors hover:text-deep-river">
                    {t(l.label)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="space-y-space-md">
            <span className="block font-headline-sm text-body-lg font-bold text-deep-river">
              {t('Tourism Experiences')}
            </span>
            <ul className="space-y-space-sm text-body-md text-on-surface-variant">
              {EXPERIENCES.map((label) => (
                <li key={label}>
                  <Link to={ROUTES.explore} className="transition-colors hover:text-deep-river">
                    {t(label)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="space-y-space-md">
            <span className="block font-headline-sm text-body-lg font-bold text-deep-river">
              {t('Support & Contact')}
            </span>
            <div className="space-y-space-xs text-body-md text-on-surface-variant">
              <p className="font-semibold text-deep-river">{t('Operational Hours')}</p>
              <p className="text-body-sm">{t(CONTACT.hours)}</p>
              <p className="pt-space-xs font-semibold text-deep-river">{t('Dispatch Hotline')}</p>
              <a href={CONTACT.hotlineHref} className="text-body-sm hover:text-deep-river">
                {CONTACT.hotline}
              </a>
            </div>
            <div className="inline-flex items-center gap-space-sm rounded-xl border border-outline-variant/30 bg-surface px-space-md py-space-xs">
              <Icon name="qr_code_2" className="text-[20px] text-teal-flow" />
              <span className="text-body-sm font-semibold text-deep-river">
                {t('Transit App Scanner')}
              </span>
            </div>
          </div>
        </div>

        <div className="flex flex-col items-center justify-between gap-space-md border-t border-outline-variant/30 pt-space-xl text-body-sm text-on-surface-variant md:flex-row">
          <p>{t('© 2025 Smart Waterbus. River Connects Greater Stories.')}</p>
          <div className="flex flex-wrap items-center justify-center gap-space-lg font-medium">
            {LEGAL.map((label) => (
              <button key={label} type="button" className="transition-colors hover:text-deep-river">
                {t(label)}
              </button>
            ))}
          </div>
        </div>
      </div>
    </footer>
  )
}
