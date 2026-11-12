import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Icon, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { useAuth } from '../../authentication/hooks/useAuth'
import { isPassenger } from '../../authentication/utils'
import { forgetOrder, saveOrder } from '../orderStorage'
import { claimOrder, revokeOrderLinks, rotateOrderLink } from '../services/orderAccessService'
import { accountOrderPath, manageLinkPath, manageLinkUrl } from '../utils'
import LinkField from './LinkField'

type Action = 'claim' | 'rotate' | 'revoke'

/** Lý do một thao tác bị từ chối, nói bằng lời của khách thay vì thông báo kỹ thuật của máy chủ. */
function failureMessage(action: Action, error: unknown): string {
  const status = error instanceof ApiError ? error.status : 0
  if (action === 'claim' && status === 409) return t('This booking is already saved to another account.')
  if (status === 403) return t('This booking link is no longer valid.')
  return t('That did not work. Please try again.')
}

function Confirm({
  message,
  confirmLabel,
  busy,
  onConfirm,
  onCancel,
}: {
  message: string
  confirmLabel: string
  busy: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <div className="mt-space-md rounded-xl bg-surface-container-low p-space-md">
      <p className="text-sm text-deep-river">{message}</p>
      <div className="mt-space-sm flex flex-wrap gap-2">
        <Button size="sm" variant="dark" onClick={onConfirm} disabled={busy}>
          {confirmLabel}
        </Button>
        <Button size="sm" variant="text" onClick={onCancel} disabled={busy}>
          {t('Cancel')}
        </Button>
      </div>
    </div>
  )
}

interface OrderAccessCardProps {
  orderId: string
  /** Token Guest đang mở đơn; null khi đơn được mở bằng tài khoản sở hữu nó. */
  accessToken: string | null
}

/**
 * Ai mở được đơn này và bằng cách nào (BR-ID-02, 04, 07).
 * - Mở bằng link: hiện link để lưu, cho đổi link, và cho Passenger đã đăng nhập lưu đơn vào tài khoản.
 * - Mở bằng tài khoản: cho cấp link mới hoặc thu hồi mọi link. Thu hồi chỉ có ở đây, vì Guest thu
 *   hồi link của chính mình sẽ không còn cách nào mở lại đơn.
 */
export default function OrderAccessCard({ orderId, accessToken }: OrderAccessCardProps) {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [confirming, setConfirming] = useState<'rotate' | 'revoke' | null>(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<{ tone: 'success' | 'error'; text: string } | null>(null)
  /** Link vừa cấp cho chủ đơn; backend chỉ trả token một lần nên chỉ hiện trong lần xem này. */
  const [issuedUrl, setIssuedUrl] = useState<string>()

  const run = async (action: Action, work: () => Promise<void>) => {
    setBusy(true)
    setNotice(null)
    try {
      await work()
    } catch (error) {
      setNotice({ tone: 'error', text: failureMessage(action, error) })
    } finally {
      setBusy(false)
      setConfirming(null)
    }
  }

  const claim = (token: string) =>
    run('claim', async () => {
      await claimOrder(orderId, token)
      navigate(accountOrderPath(orderId), { replace: true })
    })

  const rotate = () =>
    run('rotate', async () => {
      const link = await rotateOrderLink(orderId, accessToken)
      if (accessToken === null) {
        setIssuedUrl(manageLinkUrl(orderId, link.accessToken))
        return
      }
      saveOrder(orderId, link.accessToken)
      navigate(manageLinkPath(orderId, link.accessToken), { replace: true })
      setNotice({ tone: 'success', text: t('The link was replaced. Keep the new one.') })
    })

  const revoke = () =>
    run('revoke', async () => {
      const { revokedCount } = await revokeOrderLinks(orderId, null)
      forgetOrder(orderId)
      setIssuedUrl(undefined)
      setNotice({
        tone: 'success',
        text:
          revokedCount > 0
            ? t('Every link to this booking has been revoked.')
            : t('This booking has no active links.'),
      })
    })

  const viaAccount = accessToken === null

  return (
    <section className="rounded-2xl bg-white p-space-lg shadow-card">
      <h2 className="flex items-center gap-2 font-display text-lg font-bold text-deep-river">
        <Icon name={viaAccount ? 'verified' : 'lock'} className="text-2xl text-teal-flow" />
        {viaAccount ? t('Saved to your account') : t('Booking link')}
      </h2>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {viaAccount
          ? t('Sign in on any device to find this booking in My Tickets. A booking link opens it without signing in.')
          : t('Keep this link to open the booking on another device. Anyone who has it can see this booking.')}
      </p>

      {accessToken !== null && (
        <div className="mt-space-md">
          <LinkField url={manageLinkUrl(orderId, accessToken)} />
        </div>
      )}
      {issuedUrl && (
        <div className="mt-space-md">
          <LinkField url={issuedUrl} />
          <p className="mt-1 text-xs text-on-surface-variant">
            {t('Copy this link now; it is shown only once.')}
          </p>
        </div>
      )}

      {confirming === null && (
        <div className="mt-space-md flex flex-wrap gap-2">
          {accessToken !== null && isPassenger(user) && (
            <Button size="sm" icon="person" onClick={() => claim(accessToken)} disabled={busy}>
              {t('Save to my account')}
            </Button>
          )}
          <Button
            size="sm"
            variant="neutral"
            icon="refresh"
            onClick={() => setConfirming('rotate')}
            disabled={busy}
          >
            {viaAccount ? t('Create a booking link') : t('Replace link')}
          </Button>
          {viaAccount && (
            <Button size="sm" variant="text" onClick={() => setConfirming('revoke')} disabled={busy}>
              {t('Revoke all links')}
            </Button>
          )}
        </div>
      )}
      {confirming === 'rotate' && (
        <Confirm
          message={t('Any booking link issued before stops working right away.')}
          confirmLabel={viaAccount ? t('Create link') : t('Replace link')}
          busy={busy}
          onConfirm={rotate}
          onCancel={() => setConfirming(null)}
        />
      )}
      {confirming === 'revoke' && (
        <Confirm
          message={t('Every booking link and shared journey link of this booking stops working. You keep access through your account.')}
          confirmLabel={t('Revoke all links')}
          busy={busy}
          onConfirm={revoke}
          onCancel={() => setConfirming(null)}
        />
      )}

      {notice && (
        <div className="mt-space-md">
          <Notice tone={notice.tone}>{notice.text}</Notice>
        </div>
      )}
    </section>
  )
}
