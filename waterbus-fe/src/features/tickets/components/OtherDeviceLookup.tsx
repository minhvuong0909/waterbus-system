import { t } from '../../../i18n'
import { Button } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import OpenBookingLinkForm from './OpenBookingLinkForm'

/** "Mua vé trên thiết bị khác?": mở đơn bằng link quản lý vé đã lưu (BR-ID-02), hoặc tra cứu lại bằng email + mã đặt chỗ + OTP. */
export default function OtherDeviceLookup() {
  return (
    <>
      <h2 className="text-sm font-semibold text-deep-river">{t('Bought on another device?')}</h2>
      <p className="mb-space-sm mt-1 text-xs text-on-surface-variant">
        {t('Open your booking with the link you saved after payment.')}
      </p>
      <OpenBookingLinkForm />
      <Button to={ROUTES.lookupTickets} variant="text" size="sm" className="mt-space-xs">
        {t('Lost the link? Find my tickets with email and booking code')}
      </Button>
    </>
  )
}
