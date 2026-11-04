import { CopyButton, Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/**
 * Manage Order secure link của đơn (BR-ID-02). Backend chưa gửi email nên đây là cách duy nhất để
 * Guest mở lại vé từ thiết bị khác; ai có link đều xem được đơn nên nhắc khách giữ riêng.
 */
export default function ManageLinkCard({ url }: { url: string }) {
  return (
    <section className="rounded-2xl border border-teal-flow/30 bg-secondary-container/30 p-space-md">
      <h2 className="flex items-center gap-2 font-display text-lg font-bold text-deep-river">
        <Icon name="lock" className="text-xl text-teal-flow" />
        {t('Save your booking link')}
      </h2>
      <p className="mt-1 text-sm text-on-surface-variant">
        {t('Your tickets stay in My Tickets on this device. To open them on another device, keep this link — anyone who has it can see this booking.')}
      </p>
      <div className="mt-space-sm flex items-center gap-2 rounded-xl border border-outline-variant/50 bg-white px-3 py-2">
        <span className="min-w-0 flex-1 truncate font-mono text-xs text-deep-river" title={url}>
          {url}
        </span>
        <CopyButton value={url} />
      </div>
    </section>
  )
}
