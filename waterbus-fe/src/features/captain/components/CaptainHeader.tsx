import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '../../../components/brand'
import Icon from './CaptainIcon'
import { useAuth } from '../../authentication/hooks/useAuth'
import { formatClock } from '../../../utils/time'
import { ROUTES } from '../../../routes/routes'

export default function CaptainHeader({
  connected,
  fresh,
}: {
  connected: boolean
  fresh: boolean
}) {
  const { user, signOut } = useAuth()
  const [now, setNow] = useState(new Date())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000)
    return () => window.clearInterval(timer)
  }, [])
  return (
    <header className="captain-header">
      <Link
        to={ROUTES.captain}
        className="captain-brand"
        aria-label="Smart Waterbus — trang Captain"
      >
        <Logo className="h-12 w-auto" />
      </Link>
      <span className="captain-header-divider" />
      <span className="captain-header-title">Trung tâm vận hành tàu</span>
      <div className="captain-header-status" aria-live="polite">
        <span className={`captain-pill ${fresh ? 'is-teal' : 'is-warning'}`}>
          <Icon name={fresh ? 'location_on' : 'location_off'} />
          {fresh ? 'GPS trực tiếp' : 'GPS chưa cập nhật'}
        </span>
        <span className={`captain-pill ${connected ? 'is-green' : 'is-warning'}`}>
          <Icon name={connected ? 'check' : 'sync'} />
          {connected ? 'Đã kết nối' : 'Đang kết nối lại'}
        </span>
      </div>
      <details className="captain-account">
        <summary>
          <span className="captain-avatar">
            <Icon name="sailing" />
          </span>
          <span>{user?.fullName ?? 'Captain'}</span>
          <Icon name="expand_more" />
        </summary>
        <div className="captain-account-menu">
          <span>Thuyền trưởng</span>
          <strong>{user?.fullName}</strong>
          <button type="button" onClick={signOut}>
            <Icon name="logout" />
            Đăng xuất
          </button>
        </div>
      </details>
      <span className="captain-header-divider captain-clock-divider" />
      <time className="captain-clock" dateTime={now.toISOString()}>
        <Icon name="schedule" />
        {formatClock(now)}
      </time>
    </header>
  )
}
