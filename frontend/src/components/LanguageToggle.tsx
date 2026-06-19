import { useState } from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { setLang, type Lang } from '../i18n'
import './langtoggle.css'

export default function LanguageToggle() {
  const { t, i18n } = useTranslation()
  const current = i18n.language as Lang
  const [confirming, setConfirming] = useState(false)

  function confirm() {
    setConfirming(false)
    setLang(current === 'ar' ? 'en' : 'ar')
  }

  return (
    <>
      <button
        onClick={() => setConfirming(true)}
        className="shell__langbtn"
        title={current === 'ar' ? 'Switch to English' : 'التبديل إلى العربية'}
        aria-label={current === 'ar' ? 'Switch to English' : 'التبديل إلى العربية'}
      >
        {current === 'ar' ? 'EN' : 'ع'}
      </button>

      {createPortal(
        <AnimatePresence>
          {confirming && (
            <motion.div
              className="langdlg__backdrop"
              initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
              transition={{ duration: 0.18 }}
              onClick={() => setConfirming(false)}
            >
              <motion.div
                className="langdlg"
                role="dialog"
                aria-modal="true"
                aria-labelledby="langdlg-title"
                initial={{ opacity: 0, scale: 0.95, y: 12 }}
                animate={{ opacity: 1, scale: 1, y: 0 }}
                exit={{ opacity: 0, scale: 0.95, y: 12 }}
                transition={{ duration: 0.2 }}
                onClick={(e) => e.stopPropagation()}
              >
                <div className="langdlg__icon">🌐</div>
                <h2 id="langdlg-title" className="langdlg__title">
                  {t('lang.switchTitle')}
                </h2>
                <p className="langdlg__body">
                  {t('lang.switchConfirm')}
                </p>
                <div className="langdlg__actions">
                  <button className="langdlg__btn langdlg__btn--confirm" onClick={confirm}>
                    {t('lang.confirm')}
                  </button>
                  <button className="langdlg__btn langdlg__btn--cancel" onClick={() => setConfirming(false)}>
                    {t('lang.cancel')}
                  </button>
                </div>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>,
        document.body,
      )}
    </>
  )
}
