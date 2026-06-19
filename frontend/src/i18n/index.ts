import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import ar from './ar.json'
import en from './en.json'

export type Lang = 'ar' | 'en'

const STORAGE_KEY = 'diwan-lang'

export function getSavedLang(): Lang {
  return (localStorage.getItem(STORAGE_KEY) as Lang) ?? 'ar'
}

export function setLang(lang: Lang) {
  localStorage.setItem(STORAGE_KEY, lang)
  i18n.changeLanguage(lang)
  applyDir(lang)
}

export function applyDir(lang: Lang) {
  const dir = lang === 'ar' ? 'rtl' : 'ltr'
  document.documentElement.setAttribute('dir', dir)
  document.documentElement.setAttribute('lang', lang)
}

i18n
  .use(initReactI18next)
  .init({
    resources: { ar: { translation: ar }, en: { translation: en } },
    lng: getSavedLang(),
    fallbackLng: 'ar',
    interpolation: { escapeValue: false },
  })

applyDir(getSavedLang())

export default i18n
