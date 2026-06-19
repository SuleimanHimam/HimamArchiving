import { useState, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { admin, type BackupStatus, type AutoBackupSettings } from '../lib/admin'
import { useToast } from './toast'
import FolderPickerModal from './FolderPickerModal'
import './backupsettings.css'

const INTERVAL_OPTIONS = [6, 12, 24, 48, 168]

export default function BackupSettings() {
  const { t } = useTranslation()
  const toast = useToast()
  const fileRef = useRef<HTMLInputElement>(null)

  const [status, setStatus] = useState<BackupStatus | null>(null)
  const [backing, setBacking] = useState(false)
  const [restoring, setRestoring] = useState(false)
  const [confirmFile, setConfirmFile] = useState<File | null>(null)

  const [auto, setAuto] = useState<AutoBackupSettings | null>(null)
  const [autoEnabled, setAutoEnabled] = useState(false)
  const [autoPath, setAutoPath] = useState('')
  const [autoInterval, setAutoInterval] = useState(24)
  const [savingAuto, setSavingAuto] = useState(false)
  const [testingPath, setTestingPath] = useState(false)
  const [pathTestResult, setPathTestResult] = useState<{ ok: boolean; error: string | null } | null>(null)
  const [pickerOpen, setPickerOpen] = useState(false)

  useEffect(() => {
    admin.backupStatus().then(setStatus).catch(() => {})
    admin.autoBackup.get().then((a) => {
      setAuto(a)
      setAutoEnabled(a.enabled)
      setAutoPath(a.targetPath ?? '')
      setAutoInterval(a.intervalHours)
    }).catch(() => {})
  }, [])

  async function onTestPath() {
    if (!autoPath.trim()) return
    setTestingPath(true)
    setPathTestResult(null)
    try {
      const r = await admin.autoBackup.testPath(autoPath.trim())
      setPathTestResult(r)
    } catch {
      setPathTestResult({ ok: false, error: null })
    } finally { setTestingPath(false) }
  }

  async function onSaveAuto() {
    setSavingAuto(true)
    try {
      const saved = await admin.autoBackup.update({
        enabled: autoEnabled,
        targetPath: autoPath.trim() || null,
        intervalHours: autoInterval,
      })
      setAuto(saved)
      toast.success(t('backup.saveAutoSuccess'))
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? t('backup.saveAutoError'))
    } finally { setSavingAuto(false) }
  }

  async function onBackup() {
    setBacking(true)
    try {
      await admin.downloadBackup()
      toast.success(t('backup.downloadSuccess'))
    } catch {
      toast.error(t('backup.downloadError'))
    } finally { setBacking(false) }
  }

  function onFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    if (!file.name.endsWith('.sql')) {
      toast.error(t('backup.invalidFile'))
      return
    }
    setConfirmFile(file)
    if (fileRef.current) fileRef.current.value = ''
  }

  async function onRestoreConfirm() {
    if (!confirmFile) return
    setRestoring(true)
    const file = confirmFile
    setConfirmFile(null)
    try {
      const result = await admin.restore(file)
      toast.success(t('backup.restoreSuccess', { file: result.file }))
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { details?: string } } })?.response?.data?.details
      toast.error(msg ? `${t('backup.restoreError')}: ${msg}` : t('backup.restoreError'))
    } finally { setRestoring(false) }
  }

  const toolsOk = status?.mysqldumpFound && status?.mysqlFound

  return (
    <div className="doc-card bk">
      <div className="bk__head">
        <span className="kicker">{t('backup.kicker')}</span>
        <h2 className="bk__title">{t('backup.title')}</h2>
        <p className="bk__desc">{t('backup.description')}</p>
      </div>

      {/* Tool status */}
      {status && (
        <div className={`bk__status ${toolsOk ? 'is-ok' : 'is-warn'}`}>
          <span className="bk__statusicon">{toolsOk ? '✓' : '⚠'}</span>
          <span>{toolsOk ? t('backup.toolsReady') : t('backup.toolsMissing')}</span>
          {!status.mysqldumpFound && (
            <span className="bk__missing">mysqldump — {t('backup.notFound')}</span>
          )}
          {!status.mysqlFound && (
            <span className="bk__missing">mysql — {t('backup.notFound')}</span>
          )}
        </div>
      )}

      <div className="bk__grid">
        {/* Backup card */}
        <div className="bk__card">
          <div className="bk__cardicon">⬇</div>
          <h3 className="bk__cardtitle">{t('backup.backupTitle')}</h3>
          <p className="bk__carddesc">{t('backup.backupDesc')}</p>
          <button
            className="btn btn-seal bk__btn"
            onClick={onBackup}
            disabled={backing || !status?.mysqldumpFound}
          >
            {backing ? t('backup.downloading') : t('backup.downloadBtn')}
          </button>
        </div>

        {/* Restore card */}
        <div className="bk__card">
          <div className="bk__cardicon">⬆</div>
          <h3 className="bk__cardtitle">{t('backup.restoreTitle')}</h3>
          <p className="bk__carddesc">{t('backup.restoreDesc')}</p>
          <input
            ref={fileRef}
            type="file"
            accept=".sql"
            style={{ display: 'none' }}
            onChange={onFileChange}
          />
          <button
            className="btn btn-ghost bk__btn"
            onClick={() => fileRef.current?.click()}
            disabled={restoring || !status?.mysqlFound}
          >
            {restoring ? t('backup.restoring') : t('backup.restoreBtn')}
          </button>
        </div>
      </div>

      {/* Auto-backup section */}
      <div className="bk__auto">
        <div className="bk__autohead">
          <div>
            <h3 className="bk__cardtitle">{t('backup.autoTitle')}</h3>
            <p className="bk__carddesc">{t('backup.autoDesc')}</p>
          </div>
          <button
            type="button"
            className={`bk__switch ${autoEnabled ? 'is-on' : ''}`}
            role="switch"
            aria-checked={autoEnabled}
            onClick={() => setAutoEnabled((v) => !v)}
          >
            <span className="bk__switch-knob" />
            <span className="bk__switch-label">{autoEnabled ? t('backup.autoEnableOn') : t('backup.autoEnableOff')}</span>
          </button>
        </div>

        <div className="bk__autobody">
          <label className="field bk__autofield">
            <span>{t('backup.pathLabel')}</span>
            <div className="bk__pathrow">
              <input
                dir="ltr"
                value={autoPath}
                placeholder={t('backup.pathPlaceholder')}
                onChange={(e) => { setAutoPath(e.target.value); setPathTestResult(null) }}
              />
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                onClick={() => setPickerOpen(true)}
              >
                📁 {t('backup.browseBtn')}
              </button>
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                disabled={!autoPath.trim() || testingPath}
                onClick={onTestPath}
              >
                {testingPath ? t('backup.testingPath') : t('backup.testPathBtn')}
              </button>
            </div>
            {pathTestResult && (
              <span className={`bk__pathresult ${pathTestResult.ok ? 'is-ok' : 'is-err'}`}>
                {pathTestResult.ok ? t('backup.pathOk') : `${t('backup.pathError')}${pathTestResult.error ? ` — ${pathTestResult.error}` : ''}`}
              </span>
            )}
          </label>

          <label className="field bk__autofield">
            <span>{t('backup.intervalLabel')}</span>
            <select value={autoInterval} onChange={(e) => setAutoInterval(Number(e.target.value))}>
              {INTERVAL_OPTIONS.map((h) => (
                <option key={h} value={h}>
                  {h === 6 ? t('backup.every6h')
                    : h === 12 ? t('backup.every12h')
                    : h === 24 ? t('backup.daily')
                    : h === 48 ? t('backup.every2days')
                    : t('backup.weekly')}
                </option>
              ))}
            </select>
          </label>
        </div>

        {auto && (
          <div className="bk__lastrun">
            <span>{t('backup.lastRunLabel')}:</span>
            {auto.lastRunAt ? (
              <>
                <span className="mono">{new Date(auto.lastRunAt).toLocaleString()}</span>
                <span className={`status-pill ${auto.lastRunStatus === 'Success' ? 's-active' : 's-onhold'}`}>
                  {auto.lastRunStatus === 'Success' ? t('backup.lastRunSuccess') : t('backup.lastRunFailed')}
                </span>
                {auto.lastRunStatus === 'Failed' && auto.lastRunError && (
                  <span className="bk__lastrunerror">{auto.lastRunError}</span>
                )}
              </>
            ) : (
              <span className="muted">{t('backup.lastRunNever')}</span>
            )}
          </div>
        )}

        <button className="btn btn-seal bk__btn" disabled={savingAuto} onClick={onSaveAuto}>
          {savingAuto ? t('backup.savingAuto') : t('backup.saveAutoBtn')}
        </button>
      </div>

      {/* Confirm restore dialog */}
      {confirmFile && (
        <div className="bk__overlay" onClick={() => setConfirmFile(null)}>
          <div className="bk__confirm" onClick={(e) => e.stopPropagation()}>
            <div className="bk__confirmicon">⚠</div>
            <h3 className="bk__confirmtitle">{t('backup.confirmTitle')}</h3>
            <p className="bk__confirmbody">
              {t('backup.confirmBody', { file: confirmFile.name })}
            </p>
            <div className="bk__confirmactions">
              <button className="btn btn-seal" onClick={onRestoreConfirm}>
                {t('backup.confirmOk')}
              </button>
              <button className="btn btn-ghost" onClick={() => setConfirmFile(null)}>
                {t('common.actions.cancel')}
              </button>
            </div>
          </div>
        </div>
      )}

      <FolderPickerModal
        open={pickerOpen}
        initialPath={autoPath.trim() || null}
        onClose={() => setPickerOpen(false)}
        onSelect={(path) => { setAutoPath(path); setPathTestResult(null) }}
      />
    </div>
  )
}
