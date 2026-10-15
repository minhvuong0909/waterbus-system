import { useEffect } from 'react'
import { t } from '../i18n'

const APP_NAME = 'Smart Waterbus'

export function useDocumentTitle(title?: string) {
  useEffect(() => {
    document.title = title ? `${t(title)} — ${APP_NAME}` : APP_NAME
  }, [title])
}
