import { Breadcrumb, Button, Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import OpenBookingLinkForm from './OpenBookingLinkForm'

/**
 * Trang mở đơn bằng link quản lý vé: khi chưa có link (`warning` false) hoặc link không hợp lệ /
 * hết hạn (`warning` true). Không hứa tra cứu bằng mã đặt chỗ vì backend chưa hỗ trợ.
 */
export default function BookingLinkPrompt({
  title,
  message,
  warning = false,
}: {
  title: string
  message: string
  warning?: boolean
}) {
  return (
    <div className="mx-auto w-full max-w-lg px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Manage Booking' },
        ]}
      />
      <div className="mt-space-xl flex flex-col items-center rounded-2xl border border-outline-variant/40 bg-white p-space-xl text-center shadow-sm">
        <span
          className={`flex h-16 w-16 items-center justify-center rounded-full ${
            warning ? 'bg-coral-glow/10 text-coral-glow' : 'bg-secondary-container text-on-secondary-container'
          }`}
        >
          <Icon name={warning ? 'error' : 'lock'} className="text-3xl" />
        </span>
        <h1 className="mt-space-md font-display text-headline-md text-deep-river">{title}</h1>
        <p className="mb-space-lg mt-space-xs text-body-md text-on-surface-variant">{message}</p>
        <OpenBookingLinkForm />
        <Button to={ROUTES.myTickets} variant="text" size="sm" className="mt-space-sm">
          {t('Go to My Tickets')}
        </Button>
      </div>
    </div>
  )
}
