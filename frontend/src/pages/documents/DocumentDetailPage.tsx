import { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import { documents, type DocumentDetail, type ScanFormat, DOC_STATUS_LABELS, formatBytes } from '../../lib/documents'
import { scanAgent } from '../../lib/scanAgent'
import { scannerSettings } from '../../lib/scannerSettings'
import { CONFIDENTIALITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import PreservationPackagesPanel from '../../components/PreservationPackagesPanel'
import RecordMetadataPanel from '../../components/RecordMetadataPanel'
import '../incoming/incoming.css'
import './documents.css'

export default function DocumentDetailPage() {
  const { id } = useParams()
  const docId = Number(id)
  const navigate = useNavigate()
  const toast = useToast()
  const fileInput = useRef<HTMLInputElement>(null)
  const scanFallbackInput = useRef<HTMLInputElement>(null)
  const previewFrame = useRef<HTMLIFrameElement>(null)
  const [doc, setDoc] = useState<DocumentDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [scanMsg, setScanMsg] = useState('')
  const [scanName, setScanName] = useState('')
  const [scanFormat, setScanFormat] = useState<ScanFormat>('pdf')
  const [preview, setPreview] = useState<{ url: string; name: string; type: string } | null>(null)

  const load = useCallback(async () => {
    setError('')
    try { setDoc(await documents.get(docId)) }
    catch { setError('تعذّر تحميل الوثيقة (قد لا تملك صلاحية الوصول)') }
  }, [docId])

  useEffect(() => { load() }, [load])

  // Modal a11y: lock body scroll and close on Escape while a preview is open.
  useEffect(() => {
    if (!preview) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') closePreview() }
    document.addEventListener('keydown', onKey)
    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.removeEventListener('keydown', onKey); document.body.style.overflow = prevOverflow }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preview])

  async function onUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setBusy(true); setError('')
    try {
      await documents.upload(docId, file)
      await load()
      toast.success('تم رفع المرفق')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر رفع المرفق')
    } finally {
      setBusy(false)
      if (fileInput.current) fileInput.current.value = ''
    }
  }

  // Scan from the user's local scanner via the scan agent; fall back to a file picker
  // (a file produced by the scanner's own software) if the agent isn't running.
  async function onScan() {
    setBusy(true); setError(''); setScanMsg('جارٍ الاتصال بالماسح الضوئي…')
    try {
      const status = await scanAgent.status()
      if (!status) {
        setScanMsg('')
        setBusy(false)
        scanFallbackInput.current?.click() // no local agent → let the user pick a scanned file
        return
      }
      const chosen = scannerSettings.get() ?? undefined
      setScanMsg(chosen ? `جارٍ المسح من: ${chosen}…` : 'جارٍ المسح الضوئي…')
      // Upload the raw scanner image (JPEG) — robust across scanners and color modes.
      const { blob } = await scanAgent.scan('jpeg', chosen)
      await documents.scan(docId, blob, buildScanFileName(scanFormat), scanFormat)
      setScanMsg(''); setScanName('')
      await load()
      toast.success('تمت إضافة المسح الضوئي')
    } catch (err) {
      const msg = (err as { response?: { data?: { error?: string } }; message?: string })
      toast.error(msg.response?.data?.error ?? msg.message ?? 'تعذّر المسح الضوئي')
      setScanMsg('')
    } finally { setBusy(false) }
  }

  async function onScanFallback(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setBusy(true); setError('')
    try {
      // Honor a user-given name; otherwise keep the picked file's own name.
      const fileName = scanName.trim() ? buildScanFileName(scanFormat) : file.name
      await documents.scan(docId, file, fileName, scanFormat)
      setScanName('')
      await load()
      toast.success('تمت إضافة المسح الضوئي')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر رفع المسح الضوئي')
    } finally {
      setBusy(false)
      if (scanFallbackInput.current) scanFallbackInput.current.value = ''
    }
  }

  // Uses the user-given title as the file name (the scan time is shown separately per row).
  function buildScanFileName(ext: string): string {
    const base = scanName.trim() || 'مسح-ضوئي'
    return `${base}.${ext}`
  }

  async function openPreview(a: DocumentDetail['attachments'][number]) {
    setError('')
    try {
      const url = await documents.fetchObjectUrl(docId, a.id, a.contentType)
      setPreview({ url, name: a.fileName, type: a.contentType })
    } catch {
      toast.error('تعذّر فتح المعاينة')
    }
  }

  function closePreview() {
    if (preview) URL.revokeObjectURL(preview.url)
    setPreview(null)
  }

  // Print the previewed attachment: PDFs print via the embedded viewer; images via a print window.
  function printPreview() {
    if (!preview) return
    if (preview.type.includes('pdf')) {
      previewFrame.current?.contentWindow?.print()
    } else {
      const w = window.open(preview.url, '_blank')
      w?.addEventListener('load', () => w.print())
    }
  }

  // Print a PDF blob URL via a hidden iframe + the browser's PDF viewer.
  function printPdfUrl(url: string) {
    const frame = document.createElement('iframe')
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0'
    frame.src = url
    frame.onload = () => {
      frame.contentWindow?.focus()
      frame.contentWindow?.print()
      setTimeout(() => { frame.remove(); URL.revokeObjectURL(url) }, 60_000)
    }
    document.body.appendChild(frame)
  }

  // Print an attachment directly from its row (PDF / PNG / JPG) without opening the preview.
  async function printAttachment(a: DocumentDetail['attachments'][number]) {
    try {
      const url = await documents.fetchObjectUrl(docId, a.id, a.contentType)
      if (a.contentType.includes('pdf')) {
        printPdfUrl(url)
      } else {
        const w = window.open('', '_blank')
        if (w) {
          w.document.write(
            `<title>${a.fileName}</title><img src="${url}" style="max-width:100%" ` +
            `onload="window.focus();window.print()" />`)
          w.document.close()
          setTimeout(() => URL.revokeObjectURL(url), 60_000)
        }
      }
    } catch {
      toast.error('تعذّر طباعة الملف')
    }
  }

  // Merge all attachments into one PDF (server-side) and print it in a single job.
  async function printAllAttachments() {
    if (!doc || doc.attachments.length === 0) { toast.error('لا توجد مرفقات للطباعة'); return }
    setBusy(true)
    try {
      const url = await documents.fetchCombinedUrl(docId)
      printPdfUrl(url)
    } catch {
      toast.error('تعذّر تجهيز المرفقات للطباعة')
    } finally { setBusy(false) }
  }

  async function removeAttachment(attachmentId: number, fileName: string) {
    if (!confirm(`حذف المرفق «${fileName}»؟ لا يمكن التراجع.`)) return
    setBusy(true); setError('')
    try {
      await documents.removeAttachment(docId, attachmentId)
      await load()
      toast.success('تم حذف المرفق')
    } catch (err) {
      const ax = err as { response?: { data?: { error?: string } } }
      toast.error(ax.response?.data?.error ?? 'تعذّر حذف المرفق')
    } finally { setBusy(false) }
  }

  async function remove() {
    if (!confirm('حذف هذه الوثيقة؟ (حذف منطقي قابل للاسترجاع)')) return
    setBusy(true); setError('')
    try {
      await documents.remove(docId)
      toast.success('تم حذف الوثيقة')
      navigate('/app/documents', { replace: true })
    } catch { toast.error('تعذّر حذف الوثيقة (تحقق من صلاحياتك)'); setBusy(false) }
  }

  if (error && !doc) return (
    <div><Link to="/app/documents" className="btn btn-ghost">← رجوع</Link><p className="login__error">{error}</p></div>
  )
  if (!doc) return <p className="muted">…جارٍ التحميل</p>

  const c = CONFIDENTIALITY_LABELS[doc.confidentiality] ?? { ar: doc.confidentiality, cls: 'internal' }

  return (
    <div>
      <div className="print-header">
        <h2>{doc.title}</h2>
        <div className="print-sub mono">{doc.documentNumber} · {doc.documentTypeName} · {new Date().toLocaleString('ar')}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="kicker mono">{doc.documentNumber}</span>
          <h1>{doc.title}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('Documents.Print') && (
            <button className="btn btn-ghost" onClick={() => window.print()}>🖨 طباعة</button>
          )}
          <Link to="/app/documents" className="btn btn-ghost">← رجوع للقائمة</Link>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${doc.status.toLowerCase()}`}>{DOC_STATUS_LABELS[doc.status] ?? doc.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">الإصدار v{doc.version}</span>
          </div>
          <dl className="detail-list">
            <dt>النوع</dt><dd>{doc.documentTypeName}</dd>
            <dt>تاريخ الوثيقة</dt><dd className="mono">{doc.documentDate ?? '—'}</dd>
            <dt>تاريخ الانتهاء</dt><dd className="mono">{doc.expiryDate ?? '—'}</dd>
            <dt>مدة الحفظ (شهر)</dt><dd className="mono">{doc.retentionMonths}</dd>
            <dt>الكلمات المفتاحية</dt><dd>{doc.keywords ?? '—'}</dd>
            <dt>مكان الحفظ الفيزيائي</dt>
            <dd>{doc.physicalLocationName
              ? `${doc.physicalLocationName}${doc.boxNumber ? ` · صندوق ${doc.boxNumber}` : ''}${doc.fileNumber ? ` · ملف ${doc.fileNumber}` : ''}`
              : '—'}</dd>
            <dt>الوصف</dt><dd>{doc.description ?? '—'}</dd>
          </dl>

          {auth.hasPermission('Documents.Delete') && (
            <div className="action-bar">
              <button className="btn btn-ghost" disabled={busy} onClick={remove}>حذف الوثيقة</button>
            </div>
          )}
          {error && <p className="login__error">{error}</p>}
        </motion.section>

        <motion.aside className="doc-card timeline-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <div className="attach-header">
            <span className="kicker">ATTACHMENTS · المرفقات</span>
            {auth.hasPermission('Documents.Print') && doc.attachments.length > 0 && (
              <button className="btn btn-ghost btn-sm" disabled={busy} onClick={printAllAttachments}>
                🖨 طباعة كل المرفقات
              </button>
            )}
          </div>

          <ul className="attach-list">
            {doc.attachments.length === 0 && <li className="muted">لا توجد مرفقات</li>}
            {doc.attachments.map((a) => (
              <li key={a.id} className="attach-item">
                <button className="attach-name" title="معاينة" onClick={() => openPreview(a)}>
                  <span className="attach-ext mono">{a.fileExtension.toUpperCase()}</span>
                  <span className="attach-meta">
                    <span className="attach-fname">
                      {a.fileName}
                      {a.isScanned && <span className="attach-scanned" title="ممسوحة ضوئيًا">مسح ضوئي</span>}
                    </span>
                    <span className="attach-time mono">{new Date(a.createdAt).toLocaleString('ar')}</span>
                  </span>
                </button>
                <span className="attach-actions">
                  <span className="attach-size mono">{formatBytes(a.sizeBytes)}</span>
                  {auth.hasPermission('Documents.Print') && (
                    <button className="attach-view" title="طباعة" onClick={() => printAttachment(a)}>🖨</button>
                  )}
                  <button className="attach-view" title="تنزيل" onClick={() => documents.download(docId, a.id, a.fileName)}>⬇</button>
                  {auth.hasPermission('Documents.Edit') && (
                    <button className="attach-remove" title="حذف المرفق" disabled={busy}
                      onClick={() => removeAttachment(a.id, a.fileName)}>✕</button>
                  )}
                </span>
              </li>
            ))}
          </ul>

          {auth.hasPermission('Documents.Edit') && (
            <>
              <input ref={fileInput} type="file" style={{ display: 'none' }} onChange={onUpload}
                accept=".pdf,.docx,.doc,.xlsx,.xls,.jpg,.jpeg,.png,.zip" />
              {/* Fallback when no local scan agent is detected: pick a file the scanner produced. */}
              <input ref={scanFallbackInput} type="file" style={{ display: 'none' }} onChange={onScanFallback}
                accept=".pdf,.jpg,.jpeg,.png" />
              <label className="field scan-name-field">
                <span>اسم المستند الممسوح</span>
                <input
                  value={scanName}
                  onChange={(e) => setScanName(e.target.value)}
                  placeholder="مثال: محضر اجتماع"
                  maxLength={120}
                />
              </label>
              <label className="field scan-name-field">
                <span>صيغة الملف</span>
                <select value={scanFormat} onChange={(e) => setScanFormat(e.target.value as ScanFormat)}>
                  <option value="pdf">PDF</option>
                  <option value="jpg">JPG (صورة)</option>
                  <option value="png">PNG (صورة)</option>
                </select>
              </label>
              <div className="action-bar">
                <button className="btn btn-primary" disabled={busy} onClick={() => fileInput.current?.click()}>
                  + رفع مرفق
                </button>
                <button className="btn btn-seal" disabled={busy} onClick={onScan} title="المسح من ماسح ضوئي متصل بجهازك">
                  ⎙ مسح ضوئي
                </button>
              </div>
              {scanMsg && <p className="muted scan-msg">{scanMsg}</p>}
            </>
          )}
        </motion.aside>
      </div>

      <PreservationPackagesPanel docId={docId} documentNumber={doc.documentNumber} />
      <RecordMetadataPanel docId={docId} />

      {preview && (
        <div className="preview-overlay" onClick={closePreview}>
          <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title">{preview.name}</span>
              <span className="preview-actions">
                <button className="btn btn-ghost btn-sm" onClick={printPreview}>🖨 طباعة</button>
                <a className="btn btn-primary btn-sm" href={preview.url} download={preview.name}>⬇ تنزيل</a>
                <button className="btn btn-ghost btn-sm" onClick={closePreview}>✕ إغلاق</button>
              </span>
            </header>
            <div className="preview-body">
              {preview.type.includes('pdf') ? (
                <iframe ref={previewFrame} title="preview" src={preview.url} className="preview-frame" />
              ) : preview.type.startsWith('image/') ? (
                <img src={preview.url} alt={preview.name} className="preview-img" />
              ) : (
                <p className="muted preview-unsupported">لا يمكن معاينة هذا النوع داخل الصفحة — استخدم زر التنزيل.</p>
              )}
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
