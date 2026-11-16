import { Link } from 'react-router-dom'
import { Logo } from '../../../components/brand'
import { Icon } from '../../../components/ui'
import AccountMenu from '../../../layouts/header/AccountMenu'
import LanguageSwitcher from '../../../layouts/header/LanguageSwitcher'
import { ROUTES } from '../../../routes/routes'

/** Thanh đầu trang của Captain: cùng kiểu thanh đầu trang của khu điều hành (nền trắng, ngôn ngữ, menu tài khoản), không đồng hồ. */
export default function CaptainHeader({ connected, fresh }: { connected: boolean; fresh: boolean }) {
  return (
    <header className="flex min-h-[68px] items-center justify-between gap-3 border-b border-outline-variant/30 bg-white px-4 py-3 sm:px-6 lg:px-8">
      <div className="flex min-w-0 flex-1 items-center gap-3 text-sm">
        <Link to={ROUTES.captain} aria-label="Smart Waterbus — trang Captain" className="shrink-0">
          <Logo className="h-8 w-auto" />
        </Link>
        <span className="hidden text-outline-variant sm:inline">/</span>
        <span className="truncate font-semibold text-deep-river">Trung tâm vận hành tàu</span>
      </div>
      <div className="flex shrink-0 items-center gap-2" aria-live="polite">
        <span className={`captain-pill ${fresh ? 'is-success' : 'is-warning'}`}>
          <Icon name={fresh ? 'location_on' : 'location_off'} className="text-lg" />
          <span className="hidden lg:inline">{fresh ? 'GPS trực tiếp' : 'GPS chưa cập nhật'}</span>
        </span>
        <span className={`captain-pill ${connected ? 'is-success' : 'is-warning'}`}>
          <Icon name={connected ? 'check' : 'warning'} className="text-lg" />
          <span className="hidden lg:inline">{connected ? 'Đã kết nối' : 'Đang kết nối lại'}</span>
        </span>
        <LanguageSwitcher alwaysVisible />
        <AccountMenu />
      </div>
    </header>
  )
}
