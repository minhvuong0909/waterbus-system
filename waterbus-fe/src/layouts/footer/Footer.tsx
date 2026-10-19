import { Link } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { CONTACT } from '../../constant/contact'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const QUICK_LINKS = [
  { label: 'Routes & Lines', to: ROUTES.search },
  { label: 'Timetable & Headways', to: ROUTES.searchResults },
  { label: 'Help & About', to: ROUTES.help },
]

/** Slim footer: brand, the few links people look for, and how to reach the operator. */
export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant/30 bg-white print:hidden">
      <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 sm:py-10">
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          <Logo className="h-8 w-auto" />

          <ul className="space-y-1 text-sm text-on-surface-variant">
            {QUICK_LINKS.map((l) => (
              <li key={l.label}>
                <Link
                  to={l.to}
                  className="inline-flex min-h-[36px] items-center transition-colors hover:text-teal-flow"
                >
                  {t(l.label)}
                </Link>
              </li>
            ))}
          </ul>

          <div className="space-y-3 text-sm leading-relaxed text-on-surface-variant sm:col-span-2 lg:col-span-1">
            <p>
              <span className="font-semibold text-deep-river">{t('Operational Hours')}:</span>{' '}
              {t(CONTACT.hours)}
            </p>
            <p>
              <span className="font-semibold text-deep-river">{t('Dispatch Hotline')}:</span>{' '}
              <a href={CONTACT.hotlineHref} className="hover:text-deep-river">
                {CONTACT.hotline}
              </a>
            </p>
          </div>
        </div>

        <p className="mt-space-xl border-t border-outline-variant/30 pt-space-md text-body-sm text-on-surface-variant">
          {t('© {year} Smart Waterbus. River Connects Greater Stories.', {
            year: new Date().getFullYear(),
          })}
        </p>
      </div>
    </footer>
  )
}
