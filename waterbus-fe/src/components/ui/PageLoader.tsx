import { t } from '../../i18n' /** Placeholder shown while a page's data (or code chunk) loads. Keeps the layout from jumping. */
export default function PageLoader() {
  return (
    <div
      role="status"
      aria-busy="true"
      aria-label={t('Loading')}
      className="flex min-h-[60vh] items-center justify-center"
    >
      <span className="h-8 w-8 animate-spin rounded-full border-2 border-outline-variant border-t-teal-flow" />
    </div>
  )
}
