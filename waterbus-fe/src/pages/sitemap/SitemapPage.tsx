import { Link } from 'react-router-dom'
import { Card } from '../../components/ui'
import { useDocumentTitle } from '../../hooks'
import { SITEMAP_ENTRIES } from '../../routes/routes'
import { t } from '../../i18n'

export default function SitemapPage() {
  useDocumentTitle('All routes')

  return (
    <section className="mx-auto max-w-4xl px-margin py-space-2xl">
      <h1 className="font-headline-lg text-headline-lg text-deep-river">{t('All routes')}</h1>
      <p className="mt-space-sm text-body-lg text-on-surface-variant">
        {t('Every page is reachable directly by URL, with no login or earlier step required.')}
      </p>
      <Card className="mt-space-xl overflow-hidden">
        <ul className="divide-y divide-outline-variant/30">
          {SITEMAP_ENTRIES.map((entry) => (
            <li key={entry.path}>
              <Link
                to={entry.path}
                className="flex items-center justify-between gap-space-sm px-space-lg py-space-md transition-colors hover:bg-mist"
              >
                <span className="font-headline-sm text-body-lg font-semibold text-deep-river">
                  {entry.title}
                </span>
                <code className="text-body-sm text-on-surface-variant">{entry.path}</code>
              </Link>
            </li>
          ))}
        </ul>
      </Card>
    </section>
  )
}
