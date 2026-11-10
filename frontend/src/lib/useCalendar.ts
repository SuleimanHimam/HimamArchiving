import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDate, formatDateTime } from './datetime'

/**
 * Date formatters bound to the current language. Dates are always Gregorian
 * (§14.2); components re-render when the language changes.
 */
export function useDateFormat() {
  const { i18n } = useTranslation()
  const lang = i18n.language

  return {
    date: useCallback((iso?: string | null) => formatDate(iso, lang), [lang]),
    dateTime: useCallback((iso?: string | null) => formatDateTime(iso, lang), [lang]),
  }
}
