import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/ui'
import { LOCALES, t, useI18n } from '../../i18n'

interface LanguageSwitcherProps {
  /** `menu` is the header dropdown; `inline` shows both languages side by side (mobile menu). */
  variant?: 'menu' | 'inline'
}

export default function LanguageSwitcher({ variant = 'menu' }: LanguageSwitcherProps) {
  const { locale, setLocale } = useI18n()
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const current = LOCALES.find((l) => l.code === locale) ?? LOCALES[0]

  useEffect(() => {
    if (!open) return
    const onPointer = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    document.addEventListener('mousedown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  if (variant === 'inline') {
    return (
      <div role="group" aria-label={t('Language')} className="flex gap-space-sm">
        {LOCALES.map((l) => (
          <button
            key={l.code}
            type="button"
            aria-pressed={l.code === locale}
            onClick={() => setLocale(l.code)}
            className={`rounded-full px-4 py-1.5 text-body-sm font-semibold ${
              l.code === locale
                ? 'bg-deep-river text-on-primary'
                : 'bg-surface-container text-on-surface-variant hover:text-deep-river'
            }`}
          >
            {l.label}
          </button>
        ))}
      </div>
    )
  }

  return (
    <div ref={rootRef} className="relative hidden sm:block">
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={t('Language')}
        onClick={() => setOpen((v) => !v)}
        className="flex items-center gap-space-xs rounded-full px-space-sm py-space-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-deep-river"
      >
        <Icon name="language" className="text-[18px]" />
        <span className="text-body-sm font-semibold uppercase tracking-wider">{current.short}</span>
        <Icon name="expand_more" className="text-[16px]" />
      </button>
      {open && (
        <ul
          role="listbox"
          aria-label={t('Language')}
          className="absolute right-0 top-full z-50 mt-2 min-w-[150px] overflow-hidden rounded-xl border border-surface-container bg-white py-1 shadow-lg"
        >
          {LOCALES.map((l) => (
            <li key={l.code}>
              <button
                type="button"
                role="option"
                aria-selected={l.code === locale}
                onClick={() => {
                  setOpen(false)
                  setLocale(l.code)
                }}
                className={`flex w-full items-center justify-between gap-3 px-4 py-2 text-left text-body-sm hover:bg-mist ${
                  l.code === locale ? 'font-semibold text-teal-flow' : 'text-deep-river'
                }`}
              >
                {l.label}
                {l.code === locale && <Icon name="check" className="text-[16px]" />}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
