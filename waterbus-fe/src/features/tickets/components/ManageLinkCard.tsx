import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import LinkField from './LinkField'

/**
 * Manage Order secure link của đơn (BR-ID-02). Email xác nhận cố ý KHÔNG chứa link này (không gửi token truy cập
 * qua email) nên đây vẫn là cách duy nhất để Guest mở lại vé từ thiết bị khác; ai có link đều xem được đơn nên nhắc khách giữ riêng.
 */
export default function ManageLinkCard({ url }: { url: string }) {
  return (
    <section className="rounded-2xl border border-teal-flow/30 bg-secondary-container/30 p-space-md">
      <h2 className="flex items-center gap-2 font-display text-lg font-bold text-deep-river">
        <Icon name="lock" className="text-xl text-teal-flow" />
        {t('Save your booking link')}
      </h2>
      <p className="mb-space-sm mt-1 text-sm text-on-surface-variant">
        {t('Your tickets stay in My Tickets on this device. To open them on another device, keep this link — anyone who has it can see this booking.')}
      </p>
      <LinkField url={url} />
    </section>
  )
}
