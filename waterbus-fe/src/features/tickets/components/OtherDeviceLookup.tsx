import { t } from '../../../i18n'
import OpenBookingLinkForm from './OpenBookingLinkForm'

/** "Mua vé trên thiết bị khác?": mở đơn bằng link quản lý vé đã lưu (BR-ID-02). */
export default function OtherDeviceLookup() {
  return (
    <>
      <h2 className="text-sm font-semibold text-deep-river">{t('Bought on another device?')}</h2>
      <p className="mb-space-sm mt-1 text-xs text-on-surface-variant">
        {t('Open your booking with the link you saved after payment.')}
      </p>
      <OpenBookingLinkForm />
    </>
  )
}
