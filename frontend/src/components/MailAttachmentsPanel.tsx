import { useRef, useState } from 'react'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { scanAgent } from '../lib/scanAgent'
import { scannerSettings } from '../lib/scannerSettings'
import { Paperclip, Trash2, Scan } from 'lucide-react'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'

/** The shape both registers' attachments share. */
export interface MailAttachmentItem {
  id: number
  fileName: string
  sizeBytes: number
  ocrStatus: string
}

interface Props {
  attachments: MailAttachmentItem[]
  /** Uploads one file to the letter and resolves when stored. */
  onUpload: (file: File) => Promise<unknown>
  /** Removes an attachment. Omit when the register has no delete endpoint. */
  onRemove?: (attachmentId: number) => Promise<unknown>
  /** Re-fetches the letter so the new attachment appears. */
  onChanged: () => Promise<unknown>
  /** False hides the add/scan controls — e.g. an archived or read-only letter. */
  canEdit?: boolean
}

/**
 * Attachments on a saved letter, with the controls to add more.
 *
 * Compose can only stage files in memory until the letter has an id, so anything attached after
 * the fact — a scan that arrived later, a document someone forgot — had no way in. This is that
 * way in, and it is shared by the Diwan and internal registers so both behave identically.
 */
export default function MailAttachmentsPanel({
  attachments, onUpload, onRemove, onChanged, canEdit = true,
}: Props) {
  const { t } = useTranslation()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const fileRef = useRef<HTMLInputElement>(null)
  const scanFallback = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')

  /** Uploads a batch, reporting per-file so one bad file does not hide the rest succeeding. */
  async function upload(files: File[]) {
    if (files.length === 0) return
    setBusy(true)
    let ok = 0
    try {
      for (const f of files) {
        setStatus(t('corr.uploadingAttachments', { n: files.length, defaultValue: 'جارٍ الرفع…' }))
        try { await onUpload(f); ok++ }
        catch { toast.error(t('corr.errors.attachmentFailed', { name: f.name, defaultValue: `تعذّر رفع ${f.name}` })) }
      }
      if (ok > 0) { await onChanged(); toast.success(`تم إرفاق ${ok} ملف`) }
    } finally { setBusy(false); setStatus('') }
  }

  function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? [])
    e.target.value = ''            // so re-picking the same file still fires onChange
    upload(files)
  }

  async function startScan() {
    setStatus(t('corr.scan.connecting', { defaultValue: 'جارٍ الاتصال بالماسح الضوئي…' }))
    try {
      const agent = await scanAgent.status()
      // No local agent — fall back to picking the file the scanner already produced.
      if (!agent) { setStatus(''); scanFallback.current?.click(); return }
      const chosen = scannerSettings.get() ?? undefined
      setStatus(t('corr.scan.scanning', { defaultValue: 'جارٍ المسح…' }))
      const { blob, fileName } = await scanAgent.scan('pdf', { ...scannerSettings.getScanPrefs(), scanner: chosen })
      await upload([new File([blob], fileName, { type: blob.type || 'application/pdf' })])
    } catch {
      setStatus('')
      toast.error(t('corr.scan.failed', { defaultValue: 'تعذّر المسح الضوئي' }))
    }
  }

  async function remove(a: MailAttachmentItem) {
    if (!onRemove) return
    if (!(await confirm({ message: `حذف المرفق «${a.fileName}»؟`, destructive: true }))) return
    setBusy(true)
    try { await onRemove(a.id); await onChanged(); toast.success('تم حذف المرفق') }
    catch { toast.error('تعذّر حذف المرفق') }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">
        {t('corr.attachments')} <span className="corr-detail__count">{attachments.length}</span>
      </h3>

      {attachments.length === 0 ? (
        <p className="muted">{t('corr.noAttachments', { defaultValue: 'لا توجد مرفقات' })}</p>
      ) : (
        <ul className="corr__attachments">
          {attachments.map((a) => (
            <li key={a.id}>
              <Paperclip size={14} aria-hidden /> {a.fileName}{' '}
              <span className="muted mono">{Math.round(a.sizeBytes / 1024)} KB · {a.ocrStatus}</span>
              {canEdit && onRemove && (
                <button type="button" className="btn btn-ghost btn-sm" title="حذف المرفق"
                  disabled={busy} onClick={() => remove(a)}><Trash2 size={14} aria-hidden /></button>
              )}
            </li>
          ))}
        </ul>
      )}

      {canEdit && (
        <>
          <div className="action-bar" style={{ marginBlockStart: '.8rem' }}>
            <button type="button" className="btn btn-primary" disabled={busy}
              onClick={() => fileRef.current?.click()}>
              + إرفاق ملف
            </button>
            <button type="button" className="btn btn-seal" disabled={busy} onClick={startScan}
              title="المسح من ماسح ضوئي متصل بجهازك">
              <Scan size={15} aria-hidden /> مسح ضوئي
            </button>
          </div>
          {status && <p className="muted scan-msg">{status}</p>}
          <input ref={fileRef} type="file" multiple style={{ display: 'none' }} onChange={onPick} />
          <input ref={scanFallback} type="file" accept=".pdf" style={{ display: 'none' }} onChange={onPick} />
        </>
      )}
    </motion.section>
  )
}
