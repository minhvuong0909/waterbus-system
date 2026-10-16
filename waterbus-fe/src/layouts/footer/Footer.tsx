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
    <footer className="w-full border-t border-outline-variant/30 bg-surface-container-low print:hidden">
      <div className="mx-auto max-w-7xl px-margin py-space-xl">
        <div className="grid grid-cols-1 gap-space-xl md:grid-cols-3">
          <Logo className="h-8 w-auto" />

          <ul className="space-y-space-xs text-body-md text-on-surface-variant">
            {QUICK_LINKS.map((l) => (
              <li key={l.label}>
                <Link to={l.to} className="transition-colors hover:text-deep-river">
                  {t(l.label)}
                </Link>
              </li>
            ))}
          </ul>

          <div className="space-y-space-xs text-body-md text-on-surface-variant">
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
