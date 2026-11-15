import { Link } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Icon } from '../../components/ui'
import { CONTACT } from '../../constant/contact'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { NAV_ITEMS } from '../navigation'

const HEADING = 'mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-teal-flow'
const LINK = 'tap-area inline-flex items-center text-sm text-on-surface-variant transition-colors hover:text-deep-river'

/** Footer: brand, the main pages and how to reach the operator. */
export default function Footer() {
  return (
    <footer className="w-full bg-white print:hidden">
      <div aria-hidden="true" className="h-1 bg-gradient-to-r from-teal-flow via-sky-aqua to-deep-river" />
      {/* Trên điện thoại, MainLayout đã chừa chỗ cho nút trợ lý nổi ở góc dưới. */}
      <div className="mx-auto max-w-7xl px-5 pb-6 pt-8 sm:px-8 sm:pt-10">
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1.2fr] lg:gap-12">
          <div>
            <Link to={ROUTES.home} aria-label={t('Smart Waterbus home')} className="inline-block">
              <Logo className="h-9 w-auto" />
            </Link>
            <p className="mt-3 max-w-xs font-display font-semibold text-xl leading-snug text-deep-river">
              {t('River Connects Greater Stories')}
            </p>
          </div>

          <nav aria-label={t('Footer')}>
            <h2 className={HEADING}>{t('Quick links')}</h2>
            <ul className="space-y-0.5">
              {NAV_ITEMS.map((item) => (
                <li key={item.to}>
                  <Link to={item.to} className={LINK}>
                    {t(item.label)}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>

          <div>
            <h2 className={HEADING}>{t('Support & Contact')}</h2>
            <ul className="space-y-3 text-sm text-on-surface-variant">
              <li className="flex items-start gap-3">
                <Icon name="schedule" className="mt-0.5 text-lg text-teal-flow" />
                <span>
                  <span className="block font-semibold text-deep-river">{t('Operational Hours')}</span>
                  {t(CONTACT.hours)}
                </span>
              </li>
              <li className="flex items-start gap-3">
                <Icon name="call" className="mt-0.5 text-lg text-teal-flow" />
                <span>
                  <span className="block font-semibold text-deep-river">{t('Support Hotline')}</span>
                  <a href={CONTACT.hotlineHref} className="tap-area hover:text-deep-river">
                    {CONTACT.hotline}
                  </a>
                </span>
              </li>
            </ul>
          </div>
        </div>

        <p className="mt-8 border-t border-outline-variant/30 pt-4 text-body-sm text-on-surface-variant">
          {t('© {year} Smart Waterbus. River Connects Greater Stories.', {
            year: new Date().getFullYear(),
          })}
        </p>
      </div>
    </footer>
  )
}
