import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Icon } from '../../components/ui'
import { ADMIN_NAV } from '../../features/admin/navigation'
import { hasAnyRole } from '../../features/authentication/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import LanguageSwitcher from '../header/LanguageSwitcher'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-[42px] shrink-0 items-center gap-3 whitespace-nowrap rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-teal-flow text-white shadow-sm'
      : 'text-white/65 hover:bg-white/10 hover:text-white'
  }`

/** Shell of the operations area: sidebar navigation (filtered by role) and a content column. */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const { user, signOut } = useAuth()
  const items = ADMIN_NAV.filter((item) => hasAnyRole(user, item.roles))
  const { pathname } = useLocation()
  // A sub-page such as the seats of a boat belongs to the section it was opened from.
  const current =
    items.find((item) => item.to === pathname) ??
    items.find((item) => item.to !== ROUTES.admin && pathname.startsWith(`${item.to}/`))
  const [menuPath, setMenuPath] = useState<string | null>(null)
  const menuOpen = menuPath === pathname
  const menuButton = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (!menuOpen) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuPath(null)
        menuButton.current?.focus()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [menuOpen])

  return (
    <div className="min-h-screen bg-mist lg:flex">
      <a
        href="#admin-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-xl focus:bg-white focus:px-4 focus:py-3 focus:text-deep-river"
      >
        {t('Skip to main content')}
      </a>
      <aside className="border-b border-white/10 bg-deep-river lg:fixed lg:inset-y-0 lg:z-30 lg:flex lg:w-60 lg:flex-col lg:border-b-0">
        <div className="flex items-center justify-between gap-3 px-4 py-3 lg:py-5">
          <Link
            to={ROUTES.admin}
            aria-label={t('Operations home')}
            className="rounded-xl bg-white px-3 py-2"
          >
            <Logo className="h-7 w-auto" />
          </Link>
          <button
            ref={menuButton}
            type="button"
            aria-label={t('Toggle menu')}
            aria-expanded={menuOpen}
            aria-controls="admin-navigation"
            onClick={() => setMenuPath(menuOpen ? null : pathname)}
            className="flex h-11 w-11 items-center justify-center rounded-xl border border-white/20 text-white transition-colors hover:bg-white/10 lg:hidden"
          >
            <Icon name={menuOpen ? 'close' : 'menu'} />
          </button>
        </div>
        <nav
          id="admin-navigation"
          aria-label={t('Operations')}
          className={`${
            menuOpen ? 'grid' : 'hidden'
          } min-h-0 grid-cols-2 gap-1.5 px-3 pb-3 lg:flex lg:flex-1 lg:flex-col lg:overflow-y-auto lg:pt-3 [scrollbar-width:thin]`}
        >
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === ROUTES.admin}
              title={t(item.label)}
              onClick={() => setMenuPath(null)}
              className={linkClass}
            >
              <Icon name={item.icon} className="shrink-0 text-xl" />
              <span className="min-w-0 whitespace-normal leading-snug lg:truncate lg:whitespace-nowrap">
                {t(item.label)}
              </span>
            </NavLink>
          ))}
        </nav>
        <div className="hidden border-t border-white/10 p-3 lg:block">
          <div className="rounded-2xl border border-white/10 bg-white/5 p-3">
            <div className="flex min-w-0 items-center gap-3">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-sky-aqua/15 text-base font-bold text-sky-aqua">
                {user?.fullName?.trim().charAt(0).toUpperCase()}
              </span>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-semibold text-white" title={user?.fullName}>{user?.fullName}</div>
                <div className="mt-1 truncate text-xs text-white/65">{user?.roles.join(', ')}</div>
              </div>
            </div>
            <div className="mt-3 grid gap-1 border-t border-white/10 pt-2">
              <Link to={ROUTES.home} className="flex min-h-[44px] min-w-0 items-center gap-2 rounded-xl px-2 text-xs font-medium text-white/80 transition-colors hover:bg-white/10 hover:text-white">
                <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4 shrink-0"><path d="M14 3h7v7M10 14 21 3M10 3H3v18h18v-7" /></svg>
                <span className="min-w-0 flex-1 whitespace-nowrap">{t('Passenger site')}</span>
              </Link>
              <button type="button" onClick={signOut} className="flex min-h-[44px] items-center gap-2 rounded-xl px-3 text-xs font-medium text-white/80 transition-colors hover:bg-coral-glow/15 hover:text-white">
                <Icon name="logout" className="shrink-0 text-lg" />{t('Sign out')}
              </button>
            </div>
          </div>
        </div>
      </aside>
      <div className="min-w-0 flex-1 lg:ml-60">
        <header className="flex min-h-[68px] items-center justify-between gap-3 border-b border-outline-variant/30 bg-white px-4 py-3 sm:px-6 lg:px-8">
          <div className="flex min-w-0 flex-1 items-center gap-2 text-sm">
            <Icon name={current?.icon ?? 'dashboard'} className="text-xl text-teal-flow" />
            <span className="hidden text-on-surface-variant sm:inline">{t('Operations')}</span>
            <span className="hidden text-outline-variant sm:inline">/</span>
            <span className="truncate font-semibold text-deep-river">
              {t(current?.label ?? 'Overview')}
            </span>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <LanguageSwitcher alwaysVisible />
            <Link
              to={ROUTES.home}
              aria-label={t('Passenger site')}
              title={t('Passenger site')}
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-outline-variant/40 text-on-surface-variant transition-colors hover:bg-mist"
            >
              <Icon name="open_in_new" className="text-lg" />
            </Link>
            <button
              type="button"
              aria-label={t('Sign out')}
              title={t('Sign out')}
              onClick={signOut}
              className="flex h-10 w-10 items-center justify-center rounded-xl border border-outline-variant/40 text-on-surface-variant hover:bg-mist lg:hidden"
            >
              <Icon name="logout" className="text-lg" />
            </button>
          </div>
        </header>
        <main id="admin-content" className="mx-auto w-full max-w-[1600px] p-4 sm:p-6 lg:p-8">
          {children}
        </main>
      </div>
    </div>
  )
}
