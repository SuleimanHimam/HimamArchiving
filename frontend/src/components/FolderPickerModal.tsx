import { useState, useEffect, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { AnimatePresence, motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { admin, type BrowseDirEntry } from '../lib/admin'
import './folderpickermodal.css'

interface Props {
  open: boolean
  initialPath?: string | null
  onClose: () => void
  onSelect: (path: string) => void
}

export default function FolderPickerModal({ open, initialPath, onClose, onSelect }: Props) {
  const { t } = useTranslation()
  const [currentPath, setCurrentPath] = useState<string | null>(null)
  const [parentPath, setParentPath] = useState<string | null>(null)
  const [dirs, setDirs] = useState<BrowseDirEntry[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback((path?: string) => {
    setLoading(true); setError(null)
    admin.autoBackup.browse(path)
      .then((r) => {
        setCurrentPath(r.currentPath)
        setParentPath(r.parentPath)
        setDirs(r.directories)
        if (r.error) setError(r.error)
      })
      .catch(() => setError(t('backup.browseError')))
      .finally(() => setLoading(false))
  }, [t])

  useEffect(() => {
    if (open) load(initialPath ?? undefined)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  function enter(path: string) { load(path) }
  function goUp() { load(parentPath ?? undefined) }
  function confirm() { if (currentPath) { onSelect(currentPath); onClose() } }

  return createPortal(
    <AnimatePresence>
      {open && (
        <motion.div
          className="fpm__backdrop"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
          onClick={onClose}
        >
          <motion.div
            className="fpm"
            role="dialog" aria-modal="true"
            initial={{ opacity: 0, scale: 0.96, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.96, y: 10 }}
            transition={{ duration: 0.18 }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="fpm__title">{t('backup.browseTitle')}</h3>

            <div className="fpm__path mono" dir="ltr">{currentPath || t('backup.browseDrives')}</div>

            <div className="fpm__list">
              {loading ? (
                <div className="fpm__state">{t('common.loading')}</div>
              ) : (
                <>
                  {currentPath && (
                    <button className="fpm__item fpm__item--up" onClick={goUp}>
                      <span className="fpm__icon">⬑</span> ..
                    </button>
                  )}
                  {dirs.length === 0 && !error && (
                    <div className="fpm__state">{t('backup.browseEmpty')}</div>
                  )}
                  {dirs.map((d) => (
                    <button key={d.fullPath} className="fpm__item" onClick={() => enter(d.fullPath)}>
                      <span className="fpm__icon">📁</span> {d.name}
                    </button>
                  ))}
                </>
              )}
              {error && <div className="fpm__error">{error}</div>}
            </div>

            <div className="fpm__actions">
              <button className="btn btn-seal" disabled={!currentPath || !!error} onClick={confirm}>
                {t('backup.browseSelect')}
              </button>
              <button type="button" className="btn btn-ghost" onClick={onClose}>
                {t('common.actions.cancel')}
              </button>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>,
    document.body,
  )
}
