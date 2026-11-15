import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Icon } from '../../components/ui'
import { ROLES } from '../../constant/roles'
import AdminAlertBar from '../../features/operations/components/AdminAlertBar'
import { useOpenAlertSummary } from '../../features/operations/hooks/useOpenAlertSummary'
import { ADMIN_NAV, ADMIN_NAV_GROUPS } from '../../features/admin/navigation'
import { hasAnyRole } from '../../features/authentication/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import AccountMenu from '../header/AccountMenu'
import LanguageSwitcher from '../header/LanguageSwitcher'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-[36px] shrink-0 items-center gap-3 whitespace-nowrap rounded-xl px-3 py-1.5 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-teal-flow text-white shadow-sm'
      : 'text-white/65 hover:bg-white/10 hover:text-white'
  }`

/** Khung của khu điều hành: sidebar điều hướng (lọc theo vai trò) và cột nội dung. */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const items = ADMIN_NAV.filter((item) => hasAnyRole(user, item.roles))
  // Alerts are for the admin only (the incident endpoints are AdminOnly); a captain in this shell sees none.
  const alertSummary = useOpenAlertSummary(hasAnyRole(user, [ROLES.admin]))
  const { pathname } = useLocation()
  // Trang con như ghế của một tàu thuộc về mục đã mở nó.
  const current =
    items.find((item) => item.to === pathname) ??
    items.find((item) => item.to !== ROUTES.admin && pathname.startsWith(`${item.to}/`))
  const groups = ADMIN_NAV_GROUPS.map((group) => ({ group, links: items.filter((item) => item.group === group) })).filter(
    ({ links }) => links.length > 0,
  )
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
        <div className="flex items-center justify-between gap-3 px-4 py-3 lg:py-4">
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
            menuOpen ? 'block' : 'hidden'
          } min-h-0 px-3 pb-3 lg:block lg:flex-1 lg:overflow-y-auto lg:pt-1 [scrollbar-width:thin]`}
        >
          {groups.map(({ group, links }) => (
            <div key={group} className="mt-2.5 first:mt-0">
              <h2 className="px-3 pb-1 text-2xs font-semibold text-white/45">{t(group)}</h2>
              <div className="grid grid-cols-2 gap-1 lg:grid-cols-1">
                {links.map((item) => (
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
                    {item.to === ROUTES.adminIncidents && alertSummary && (
                      <span
                        aria-label={`${t('Open alerts')}: ${alertSummary.open}`}
                        className="ml-auto flex h-5 min-w-5 items-center justify-center rounded-full bg-coral-glow px-1.5 text-2xs font-bold text-white"
                      >
                        {alertSummary.open}
                      </span>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>
      </aside>
      <div className="min-w-0 flex-1 lg:ml-60">
        <header className="flex min-h-[68px] items-center justify-between gap-3 border-b border-outline-variant/30 bg-white px-4 py-3 sm:px-6 lg:px-8">
          <div className="flex min-w-0 flex-1 items-center gap-2 text-sm">
            <Icon name={current?.icon ?? 'dashboard'} className="text-xl text-teal-flow" />
            <span className="hidden text-on-surface-variant sm:inline">{t(current?.group ?? 'Operations')}</span>
            <span className="hidden text-outline-variant sm:inline">/</span>
            <span className="truncate font-semibold text-deep-river">
              {t(current?.label ?? 'Overview')}
            </span>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <LanguageSwitcher alwaysVisible />
            <AccountMenu />
          </div>
        </header>
        <main id="admin-content" className="mx-auto w-full max-w-[1600px] p-4 sm:p-6 lg:p-8">
          <AdminAlertBar summary={alertSummary} pathname={pathname} />
          {children}
        </main>
      </div>
    </div>
  )
}
