import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { manageLinkPath, parseManageLink } from '../utils'

/**
 * Mở một đơn mua trên thiết bị khác bằng link quản lý vé khách đã lưu (BR-ID-02). Chưa có API tra
 * cứu theo mã đặt chỗ nên chỉ nhận link, không hứa tra cứu bằng mã.
 */
export default function OpenBookingLinkForm() {
  const navigate = useNavigate()
  const [value, setValue] = useState('')
  const [invalid, setInvalid] = useState(false)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const link = parseManageLink(value)
    if (!link) {
      setInvalid(true)
      return
    }
    navigate(manageLinkPath(link.orderId, link.accessToken))
  }

  return (
    <form onSubmit={submit} className="flex w-full flex-col gap-space-sm text-left sm:flex-row sm:items-start">
      <div className="flex-1">
        <TextField
          label="Booking link"
          hint="The link shown on the confirmation page after payment."
          placeholder={t('Paste your booking link')}
          value={value}
          onChange={(e) => {
            setValue(e.target.value)
            setInvalid(false)
          }}
          error={invalid ? t('This is not a Smart Waterbus booking link.') : undefined}
          spellCheck={false}
          autoComplete="off"
        />
      </div>
      <Button type="submit" variant="dark" className="sm:mt-6" disabled={!value.trim()}>
        {t('Open booking')}
      </Button>
    </form>
  )
}
