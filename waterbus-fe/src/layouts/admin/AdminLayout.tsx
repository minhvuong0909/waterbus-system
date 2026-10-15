import type { ReactNode } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Icon } from '../../components/ui'
import { ADMIN_NAV } from '../../features/admin/navigation'
import { hasAnyRole } from '../../features/authentication/authenticationUtils'
import { useAuth } from '../../features/authentication/hook/authenticationHook'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import LanguageSwitcher from '../header/LanguageSwitcher'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-semibold transition-colors ${
    isActive
      ? 'bg-teal-flow/10 text-teal-flow'
      : 'text-on-surface-variant hover:bg-surface-container hover:text-deep-river'
  }`

/** Shell of the operations area: sidebar navigation (filtered by role) and a content column. */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const { user, signOut } = useAuth()
  const items = ADMIN_NAV.filter((item) => hasAnyRole(user, item.roles))

  return (
    <div className="min-h-screen bg-mist lg:flex">
      <aside className="border-b border-outline-variant/40 bg-white lg:fixed lg:inset-y-0 lg:flex lg:w-60 lg:flex-col lg:border-b-0 lg:border-r">
        <div className="flex items-center justify-between gap-3 px-4 py-3 lg:py-5">
          <Link to={ROUTES.admin} aria-label={t('Operations home')}>
            <Logo className="h-8 w-auto" />
          </Link>
          <span className="rounded bg-deep-river px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-on-primary">
            {t('Ops')}
          </span>
        </div>
        <nav
          aria-label={t('Operations')}
          className="flex gap-1 overflow-x-auto px-3 pb-3 lg:flex-1 lg:flex-col lg:overflow-visible lg:pb-0"
        >
          {items.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.to === ROUTES.admin} className={linkClass}>
              <Icon name={item.icon} className="text-[20px]" />
              {t(item.label)}
            </NavLink>
          ))}
        </nav>
        <div className="hidden space-y-2 border-t border-outline-variant/40 p-4 lg:block">
          <LanguageSwitcher variant="inline" />
          <div className="truncate text-sm font-semibold text-deep-river" title={user?.email}>
            {user?.fullName}
          </div>
          <div className="text-xs text-on-surface-variant">{user?.roles.join(', ')}</div>
          <div className="flex gap-2 pt-1 text-xs font-semibold">
            <Link to={ROUTES.home} className="text-teal-flow hover:underline">
              {t('Passenger site')}
            </Link>
            <span className="text-outline">•</span>
            <button type="button" onClick={signOut} className="text-teal-flow hover:underline">
              {t('Sign out')}
            </button>
          </div>
        </div>
      </aside>
      <main className="min-w-0 flex-1 p-4 sm:p-6 lg:ml-60 lg:p-8">{children}</main>
    </div>
  )
}
