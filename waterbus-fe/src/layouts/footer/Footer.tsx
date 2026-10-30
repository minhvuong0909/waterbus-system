import { Logo } from '../../components/brand'
import { CONTACT } from '../../constant/contact'
import { t } from '../../i18n'

/** Slim footer: brand and how to reach the operator; the navigation stays in the header. */
export default function Footer() {
  return (
    <footer className="w-full border-t border-outline-variant/30 bg-white print:hidden">
      <div className="mx-auto max-w-7xl px-5 py-8 sm:px-8 sm:py-10">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
          <Logo className="h-8 w-auto" />

          <div className="space-y-3 text-sm leading-relaxed text-on-surface-variant">
            <p>
              <span className="font-semibold text-deep-river">{t('Operational Hours')}:</span>{' '}
              {t(CONTACT.hours)}
            </p>
            <p>
              <span className="font-semibold text-deep-river">{t('Support Hotline')}:</span>{' '}
              <a href={CONTACT.hotlineHref} className="tap-area hover:text-deep-river">
                {CONTACT.hotline}
              </a>
            </p>
          </div>
        </div>

        <p className="mt-space-lg max-w-3xl text-xs leading-relaxed text-on-surface-variant">
          {t(
            'Refunds: if a trip is cancelled or ends early, the part you paid for that was not served is refunded in full. A ticket missed at check-in is not refunded.',
          )}
        </p>

        <p className="mt-space-md border-t border-outline-variant/30 pt-space-md text-body-sm text-on-surface-variant">
          {t('© {year} Smart Waterbus. River Connects Greater Stories.', {
            year: new Date().getFullYear(),
          })}
        </p>
      </div>
    </footer>
  )
}
