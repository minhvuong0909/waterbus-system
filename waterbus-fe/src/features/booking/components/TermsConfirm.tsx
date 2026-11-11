import { useState } from 'react'
import { Button, Modal } from '../../../components/ui'
import { CARRIAGE_RULES } from '../../../content/carriageRules'
import { t } from '../../../i18n'

export default function TermsConfirm({
  checked,
  onChange,
}: {
  checked: boolean
  onChange: (v: boolean) => void
}) {
  // Khách phải đọc được quy tắc mình đồng ý, nên tên quy tắc mở ra nội dung thay vì chỉ là chữ gạch chân.
  const [open, setOpen] = useState(false)
  return (
    <>
      <label className="flex cursor-pointer items-center gap-3 rounded-2xl bg-surface-container px-space-lg py-space-md text-xs text-on-surface-variant">
        <input
          type="checkbox"
          checked={checked}
          onChange={(e) => onChange(e.target.checked)}
          className="h-5 w-5 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow"
        />
        <span>
          {t('I confirm that the booking details above are correct. I accept the')}{' '}
          <button
            type="button"
            onClick={(e) => {
              e.preventDefault()
              setOpen(true)
            }}
            className="font-semibold text-teal-flow underline underline-offset-2 hover:text-secondary"
          >
            {t('Waterway Carriage Rules')}
          </button>
          .
        </span>
      </label>
      {open && (
        <Modal title={t('Waterway Carriage Rules')} onClose={() => setOpen(false)}>
          <ol className="list-decimal space-y-2 pl-5 text-sm text-on-surface-variant">
            {CARRIAGE_RULES.map((rule) => (
              <li key={rule}>{t(rule)}</li>
            ))}
          </ol>
          <div className="mt-space-md flex justify-end">
            <Button
              onClick={() => {
                onChange(true)
                setOpen(false)
              }}
            >
              {t('I accept')}
            </Button>
          </div>
        </Modal>
      )}
    </>
  )
}
