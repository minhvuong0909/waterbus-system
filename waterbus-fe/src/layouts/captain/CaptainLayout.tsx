import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '../../components/brand'
import AccountMenu from '../header/AccountMenu'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

/** Minimal shell for the captain's portal: used on board, so just a header and the content. */
export default function CaptainLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-mist">
      <header className="flex items-center justify-between gap-3 border-b border-outline-variant/40 bg-white px-4 py-3">
        <Link to={ROUTES.captain} aria-label={t('Captain portal home')}>
          <Logo className="h-8 w-auto" />
        </Link>
        <AccountMenu />
      </header>
      <main className="mx-auto w-full max-w-3xl p-4 sm:p-6">{children}</main>
    </div>
  )
}
