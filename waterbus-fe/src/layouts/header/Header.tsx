import { useEffect, useRef, useState } from 'react'
import { usePrefersReducedMotion } from '../../hooks'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Button, Icon } from '../../components/ui'
import { landingRouteFor, STAFF_ROLES } from '../../features/admin/navigation'
import { hasAnyRole } from '../../features/authentication/authenticationUtils'
import { useAuth } from '../../features/authentication/hook/authenticationHook'
import LanguageSwitcher from './LanguageSwitcher'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const NAV_ITEMS = [
  { label: 'Timetable', to: ROUTES.searchResults },
  { label: 'Explore', to: ROUTES.explore },
  { label: 'My Tickets', to: ROUTES.myTickets },
  { label: 'Help', to: ROUTES.help },
]

const linkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'whitespace-nowrap text-body-md font-semibold text-teal-flow border-b-2 border-teal-flow py-2 transition-colors'
    : 'whitespace-nowrap text-body-md font-semibold text-on-surface-variant border-b-2 border-transparent py-2 hover:text-deep-river transition-colors'

export default function Header() {
  const [openPath, setOpenPath] = useState<string | null>(null)
  const menuButton = useRef<HTMLButtonElement>(null)
  const { pathname } = useLocation()
  const reducedMotion = usePrefersReducedMotion()
  const open = openPath === pathname
  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpenPath(null)
        menuButton.current?.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open])
  const { user, signOut } = useAuth()
  const staffArea = hasAnyRole(user, STAFF_ROLES) && user ? landingRouteFor(user.roles) : null

  return (
    <header className="fixed inset-x-0 top-0 z-50 print:hidden border-b border-surface-container/60 bg-mist/90 shadow-[0_2px_12px_rgba(13,37,56,0.03)] backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-3 px-4 sm:h-20 sm:px-6">
        <div className="flex min-w-0 items-center gap-6">
          <Link
            to={ROUTES.home}
            aria-label={t('Smart Waterbus home')}
            className="shrink-0"
            onClick={(event) => {
              // Đã ở trang chủ: không tải lại, chỉ cuộn mượt lên đầu trang.
              if (pathname === ROUTES.home) {
                event.preventDefault()
                window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' })
              }
            }}
          >
            <Logo className="h-8 w-auto sm:h-9" />
          </Link>
          <nav className="hidden items-center gap-4 xl:flex" aria-label={t('Main')}>
            {NAV_ITEMS.map((item) => (
              <NavLink key={item.label} to={item.to} className={linkClass}>
                {t(item.label)}
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="flex items-center gap-2 sm:gap-3">
          <LanguageSwitcher />
          {user ? (
            <>
              <span
                className="hidden max-w-[80px] truncate text-body-md font-medium text-deep-river 2xl:inline"
                title={user.email}
              >
                {user.fullName}
              </span>
              {staffArea && (
                <Button
                  to={staffArea}
                  variant="secondary"
                  className="hidden whitespace-nowrap sm:inline-flex"
                >
                  {t('Operations')}
                </Button>
              )}
              <Button
                variant="secondary"
                onClick={signOut}
                className="hidden whitespace-nowrap sm:inline-flex"
              >
                {t('Sign out')}
              </Button>
            </>
          ) : (
            <Button
              to={ROUTES.signIn}
              variant="secondary"
              className="hidden whitespace-nowrap sm:inline-flex"
            >
              {t('Sign in')}
            </Button>
          )}
          <Button to={ROUTES.search} className="whitespace-nowrap !px-4 sm:!px-6">
            {t('Book a Trip')}
          </Button>
          <button
            ref={menuButton}
            type="button"
            aria-label={t('Toggle menu')}
            aria-expanded={open}
            aria-controls="mobile-navigation"
            onClick={() => setOpenPath(open ? null : pathname)}
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-deep-river transition-colors hover:bg-surface-container xl:hidden"
          >
            <Icon name={open ? 'close' : 'menu'} />
          </button>
        </div>
      </div>

      {open && (
        <nav
          id="mobile-navigation"
          className="max-h-[calc(100dvh-4rem)] overflow-y-auto border-t border-surface-container/60 bg-mist px-4 py-space-md shadow-lg sm:max-h-[calc(100dvh-5rem)] sm:px-6 xl:hidden"
          aria-label={t('Mobile')}
        >
          <ul className="flex flex-col gap-space-sm">
            {NAV_ITEMS.map((item) => (
              <li key={item.label}>
                <NavLink
                  to={item.to}
                  onClick={() => setOpenPath(null)}
                  className="block rounded-lg px-space-md py-space-sm text-body-lg font-medium text-deep-river hover:bg-surface-container"
                >
                  {t(item.label)}
                </NavLink>
              </li>
            ))}
            <li className="pt-space-sm">
              <LanguageSwitcher variant="inline" />
            </li>
            <li className="grid grid-cols-2 gap-space-sm pt-space-sm">
              {staffArea && (
                <Button to={staffArea} variant="secondary" onClick={() => setOpenPath(null)}>
                  {t('Operations')}
                </Button>
              )}
              {user ? (
                <Button
                  variant="secondary"
                  onClick={() => {
                    signOut()
                    setOpenPath(null)
                  }}
                >
                  {t('Sign out')}
                </Button>
              ) : (
                <Button to={ROUTES.signIn} variant="secondary" onClick={() => setOpenPath(null)}>
                  {t('Sign in')}
                </Button>
              )}
              <Button
                to={ROUTES.search}
                className={staffArea ? 'col-span-2' : ''}
                onClick={() => setOpenPath(null)}
              >
                {t('Book a Trip')}
              </Button>
            </li>
          </ul>
        </nav>
      )}
    </header>
  )
}
