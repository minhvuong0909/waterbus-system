import { Link, useLocation } from 'react-router-dom'
import { Icon } from '../../components/ui'
import { STAFF_ROLES } from '../../constant/roles'
import { hasAnyRole, isPassenger } from '../../features/authentication/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { usePopoverList } from '../../hooks/usePopoverList'
import { t } from '../../i18n'
import { landingRouteFor } from '../../routes/landingRoute'
import { ROUTES } from '../../routes/routes'

const itemClass =
  'flex min-h-[44px] w-full items-center gap-3 rounded-xl px-3 text-left text-sm font-medium text-deep-river transition-colors hover:bg-mist'

/**
 * Menu tài khoản dùng chung cho thanh đầu trang của khách và của khu điều hành: tên người dùng, vai trò, đường
 * tắt đổi khu (trang khách ⇄ điều hành) và đăng xuất ở một chỗ cố định, nên hai khu trông và hoạt động giống nhau.
 */
export default function AccountMenu() {
  const { user, signOut } = useAuth()
  const { pathname } = useLocation()
  const { root, open, setOpen, toggle } = usePopoverList(280)
  if (!user) return null

  const staff = hasAnyRole(user, STAFF_ROLES)
  const inStaffArea = pathname.startsWith(ROUTES.admin) || pathname.startsWith(ROUTES.captain)
  const initial = user.fullName?.trim().charAt(0).toUpperCase() || '?'

  return (
    <div ref={root} className="relative">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('Account menu')}
        onClick={toggle}
        className="flex min-h-[44px] items-center gap-2 rounded-full border border-outline-variant/40 bg-white py-1 pl-1 pr-2.5 shadow-xs transition-colors hover:bg-mist sm:pr-3"
      >
        <span
          aria-hidden="true"
          className="flex h-9 w-9 items-center justify-center rounded-full bg-teal-flow/15 text-sm font-bold text-teal-flow"
        >
          {initial}
        </span>
        <span className="hidden max-w-[9rem] truncate text-sm font-semibold text-deep-river md:inline">
          {user.fullName}
        </span>
        <Icon name="expand_more" className={`text-lg text-outline transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 top-full z-50 mt-2 w-64 rounded-2xl border border-outline-variant/40 bg-white p-1.5 shadow-lg"
        >
          <div className="px-3 pb-2 pt-2">
            <div className="truncate text-sm font-bold text-deep-river" title={user.fullName}>
              {user.fullName}
            </div>
            <div className="truncate text-xs text-on-surface-variant" title={user.email}>
              {user.email}
            </div>
            <div className="mt-1 text-2xs font-semibold uppercase tracking-wide text-teal-flow">
              {user.roles.join(' · ')}
            </div>
          </div>
          <div className="my-1 border-t border-surface-container" />

          {isPassenger(user) && (
            <Link role="menuitem" to={ROUTES.profile} className={itemClass} onClick={() => setOpen(false)}>
              <Icon name="person" className="text-xl text-outline" />
              {t('My profile')}
            </Link>
          )}
          {staff && (
            <Link
              role="menuitem"
              to={inStaffArea ? ROUTES.home : landingRouteFor(user.roles)}
              className={itemClass}
              onClick={() => setOpen(false)}
            >
              <Icon name={inStaffArea ? 'arrow_back' : 'dashboard'} className="text-xl text-outline" />
              {inStaffArea ? t('Passenger site') : t('Operations')}
            </Link>
          )}
          <button
            type="button"
            role="menuitem"
            className={`${itemClass} text-coral-glow`}
            onClick={() => {
              setOpen(false)
              signOut()
            }}
          >
            <Icon name="logout" className="text-xl" />
            {t('Sign out')}
          </button>
        </div>
      )}
    </div>
  )
}
