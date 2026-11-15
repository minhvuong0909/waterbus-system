import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '../../../components/brand'
import { Reveal } from '../../../components/motion'
import { Icon } from '../../../components/ui'
import loginPhoto from '../../../assets/images/auth-login.jpg'
import { t } from '../../../i18n'
import LanguageSwitcher from '../../../layouts/header/LanguageSwitcher'
import { ROUTES } from '../../../routes/routes'

const PERKS = [
  'Book without an account',
  'Choose your seat for the part of the route you ride',
  'One QR code per booking, scanned at your boarding pier',
]

/**
 * Trang đăng nhập, đăng ký và xác minh email: toàn màn hình, không có thanh đầu trang và chân trang của web
 * (để khách tập trung vào form). Ảnh tàu bên trái (trên điện thoại là dải mỏng ở trên), form bên phải; logo
 * và "Về trang chủ" là lối thoát. Ba trang dùng chung nên trông đồng bộ.
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main id="main-content" className="grid min-h-svh bg-mist lg:grid-cols-[1.05fr_1fr]">
      <aside className="relative isolate m-3 flex h-44 flex-col justify-between overflow-hidden rounded-3xl bg-deep-river p-5 text-white sm:h-56 sm:p-8 lg:m-4 lg:h-auto lg:p-12">
        <img src={loginPhoto} alt="" className="auth-photo absolute inset-0 -z-10 h-full w-full object-cover" />
        <div
          aria-hidden
          className="absolute inset-0 -z-10 bg-gradient-to-t from-deep-river/90 via-deep-river/40 to-deep-river/20 lg:via-deep-river/30 lg:to-deep-river/30"
        />
        <div className="mt-auto">
          <p className="font-display text-2xl font-extrabold leading-tight tracking-tight drop-shadow-[0_2px_12px_rgba(13,37,56,0.5)] sm:text-3xl lg:text-4xl xl:text-5xl">
            <span className="block">{t('A Smarter Journey,')}</span>
            <span className="block text-sky-aqua">{t('A Brighter Saigon')}</span>
          </p>
          <ul className="mt-space-lg hidden space-y-2.5 text-sm text-white/90 lg:block">
            {PERKS.map((perk) => (
              <li key={perk} className="flex items-start gap-2.5">
                <Icon name="check" className="mt-0.5 text-base text-sky-aqua" />
                {t(perk)}
              </li>
            ))}
          </ul>
        </div>
      </aside>

      <div className="flex flex-col px-5 pb-5 pt-2 sm:px-10 lg:py-8">
        <div className="flex items-center justify-between">
          <Link to={ROUTES.home} aria-label={t('Smart Waterbus home')} className="shrink-0">
            <Logo className="h-8 w-auto sm:h-9" />
          </Link>
          <LanguageSwitcher alwaysVisible />
        </div>
        <div className="flex flex-1 items-start justify-center py-space-xl lg:items-center">
          <Reveal className="w-full max-w-md">
            <div data-reveal className="rounded-3xl bg-white p-6 shadow-card sm:p-10">
              {children}
            </div>
          </Reveal>
        </div>
      </div>
    </main>
  )
}
