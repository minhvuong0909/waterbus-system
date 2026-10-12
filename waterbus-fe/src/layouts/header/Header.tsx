import { useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { Logo } from '../../components/brand'
import { Button, Icon } from '../../components/ui'
import LanguageSwitcher from './LanguageSwitcher'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const NAV_ITEMS = [
  { label: 'Timetable', to: ROUTES.searchResults },
  { label: 'Explore', to: ROUTES.explore },
  { label: 'Live Tracking', to: ROUTES.liveTracking },
  { label: 'My Tickets', to: ROUTES.myTickets },
  { label: 'Help', to: ROUTES.help },
]

const linkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'whitespace-nowrap text-body-lg font-semibold text-teal-flow border-b-2 border-teal-flow pb-0.5 transition-colors'
    : 'whitespace-nowrap text-body-md font-medium text-on-surface-variant hover:text-deep-river transition-colors'

export default function Header() {
  const [open, setOpen] = useState(false)

  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-surface-container/60 bg-mist/90 shadow-[0_2px_12px_rgba(13,37,56,0.03)] backdrop-blur-xl">
      <div className="mx-auto flex h-20 max-w-7xl items-center justify-between gap-space-lg px-margin">
        <div className="flex items-center gap-space-xl">
          <Link to={ROUTES.home} aria-label={t('Smart Waterbus home')}>
            <Logo className="h-9 w-auto" />
          </Link>
          <nav className="hidden items-center gap-space-lg xl:flex" aria-label={t('Main')}>
            {NAV_ITEMS.map((item) => (
              <NavLink key={item.label} to={item.to} className={linkClass}>
                {t(item.label)}
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="flex items-center gap-space-md">
          <Link
            to={ROUTES.search}
            aria-label={t('Search routes and piers')}
            className="flex h-10 w-10 items-center justify-center rounded-full text-on-surface-variant transition-all hover:bg-surface-container hover:text-deep-river"
          >
            <Icon name="search" className="text-[20px]" />
          </Link>
          <LanguageSwitcher />
          <Button
            to={ROUTES.signIn}
            variant="dark"
            className="hidden whitespace-nowrap sm:inline-flex"
          >
            {t('Sign in')}
          </Button>
          <Button to={ROUTES.search} className="hidden whitespace-nowrap md:inline-flex">
            {t('Book a Trip')}
          </Button>
          <button
            type="button"
            aria-label={t('Toggle menu')}
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
            className="flex h-10 w-10 items-center justify-center rounded-full text-deep-river hover:bg-surface-container xl:hidden"
          >
            <Icon name={open ? 'close' : 'menu'} />
          </button>
        </div>
      </div>

      {open && (
        <nav
          className="border-t border-surface-container/60 bg-mist px-margin py-space-md xl:hidden"
          aria-label={t('Mobile')}
        >
          <ul className="flex flex-col gap-space-sm">
            {NAV_ITEMS.map((item) => (
              <li key={item.label}>
                <NavLink
                  to={item.to}
                  onClick={() => setOpen(false)}
                  className="block rounded-lg px-space-md py-space-sm text-body-lg font-medium text-deep-river hover:bg-surface-container"
                >
                  {t(item.label)}
                </NavLink>
              </li>
            ))}
            <li className="pt-space-sm">
              <LanguageSwitcher variant="inline" />
            </li>
            <li className="flex gap-space-sm pt-space-sm">
              <Button to={ROUTES.signIn} variant="dark" onClick={() => setOpen(false)}>
                {t('Sign in')}
              </Button>
              <Button to={ROUTES.search} onClick={() => setOpen(false)}>
                {t('Book a Trip')}
              </Button>
            </li>
          </ul>
        </nav>
      )}
    </header>
  )
}
