import { Card, Icon } from '../../components/ui'
import { CONTACT } from '../../constant/contact'
import { FAQ, HELP_HIGHLIGHTS } from '../../content/help'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

export default function HelpPage() {
  useDocumentTitle('Help & About')

  return (
    <div className="mx-auto w-full max-w-5xl px-margin pb-space-3xl pt-space-xl">
      <header className="max-w-2xl space-y-space-xs">
        <span className="text-body-sm font-bold uppercase tracking-wider text-teal-flow">
          {t('About Smart Waterbus')}
        </span>
        <h1 className="font-display text-headline-lg font-bold tracking-tight text-deep-river">
          {t('Help & About')}
        </h1>
        <p className="text-body-lg text-on-surface-variant">
          {t(
            'Smart Waterbus is an electric river bus service for Saigon, with online booking, QR e-tickets and live trip status.',
          )}
        </p>
      </header>

      <ul className="mt-space-xl grid grid-cols-1 gap-space-md md:grid-cols-3">
        {HELP_HIGHLIGHTS.map((h) => (
          <li key={h.title}>
            <Card className="h-full space-y-space-sm p-space-lg">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-mist text-teal-flow">
                <Icon name={h.icon} className="text-2xl" />
              </span>
              <h2 className="font-display text-headline-sm font-bold text-deep-river">
                {t(h.title)}
              </h2>
              <p className="text-body-md text-on-surface-variant">{t(h.description)}</p>
            </Card>
          </li>
        ))}
      </ul>

      <div className="mt-space-2xl grid grid-cols-1 items-start gap-space-xl lg:grid-cols-3">
        <section className="space-y-space-md lg:col-span-2" aria-labelledby="faq-title">
          <h2
            id="faq-title"
            className="font-display text-headline-md font-bold text-deep-river"
          >
            {t('Frequently Asked Questions')}
          </h2>
          <Card className="divide-y divide-outline-variant/30">
            {FAQ.map((item) => (
              <details key={item.question} className="group px-space-lg py-space-md">
                <summary className="-my-3 flex cursor-pointer list-none items-center justify-between gap-space-md font-display text-body-lg font-semibold text-deep-river [&::-webkit-details-marker]:hidden py-3">
                  {t(item.question)}
                  <Icon
                    name="expand_more"
                    className="shrink-0 text-2xl text-teal-flow transition-transform group-open:rotate-180"
                  />
                </summary>
                <p className="pt-space-sm text-body-md text-on-surface-variant">{t(item.answer)}</p>
              </details>
            ))}
          </Card>
        </section>

        <aside className="space-y-space-md" aria-label={t('Support & Contact')}>
          <Card className="space-y-space-sm p-space-lg">
            <h2 className="font-display text-headline-sm font-bold text-deep-river">
              {t('Support & Contact')}
            </h2>
            <p className="text-body-sm font-semibold text-deep-river">{t('Operational Hours')}</p>
            <p className="text-body-md text-on-surface-variant">{t(CONTACT.hours)}</p>
            <p className="pt-space-xs text-body-sm font-semibold text-deep-river">
              {t('Support Hotline')}
            </p>
            <a
              href={CONTACT.hotlineHref}
              className="tap-area inline-flex items-center gap-2 text-body-md font-semibold text-teal-flow hover:underline"
            >
              <Icon name="call" className="text-lg" />
              {CONTACT.hotline}
            </a>
          </Card>
        </aside>
      </div>
    </div>
  )
}
