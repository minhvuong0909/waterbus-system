import {
  createContext,
  Fragment,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import {
  DEFAULT_LOCALE,
  hasDictionary,
  isLocale,
  loadDictionary,
  setActiveLocale,
} from './translate'
import type { Locale } from './translate'

const STORAGE_KEY = 'waterbus.locale'

function readStoredLocale(): Locale {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (isLocale(stored)) return stored
  } catch {
    // localStorage can be blocked (private mode); fall back to the default.
  }
  return DEFAULT_LOCALE
}

interface I18nContextValue {
  locale: Locale
  setLocale: (locale: Locale) => void
}

const I18nContext = createContext<I18nContextValue>({
  locale: DEFAULT_LOCALE,
  setLocale: () => undefined,
})

export function useI18n() {
  return useContext(I18nContext)
}

interface State {
  /** Language currently shown. */
  locale: Locale
  /** Saved language whose translations are still downloading on first load. */
  pending: Locale | null
}

function initialState(): State {
  const stored = readStoredLocale()
  return hasDictionary(stored)
    ? { locale: stored, pending: null }
    : { locale: DEFAULT_LOCALE, pending: stored }
}

/**
 * Holds the chosen language. The subtree is keyed by locale, so switching remounts every page and
 * all text, data requests and number formats are produced again in the new language.
 * Translations are downloaded before switching, so the screen never shows a mix of languages.
 */
export function I18nProvider({ children }: { children: ReactNode }) {
  const [{ locale, pending }, setState] = useState<State>(initialState)

  // Must happen before children render so `t()` already sees the new language.
  setActiveLocale(locale)

  useEffect(() => {
    document.documentElement.lang = locale
  }, [locale])

  // First load with a saved non-English language: wait for its dictionary.
  useEffect(() => {
    if (!pending) return
    let cancelled = false
    loadDictionary(pending).then(() => {
      if (!cancelled) setState({ locale: pending, pending: null })
    })
    return () => {
      cancelled = true
    }
  }, [pending])

  const setLocale = useCallback((next: Locale) => {
    loadDictionary(next).then(() => {
      try {
        window.localStorage.setItem(STORAGE_KEY, next)
      } catch {
        // Not persisted, still applies for this visit.
      }
      setState({ locale: next, pending: null })
    })
  }, [])

  const value = useMemo(() => ({ locale, setLocale }), [locale, setLocale])

  return (
    <I18nContext.Provider value={value}>
      {pending ? null : <Fragment key={locale}>{children}</Fragment>}
    </I18nContext.Provider>
  )
}
