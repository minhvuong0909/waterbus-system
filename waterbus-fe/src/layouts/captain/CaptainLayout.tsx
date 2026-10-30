import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

/** Minimal shell for the captain's portal: used on board, so just a header and the content. */
export default function CaptainLayout({ children }: { children: ReactNode }) {
  const { user, signOut } = useAuth()
  return (
    <div className="min-h-screen bg-mist">
      <header className="flex items-center justify-between gap-3 border-b border-outline-variant/40 bg-white px-4 py-3">
        <Link to={ROUTES.captain} aria-label={t('Captain portal home')}>
          <Logo className="h-8 w-auto" />
        </Link>
        <div className="flex items-center gap-3 text-sm">
          <span className="hidden font-semibold text-deep-river sm:inline">{user?.fullName}</span>
          <button
            type="button"
            onClick={signOut}
            className="rounded-full bg-deep-river px-4 py-1.5 text-xs font-semibold text-on-primary"
          >
            {t('Sign out')}
          </button>
        </div>
      </header>
      <main className="mx-auto w-full max-w-3xl p-4 sm:p-6">{children}</main>
    </div>
  )
}
