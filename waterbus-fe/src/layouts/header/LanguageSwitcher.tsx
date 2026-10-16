import { LOCALES, t, useI18n } from '../../i18n'

interface LanguageSwitcherProps {
  /** `compact` is the small EN | VI toggle in the header; `inline` is the larger one in the mobile menu. */
  variant?: 'compact' | 'inline'
}

/** Shows every language at once, so switching takes one click and needs no menu. */
export default function LanguageSwitcher({ variant = 'compact' }: LanguageSwitcherProps) {
  const { locale, setLocale } = useI18n()
  const compact = variant === 'compact'

  return (
    <div
      role="group"
      aria-label={t('Language')}
      className={compact ? 'hidden items-center gap-0.5 sm:flex' : 'flex gap-space-sm'}
    >
      {LOCALES.map((l) => (
        <button
          key={l.code}
          type="button"
          aria-pressed={l.code === locale}
          aria-label={l.label}
          onClick={() => setLocale(l.code)}
          className={`rounded-full text-body-sm font-semibold transition-colors ${
            compact ? 'px-2.5 py-1' : 'px-4 py-1.5'
          } ${
            l.code === locale
              ? 'bg-deep-river text-on-primary'
              : 'text-on-surface-variant hover:bg-surface-container hover:text-deep-river'
          }`}
        >
          {compact ? l.short : l.label}
        </button>
      ))}
    </div>
  )
}
